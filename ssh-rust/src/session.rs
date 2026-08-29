//! Real russh-backed SSH session.
//!
//! A `SshSession` owns a tokio runtime and, on that runtime, an SSH connection
//! plus an interactive shell channel. FFI entry points (in `lib.rs`) drive it
//! through shared buffers and mpsc channels:
//!   - remote output is drained by a background task into a shared Vec (read
//!     by `sr_poll_read`),
//!   - local input is pushed into an mpsc channel consumed by the background
//!     task and written to the channel via `Channel::data`,
//!   - resize is sent as a control message to the read loop.
//!
//! [`TermSession`] is the protocol-agnostic view every session (SSH / Telnet /
//! Serial) exposes to the FFI layer; the session registry in [`sessions`] is a
//! `Box<dyn TermSession>` so all handles share one table.

use std::collections::HashMap;
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;

use russh::ChannelMsg;
use tokio::sync::mpsc as tokio_mpsc;

use crate::SrError;

/// Direction sent to the background read loop by `sr_write`.
enum Input {
    Data(Vec<u8>),
    Resize(u32, u32),
}

/// Shared logic for draining the output buffer shared between sessions.
/// Returns `Err(SrError::Closed)` when the buffer is empty and the session
/// has already terminated, `Ok(0)` when empty but still alive, else the
/// number of bytes copied.
fn drain_output(
    output: &Mutex<Vec<u8>>,
    closed: &Mutex<bool>,
    buf: &mut [u8],
) -> Result<usize, SrError> {
    let mut out = output.lock().unwrap();
    if out.is_empty() {
        if *closed.lock().unwrap() {
            return Err(SrError::Closed);
        }
        return Ok(0);
    }
    let n = out.len().min(buf.len());
    buf[..n].copy_from_slice(&out[..n]);
    out.drain(..n);
    Ok(n)
}

/// Protocol-agnostic view of a terminal session shared with the FFI layer.
pub trait TermSession: Send + Sync {
    /// Non-blocking write of terminal input to the remote.
    fn write(&self, data: &[u8]) -> Result<(), SrError>;
    /// Drain available output bytes into `buf`, returning how many were copied.
    fn poll_read(&self, buf: &mut [u8]) -> Result<usize, SrError>;
    fn is_closed(&self) -> bool;
    fn error_message(&self) -> Option<String>;
    /// Notify the remote of a resize (no-op for raw-stream protocols).
    fn resize(&self, cols: u32, rows: u32) -> Result<(), SrError>;
}

pub struct SshSession {
    /// Output bytes from the remote, written by the background task and drained
    /// by `sr_poll_read`. Guarded by the mutex.
    pub output: Arc<Mutex<Vec<u8>>>,
    /// Signals EOF/closure to the poller.
    pub closed: Arc<Mutex<bool>>,
    /// Error message (UTF-8) once the session terminates abnormally.
    pub error: Arc<Mutex<Option<String>>>,
    /// Kept alive for the session lifetime.
    runtime: Arc<tokio::runtime::Runtime>,
    /// Input pipe from FFI writes into the background task.
    input_tx: tokio_mpsc::Sender<Input>,
}

async fn connect_async(
    host: String,
    port: u16,
    user: String,
    password: Option<String>,
    key_path: Option<String>,
    timeout: Duration,
) -> Result<russh::client::Handle<crate::Handler>, SrError> {
    let config = Arc::new(russh::client::Config {
        // Never auto-disconnect an idle interactive session (the 10s connect
        // timeout must NOT be reused as an inactivity timeout). TCP keepalive /
        // the remote server decide when a dead peer is dropped.
        inactivity_timeout: None,
        // Keep the connection alive through NATs / firewalls: send a keepalive
        // when nothing has been received for this long, close after
        // `keepalive_max` unanswered keepalives.
        keepalive_interval: Some(Duration::from_secs(30)),
        ..<_>::default()
    });
    let handler = crate::Handler {};
    let mut session = tokio::time::timeout(timeout, russh::client::connect(config, (host.as_str(), port), handler))
        .await
        .map_err(|_| SrError::Connect(format!("connection to {host}:{port} timed out after {}s", timeout.as_secs())))?
        .map_err(|e| SrError::Connect(e.to_string()))?;

    let auth_result = match (password, key_path) {
        (Some(_), Some(_)) => {
            return Err(SrError::Connect(
                "password and private key are mutually exclusive".into(),
            ));
        }
        (Some(pw), None) => session
            .authenticate_password(user, pw)
            .await
            .map_err(|e| SrError::Connect(e.to_string()))?,
        (None, Some(key_path)) => {
            // russh accepts OpenSSH/PEM private keys here. PuTTY .ppk and
            // passphrase-protected keys are intentionally not supported by the
            // built-in runner; external runners can be used for those formats.
            let key = russh::keys::load_secret_key(key_path, None)
                .map_err(|e| SrError::Connect(format!("cannot load private key: {e}")))?;
            let hash_alg = session
                .best_supported_rsa_hash()
                .await
                .map_err(|e| SrError::Connect(e.to_string()))?
                .flatten();
            session
                .authenticate_publickey(
                    user,
                    russh::keys::PrivateKeyWithHashAlg::new(Arc::new(key), hash_alg),
                )
                .await
                .map_err(|e| SrError::Connect(e.to_string()))?
        }
        (None, None) => return Err(SrError::Connect("no credentials".into())),
    };

    if auth_result != russh::client::AuthResult::Success {
        return Err(SrError::Connect("authentication failed".into()));
    }
    Ok(session)
}

