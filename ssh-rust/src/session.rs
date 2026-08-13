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

    let auth_result = if let Some(pw) = password {
        session
            .authenticate_password(user, pw)
            .await
            .map_err(|e| SrError::Connect(e.to_string()))?
    } else if let Some(key) = key_path {
        let key = russh::keys::load_secret_key(key, None)
            .map_err(|e| SrError::Connect(format!("cannot load key: {e}")))?;
        let hash_alg = session
            .best_supported_rsa_hash()
            .await
            .map_err(|e| SrError::Connect(e.to_string()))?
            .flatten();
        session
            .authenticate_publickey(user, russh::keys::PrivateKeyWithHashAlg::new(Arc::new(key), hash_alg))
            .await
            .map_err(|e| SrError::Connect(e.to_string()))?
    } else {
        return Err(SrError::Connect("no credentials".into()));
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

    /// Non-blocking write of terminal input to the remote shell.
    pub fn write(&self, data: &[u8]) -> Result<(), SrError> {
        if data.is_empty() {
            return Ok(());
        }
        self.input_tx
            .try_send(Input::Data(data.to_vec()))
            .map_err(|_| SrError::Closed)
    }

    /// Drain available output bytes into `buf`, returning how many were copied.
    pub fn poll_read(&self, buf: &mut [u8]) -> Result<usize, SrError> {
        let mut out = self.output.lock().unwrap();
        if out.is_empty() {
            if *self.closed.lock().unwrap() {
                return Err(SrError::Closed);
            }
            return Ok(0);
        }
        let n = out.len().min(buf.len());
        buf[..n].copy_from_slice(&out[..n]);
        out.drain(..n);
        Ok(n)
    }

    pub fn is_closed(&self) -> bool {
        *self.closed.lock().unwrap()
    }

    pub fn error_message(&self) -> Option<String> {
        self.error.lock().unwrap().clone()
    }

    /// Notify the remote PTY of a resize (cols x rows).
    pub fn resize(&self, cols: u32, rows: u32) -> Result<(), SrError> {
        self.input_tx
            .try_send(Input::Resize(cols, rows))
            .map_err(|_| SrError::Closed)
    }
}

/// Global session registry keyed by `i64` handle.
pub fn sessions() -> &'static Arc<Mutex<HashMap<i64, SshSession>>> {
    static SESSIONS: OnceLock<Arc<Mutex<HashMap<i64, SshSession>>>> = OnceLock::new();
    SESSIONS.get_or_init(|| Arc::new(Mutex::new(HashMap::new())))
}