async fn run_read_loop(
    session: russh::client::Handle<crate::Handler>,
    output: Arc<Mutex<Vec<u8>>>,
    closed: Arc<Mutex<bool>>,
    error: Arc<Mutex<Option<String>>>,
    mut input_rx: tokio_mpsc::Receiver<Input>,
    startup_command: Option<String>,
) {
    let result: Result<(), russh::Error> = async {
        let mut channel = session.channel_open_session().await?;
        channel.request_pty(true, "xterm", 80, 24, 0, 0, &[]).await?;
        channel.request_shell(true).await?;

        if let Some(cmd) = startup_command {
            if !cmd.is_empty() {
                let mut inject = cmd.into_bytes();
                inject.push(b'\n');
                channel.data(inject.as_slice()).await?;
            }
        }

        loop {
            tokio::select! {
                maybe_msg = channel.wait() => {
                    let Some(msg) = maybe_msg else {
                        *closed.lock().unwrap() = true;
                        return Ok(());
                    };
                    match msg {
                        ChannelMsg::Data { ref data } => {
                            output.lock().unwrap().extend_from_slice(data);
                        }
                        ChannelMsg::ExtendedData { ref data, .. } => {
                            output.lock().unwrap().extend_from_slice(data);
                        }
                        ChannelMsg::ExitStatus { .. }
                        | ChannelMsg::ExitSignal { .. }
                        | ChannelMsg::Close
                        | ChannelMsg::Eof => {
                            *closed.lock().unwrap() = true;
                            return Ok(());
                        }
                        _ => {}
                    }
                }
                maybe_input = input_rx.recv() => {
                    let Some(msg) = maybe_input else {
                        channel.eof().await?;
                        return Ok(());
                    };
                    match msg {
                        Input::Data(data) => {
                            if data.is_empty() {
                                channel.eof().await?;
                                return Ok(());
                            }
                            channel.data(data.as_slice()).await?;
                        }
                        Input::Resize(cols, rows) => {
                            let _ = channel.window_change(cols, rows, 0, 0).await;
                        }
                    }
                }
            }
        }
    }
    .await;

    if let Err(e) = result {
        *error.lock().unwrap() = Some(e.to_string());
    }
    *closed.lock().unwrap() = true;
}

impl SshSession {
    /// Establish a session synchronously (blocks until connected or error).
    #[allow(clippy::too_many_arguments)]
    pub fn connect(
        host: String,
        port: u16,
        user: String,
        password: Option<String>,
        key_path: Option<String>,
        timeout: Duration,
        startup_command: Option<String>,
    ) -> Result<Self, SrError> {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .worker_threads(2)
            .enable_all()
            .build()
            .map_err(|e| SrError::Internal(format!("tokio runtime: {e}")))?;

        let output = Arc::new(Mutex::new(Vec::new()));
        let closed = Arc::new(Mutex::new(false));
        let error = Arc::new(Mutex::new(None));
        let (input_tx, input_rx) = tokio_mpsc::channel::<Input>(64);

        let out2 = output.clone();
        let closed2 = closed.clone();
        let err2 = error.clone();

        let connect_result = runtime.block_on(async {
            match connect_async(host, port, user, password, key_path, timeout).await {
                Ok(session) => {
                    tokio::spawn(run_read_loop(
                        session, out2, closed2, err2.clone(), input_rx, startup_command,
                    ));
                    Ok::<_, SrError>(())
                }
                Err(e) => Err(SrError::Connect(e.to_string())),
            }
        });

        if let Err(e) = connect_result {
            return Err(e);
        }

        Ok(Self {
            output,
            closed,
            error,
            runtime: Arc::new(runtime),
            input_tx,
        })
    }
}

impl TermSession for SshSession {
    /// Non-blocking write of terminal input to the remote shell.
    fn write(&self, data: &[u8]) -> Result<(), SrError> {
        if data.is_empty() {
            return Ok(());
        }
        self.input_tx
            .try_send(Input::Data(data.to_vec()))
            .map_err(|_| SrError::Closed)
    }

    /// Drain available output bytes into `buf`, returning how many were copied.
    fn poll_read(&self, buf: &mut [u8]) -> Result<usize, SrError> {
        drain_output(&self.output, &self.closed, buf)
    }

    fn is_closed(&self) -> bool {
        *self.closed.lock().unwrap()
    }

    fn error_message(&self) -> Option<String> {
        self.error.lock().unwrap().clone()
    }

    /// Notify the remote PTY of a resize (cols x rows).
    fn resize(&self, cols: u32, rows: u32) -> Result<(), SrError> {
        self.input_tx
            .try_send(Input::Resize(cols, rows))
            .map_err(|_| SrError::Closed)
    }
}

// ---------------------------------------------------------------------------
// Telnet session (minimal IAC negotiation)
// ---------------------------------------------------------------------------

/// Telnet IAC command/option bytes (RFC 854).
const IAC: u8 = 255;
const WILL: u8 = 251;
const WONT: u8 = 252;
const DO: u8 = 253;
const DONT: u8 = 254;
const SB: u8 = 250;
const SE: u8 = 240;
const NAWS: u8 = 31;

/// A raw TCP telnet session with minimal IAC negotiation:
///   - `WILL`/`DO` from the server are answered with `WONT`/`DONT` (refuse),
///   - `SB ... SE` sub-negotiation bodies are discarded,
///   - NAWS (window size) is reported to the server on resize.
pub struct TelnetSession {
    output: Arc<Mutex<Vec<u8>>>,
    closed: Arc<Mutex<bool>>,
    error: Arc<Mutex<Option<String>>>,
    runtime: Arc<tokio::runtime::Runtime>,
    input_tx: tokio_mpsc::Sender<Input>,
}

async fn run_telnet_loop(
    stream: tokio::net::TcpStream,
    output: Arc<Mutex<Vec<u8>>>,
    closed: Arc<Mutex<bool>>,
    error: Arc<Mutex<Option<String>>>,
    mut input_rx: tokio_mpsc::Receiver<Input>,
) {
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    let (mut read_half, mut write_half) = tokio::io::split(stream);

    // Telnet is line/negotiation oriented; a small state machine keeps track of
    // a pending IAC sequence across read boundaries.
    let mut iac_pending = false; // last byte seen was IAC
    let mut expect_opt = false;  // IAC WILL/DO/WONT/DONT, next byte is the option
    let mut in_subneg = false;   // inside IAC SB ... IAC SE

    let result: Result<(), SrError> = async {
        let mut buf = [0u8; 4096];
        loop {
            tokio::select! {
                r = read_half.read(&mut buf) => {
                    let n = r.map_err(|e| SrError::Internal(e.to_string()))?;
                    if n == 0 {
                        return Ok(()); // remote closed
                    }
                    let mut out_chunk: Vec<u8> = Vec::new();
                    let mut replies: Vec<u8> = Vec::new();
                    for &b in &buf[..n] {
                        if in_subneg {
                            if iac_pending {
                                if b == SE {
                                    in_subneg = false;
                                }
                                iac_pending = false;
                            } else if b == IAC {
                                iac_pending = true;
                            }
                            continue;
                        }
                        if iac_pending {
                            match b {
                                WILL | DO | WONT | DONT => {
                                    expect_opt = true;
                                }
                                SB => {
                                    in_subneg = true;
                                }
                                IAC => {
                                    // escaped IAC -> literal 0xFF
                                    out_chunk.push(IAC);
                                }
                                _ => { /* other IAC commands (NOP etc.) ignored */ }
                            }
                            iac_pending = false;
                            continue;
                        }
                        if expect_opt {
                            match b {
                                WILL => replies.extend_from_slice(&[IAC, WONT]),
                                DO => replies.extend_from_slice(&[IAC, DONT]),
                                _ => { /* WONT/DONT: nothing to answer */ }
                            }
                            // The reply command is followed by this option byte.
                            replies.push(b);
                            expect_opt = false;
                            continue;
                        }
                        if b == IAC {
                            iac_pending = true;
                            continue;
                        }
                        out_chunk.push(b);
                    }
                    if !out_chunk.is_empty() {
                        output.lock().unwrap().extend_from_slice(&out_chunk);
                    }
                    if !replies.is_empty() {
                        write_half.write_all(&replies).await.map_err(|e| SrError::Internal(e.to_string()))?;
                    }
                }
                maybe_input = input_rx.recv() => {
                    let Some(msg) = maybe_input else {
                        return Ok(());
                    };
                    match msg {
                        Input::Data(data) => {
                            write_half.write_all(&data).await.map_err(|e| SrError::Internal(e.to_string()))?;
                        }
                        Input::Resize(..) => { /* telnet NAWS is sent via Data by resize() */ }
                    }
                }
            }
        }
    }
    .await;

    if let Err(e) = result {
        *error.lock().unwrap() = Some(e.to_string());
    }
    *closed.lock().unwrap() = true;
}

impl TelnetSession {
    /// Establish a raw TCP telnet session synchronously.
    pub fn connect(host: String, port: u16, timeout: Duration) -> Result<Self, SrError> {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .worker_threads(2)
            .enable_all()
            .build()
            .map_err(|e| SrError::Internal(format!("tokio runtime: {e}")))?;

        let output = Arc::new(Mutex::new(Vec::new()));
        let closed = Arc::new(Mutex::new(false));
        let error = Arc::new(Mutex::new(None));
        let (input_tx, input_rx) = tokio_mpsc::channel::<Input>(64);

        let out2 = output.clone();
        let closed2 = closed.clone();
        let err2 = error.clone();

        let connect_result = runtime.block_on(async {
            match tokio::time::timeout(
                timeout,
                tokio::net::TcpStream::connect((host.as_str(), port)),
            )
            .await
            {
                Ok(Ok(stream)) => {
                    tokio::spawn(run_telnet_loop(
                        stream, out2, closed2, err2, input_rx,
                    ));
                    Ok::<_, SrError>(())
                }
                Ok(Err(e)) => Err(SrError::Connect(e.to_string())),
                Err(_) => Err(SrError::Connect(format!(
                    "connection to {host}:{port} timed out after {}s",
                    timeout.as_secs()
                ))),
            }
        });

        if let Err(e) = connect_result {
            return Err(e);
        }

        Ok(Self {
            output,
            closed,
            error,
            runtime: Arc::new(runtime),
            input_tx,
        })
    }
}

impl TermSession for TelnetSession {
    fn write(&self, data: &[u8]) -> Result<(), SrError> {
        if data.is_empty() {
            return Ok(());
        }
        self.input_tx
            .try_send(Input::Data(data.to_vec()))
            .map_err(|_| SrError::Closed)
    }

    fn poll_read(&self, buf: &mut [u8]) -> Result<usize, SrError> {
        drain_output(&self.output, &self.closed, buf)
    }

    fn is_closed(&self) -> bool {
        *self.closed.lock().unwrap()
    }

    fn error_message(&self) -> Option<String> {
        self.error.lock().unwrap().clone()
    }

    /// Report a window resize to the server via telnet NAWS.
    fn resize(&self, cols: u32, rows: u32) -> Result<(), SrError> {
        // IAC SB NAWS <cols:hi,lo> <rows:hi,lo> IAC SE
        let mut msg = vec![IAC, SB, NAWS];
        msg.extend_from_slice(&[(cols >> 8) as u8, (cols & 0xff) as u8]);
        msg.extend_from_slice(&[(rows >> 8) as u8, (rows & 0xff) as u8]);
        msg.extend_from_slice(&[IAC, SE]);
        self.input_tx
            .try_send(Input::Data(msg))
            .map_err(|_| SrError::Closed)
    }
}

// ---------------------------------------------------------------------------
// Serial session
// ---------------------------------------------------------------------------

/// Serial port session. Unlike SSH/Telnet this is a raw byte stream with no
/// PTY and no remote; `serialport` is a synchronous API so I/O runs on
/// dedicated threads bridged through mpsc channels.
pub struct SerialSession {
    output: Arc<Mutex<Vec<u8>>>,
    closed: Arc<Mutex<bool>>,
    error: Arc<Mutex<Option<String>>>,
    input_tx: std::sync::mpsc::Sender<Vec<u8>>,
}

/// Run the serial port read loop on a dedicated thread. Fills `output` until
/// the port errors or closes.
fn run_serial_read_loop(
    mut port: Box<dyn serialport::SerialPort>,
    output: Arc<Mutex<Vec<u8>>>,
    closed: Arc<Mutex<bool>>,
    error: Arc<Mutex<Option<String>>>,
) {
    let mut buf = [0u8; 4096];
    loop {
        match port.read(&mut buf) {
            Ok(0) => {
                *closed.lock().unwrap() = true;
                break;
            }
            Ok(n) => {
                output.lock().unwrap().extend_from_slice(&buf[..n]);
            }
            Err(e) if e.kind() == std::io::ErrorKind::TimedOut => continue,
            Err(e) => {
                *error.lock().unwrap() = Some(e.to_string());
                *closed.lock().unwrap() = true;
                break;
            }
        }
    }
}

impl SerialSession {
    /// Open a serial port synchronously. `parity`/`stop_bits`/`flow_control`
    /// are encoded as integers by the C# side (see `Serial.cs`).
    #[allow(clippy::too_many_arguments)]
    pub fn connect(
        port_name: String,
        baud_rate: u32,
        data_bits: u8,
        parity: u8,       // 0=None, 1=Odd, 2=Even, 3=Mark, 4=Space
        stop_bits: u8,    // 0=One, 1=Two
        flow_control: u8, // 0=None, 1=XON/XOFF, 2=RTS/CTS, 3=DSR/DTR
    ) -> Result<Self, SrError> {
        // `serialport` 4.x supports only None/Odd/Even parity on Windows; Mark
        // and Space (encoded as 3/4 by the C# side) fall back to None.
        let builder = serialport::new(port_name.clone(), baud_rate)
            .data_bits(match data_bits {
                5 => serialport::DataBits::Five,
                6 => serialport::DataBits::Six,
                7 => serialport::DataBits::Seven,
                _ => serialport::DataBits::Eight,
            })
            .parity(match parity {
                1 => serialport::Parity::Odd,
                2 => serialport::Parity::Even,
                _ => serialport::Parity::None,
            })
            .stop_bits(match stop_bits {
                1 => serialport::StopBits::Two,
                _ => serialport::StopBits::One,
            })
            .flow_control(match flow_control {
                1 => serialport::FlowControl::Software,
                2 => serialport::FlowControl::Hardware,
                _ => serialport::FlowControl::None,
            })
            .timeout(std::time::Duration::from_millis(100));
        let mut port = builder
            .open()
            .map_err(|e| SrError::Connect(format!("{port_name}: {e}")))?;

        let output = Arc::new(Mutex::new(Vec::new()));
        let closed = Arc::new(Mutex::new(false));
        let error = Arc::new(Mutex::new(None));

        // Read loop on a dedicated thread (serialport is a blocking API).
        let read_port = port.try_clone().map_err(|e| SrError::Internal(e.to_string()))?;
        let out_r = output.clone();
        let closed_r = closed.clone();
        let err_r = error.clone();
        std::thread::spawn(move || run_serial_read_loop(read_port, out_r, closed_r, err_r));

        // Write loop on a dedicated thread, bridged through an mpsc channel.
        let (input_tx, input_rx) = std::sync::mpsc::channel::<Vec<u8>>();
        let closed_w = closed.clone();
        std::thread::spawn(move || {
            while let Ok(data) = input_rx.recv() {
                if data.is_empty() {
                    break;
                }
                if port.write_all(&data).is_err() {
                    break;
                }
            }
            *closed_w.lock().unwrap() = true;
        });

        Ok(Self {
            output,
            closed,
            error,
            input_tx,
        })
    }
}

impl TermSession for SerialSession {
    fn write(&self, data: &[u8]) -> Result<(), SrError> {
        if data.is_empty() {
            return Ok(());
        }
        self.input_tx
            .send(data.to_vec())
            .map_err(|_| SrError::Closed)
    }

    fn poll_read(&self, buf: &mut [u8]) -> Result<usize, SrError> {
        drain_output(&self.output, &self.closed, buf)
    }

    fn is_closed(&self) -> bool {
        *self.closed.lock().unwrap()
    }

    fn error_message(&self) -> Option<String> {
        self.error.lock().unwrap().clone()
    }

    /// No-op: a serial port has no remote window size.
    fn resize(&self, _cols: u32, _rows: u32) -> Result<(), SrError> {
        Ok(())
    }
}

/// Global session registry keyed by `i64` handle.
pub fn sessions() -> &'static Arc<Mutex<HashMap<i64, Box<dyn TermSession>>>> {
    static SESSIONS: OnceLock<Arc<Mutex<HashMap<i64, Box<dyn TermSession>>>>> = OnceLock::new();
    SESSIONS.get_or_init(|| Arc::new(Mutex::new(HashMap::new())))
}
