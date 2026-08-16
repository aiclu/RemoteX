//! Minimal RFB (VNC) protocol client, aligned with the behavior of the old
//! `1Remote.VncSharpCore` library (see docs/adr/0009).
//!
//! Implements:
//!   - protocol handshake 3.3/3.7/3.8, security type negotiation
//!   - auth: VNC password (DES challenge) and None
//!   - ServerInit: framebuffer size + pixel format negotiation
//!   - encodings: Raw, CopyRect, Hextile, Tight (zlib), ZRLE
//!   - input events: keyboard, pointer (mouse), framebuffer resize request
//!
//! A `VncSession` owns a worker thread that runs the RFB read loop and decodes
//! framebuffer updates into a shared BGRA pixel buffer; the FFI layer exposes
//! polling of that buffer and a command channel for input.

use std::collections::HashMap;
use std::io::{self, Read, Write};
use std::net::{TcpStream, ToSocketAddrs};
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;

// ---------------------------------------------------------------------------
// RFB protocol constants
// ---------------------------------------------------------------------------

const RFB_VERSION: &[u8; 12] = b"RFB 003.008\n";

const SECURITY_TYPE_INVALID: u8 = 0;
const SECURITY_TYPE_NONE: u8 = 1;
const SECURITY_TYPE_VNC_AUTH: u8 = 2;

const ENCODING_RAW: i32 = 0;
const ENCODING_COPY_RECT: i32 = 1;
const ENCODING_RRE: i32 = 2;
const ENCODING_HEXTILE: i32 = 5;
const ENCODING_TIGHT: i32 = 7;
const ENCODING_ZRLE: i32 = 16;

const ENCODING_FB_RESIZE: i32 = -223; // desktop resize (non-RFB pseudo encoding)

const MSG_FRAMEBUFFER_UPDATE: u8 = 0;
const MSG_SET_COLOUR_MAP: u8 = 1;
const MSG_BELL: u8 = 2;
const MSG_SERVER_CUT_TEXT: u8 = 3;

const CLIENT_SET_PIXEL_FORMAT: u8 = 0;
const CLIENT_SET_ENCODINGS: u8 = 2;
const CLIENT_FRAMEBUFFER_UPDATE_REQUEST: u8 = 3;
const CLIENT_KEY_EVENT: u8 = 4;
const CLIENT_POINTER_EVENT: u8 = 5;
const CLIENT_CLIENT_CUT_TEXT: u8 = 6;

// pointer buttons
pub const PTR_LEFT: u8 = 1;
pub const PTR_MIDDLE: u8 = 2;
pub const PTR_RIGHT: u8 = 4;
pub const PTR_WHEEL_UP: u8 = 8;
pub const PTR_WHEEL_DOWN: u8 = 16;

// ---------------------------------------------------------------------------
// Public session type
// ---------------------------------------------------------------------------

pub struct VncSession {
    pub host: String,
    pub port: u16,
    pub password: Option<String>,
    /// True once the RFB handshake completed and the framebuffer is live.
    pub connected: Arc<AtomicBool>,
    /// Set when the connection terminates (EOF/error).
    pub closed: Arc<AtomicBool>,
    /// Error message once closed.
    pub error: Arc<Mutex<Option<String>>>,
    /// Framebuffer geometry (pixels).
    pub width: Arc<Mutex<u32>>,
    pub height: Arc<Mutex<u32>>,
    /// BGRA pixel buffer (width*height*4).
    pub pixels: Arc<Mutex<Vec<u8>>>,
    /// Monotonic counter bumped after each completed framebuffer update.
    pub frame_seq: Arc<Mutex<u64>>,
    pub(crate) worker: Option<std::thread::JoinHandle<()>>,
    /// Command channel for input + resize.
    pub cmd_tx: std::sync::mpsc::Sender<VncCommand>,
}

pub enum VncCommand {
    Pointer { x: u16, y: u16, buttons: u8 },
    Key { keysym: u32, down: bool },
    RequestFbUpdate,
}

/// Debug helper: append a line to a log file so we can trace the VNC
/// handshake/decode progress without a debugger. Only active when the
/// environment variable REMOTEX_VNC_LOG is set (to any value).
fn vlog(msg: &str) {
    if std::env::var("REMOTEX_VNC_LOG").is_err() {
        return;
    }
    use std::io::Write;
    if let Ok(mut f) = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open("remotex_vnc.log")
    {
        let _ = writeln!(f, "{}", msg);
    }
}

// ---------------------------------------------------------------------------
// Session registry (independent handle space)
// ---------------------------------------------------------------------------

pub fn sessions() -> &'static Mutex<HashMap<i64, VncSession>> {
    static SESSIONS: OnceLock<Mutex<HashMap<i64, VncSession>>> = OnceLock::new();
    SESSIONS.get_or_init(|| Mutex::new(HashMap::new()))
}

static VNC_NEXT_HANDLE: AtomicI64 = AtomicI64::new(3_000_000_000);

pub fn connect(
    host: &str,
    port: u16,
    password: Option<&str>,
    timeout: Duration,
) -> Result<i64, String> {
    let (tx, rx) = std::sync::mpsc::channel::<VncCommand>();
    let session = VncSession {
        host: host.to_string(),
        port,
        password: password.map(|s| s.to_string()),
        connected: Arc::new(AtomicBool::new(false)),
        closed: Arc::new(AtomicBool::new(false)),
        error: Arc::new(Mutex::new(None)),
        width: Arc::new(Mutex::new(0)),
        height: Arc::new(Mutex::new(0)),
        pixels: Arc::new(Mutex::new(Vec::new())),
        frame_seq: Arc::new(Mutex::new(0)),
        cmd_tx: tx,
        worker: None,
    };
    let s = session;

    let handle = VNC_NEXT_HANDLE.fetch_add(1, Ordering::SeqCst);
    {
        let mut sessions = sessions().lock().unwrap();
        sessions.insert(handle, s);
    }

    // Detach so the caller never blocks on the handshake; spawn the worker.
    let weak_host = host.to_string();
    let weak_port = port;
    let weak_pw = password.map(|s| s.to_string());
    let sessions_static = sessions(); // &'static Mutex<...>
    let h = handle;
    let worker = std::thread::spawn(move || {
        let result = run_session(sessions_static, h, &weak_host, weak_port, weak_pw.as_deref(), rx, timeout);
        // Mark the session closed + record any error, so sr_vnc_poll reports it.
        if let Some(session) = sessions_static.lock().unwrap().get_mut(&h) {
            session.closed.store(true, Ordering::SeqCst);
            match &result {
                Err(e) => {
                    vlog(&format!("worker: run_session FAILED: {e}"));
                    *session.error.lock().unwrap() = Some(e.clone());
                }
                Ok(()) => {
                    vlog("worker: run_session exited normally (closed)");
                }
            }
        }
    });
    if let Some(entry) = sessions().lock().unwrap().get_mut(&handle) {
        entry.worker = Some(worker);
    }
    Ok(handle)
}

pub fn disconnect(handle: i64) {
    // Remove the session in a scoped block so the registry MutexGuard is
    // dropped BEFORE worker.join(). The worker's read_loop needs to lock the
    // registry to observe the `closed` flag; holding the lock across join()
    // would deadlock.
    let removed = {
        let mut guard = sessions().lock().unwrap();
        guard.remove(&handle)
    };
    if let Some(mut s) = removed {
        s.closed.store(true, Ordering::SeqCst);
        if let Some(w) = s.worker.take() {
            let _ = w.join();
        }
    }
}

fn run_session(
    sessions: &'static Mutex<HashMap<i64, VncSession>>,
    handle: i64,
    host: &str,
    port: u16,
    password: Option<&str>,
    rx: std::sync::mpsc::Receiver<VncCommand>,
    timeout: Duration,
) -> Result<(), String> {
    // Resolve + TCP connect with timeout. Use (host, port) so that plain
    // hostnames like "localhost" are resolved correctly (host.to_socket_addrs()
    // expects a "host:port" string and fails on bare names).
    let addr = (host, port)
        .to_socket_addrs()
        .map_err(|e| format!("dns resolve {host}: {e}"))?
        .next()
        .ok_or_else(|| format!("no address for {host}"))?;
    let stream = TcpStream::connect_timeout(&addr, timeout)
        .map_err(|e| format!("connect {host}:{port}: {e}"))?;
    stream.set_nodelay(true).ok();
    // A short read timeout keeps read_loop responsive to the `closed` flag so
    // disconnect()/worker.join() can always terminate instead of hanging.
    let _ = stream.set_read_timeout(Some(Duration::from_millis(200)));

    let mut reader = stream.try_clone().map_err(|e| format!("clone: {e}"))?;
    // try_clone may not inherit SO_RCVTIMEO on every platform; set it again on
    // the read side so read_exact never blocks forever.
    let _ = reader.set_read_timeout(Some(Duration::from_millis(200)));
    let mut writer = stream;
    let (width, height) = handshake(&mut reader, &mut writer, password)?;

    // Publish framebuffer.
    {
        let mut guard = sessions.lock().unwrap();
        let session = guard.get_mut(&handle).ok_or("session gone")?;
        *session.width.lock().unwrap() = width;
        *session.height.lock().unwrap() = height;
        session
            .pixels
            .lock()
            .unwrap()
            .resize(width as usize * height as usize * 4, 0);
        session.connected.store(true, Ordering::SeqCst);
    }

    // Request pixel format (32bpp true-colour) + encodings + an initial update.
    request_pixel_format_and_encodings(&mut writer, width, height)?;
    request_fb_update(&mut writer)?;

    read_loop(sessions, handle, &mut reader, &mut writer, &rx)
}

// ---------------------------------------------------------------------------
// Handshake
// ---------------------------------------------------------------------------

fn read_exact(r: &mut dyn Read, buf: &mut [u8]) -> Result<(), String> {
    r.read_exact(buf).map_err(|e| format!("read: {e}"))
}

fn write_all(w: &mut dyn Write, buf: &[u8]) -> Result<(), String> {
    w.write_all(buf).map_err(|e| format!("write: {e}"))
}

fn handshake<R: Read, W: Write>(
    reader: &mut R,
    writer: &mut W,
    password: Option<&str>,
) -> Result<(u32, u32), String> {
    // 1. version handshake: send 3.8, read server's version.
    write_all(writer, RFB_VERSION)?;
    let mut server_version = [0u8; 12];
    read_exact(reader, &mut server_version)?;
    if !server_version.starts_with(b"RFB 003.") {
        return Err(format!(
            "unsupported RFB version: {}",
            String::from_utf8_lossy(&server_version)
        ));
    }
    vlog(&format!("handshake: server version = {:?}", String::from_utf8_lossy(&server_version)));
    // RFB 3.7 does NOT expect the client to send a SharedDesktop flag before
    // ServerInit; only 3.8+ does. Match the server's major.minor so we don't
    // feed it a stray byte that it will then misinterpret as the high byte
    // of the framebuffer width. Version string looks like "RFB 003.008\n".
    let ver_str = String::from_utf8_lossy(&server_version[4..11]).to_string(); // "003.008"
    let is_rfb38 = !ver_str.starts_with("003.003") && !ver_str.starts_with("003.007");
    vlog(&format!("server version = {ver_str}, is_rfb38 = {is_rfb38}"));

    // 2. security types.
    let mut count = [0u8; 1];
    read_exact(reader, &mut count)?;
    let n = count[0] as usize;
    if n == 0 {
        let mut len_b = [0u8; 4];
        read_exact(reader, &mut len_b)?;
        let len = u32::from_be_bytes(len_b) as usize;
        let mut reason = vec![0u8; len];
        read_exact(reader, &mut reason)?;
        return Err(format!(
            "server refused: {}",
            String::from_utf8_lossy(&reason)
        ));
    }
    let mut types = vec![0u8; n];
    read_exact(reader, &mut types)?;
    let chosen = if types.contains(&SECURITY_TYPE_VNC_AUTH) {
        SECURITY_TYPE_VNC_AUTH
    } else if types.contains(&SECURITY_TYPE_NONE) {
        SECURITY_TYPE_NONE
    } else {
        return Err(format!("no supported security type (offered {:?})", types));
    };
    write_all(writer, &[chosen])?;
    vlog(&format!("handshake: security types = {:?}, chosen = {}", types, chosen));

    match chosen {
        SECURITY_TYPE_VNC_AUTH => {
            let mut challenge = [0u8; 16];
            read_exact(reader, &mut challenge)?;
            let resp = vnc_des_challenge(password.unwrap_or(""), &challenge);
            write_all(writer, &resp)?;
            let mut result = [0u8; 4];
            read_exact(reader, &mut result)?;
            if u32::from_be_bytes(result) != 0 {
                return Err("authentication failed".to_string());
            }
        }
        SECURITY_TYPE_NONE => {
            // 3.8: 4-byte status word
            let mut result = [0u8; 4];
            read_exact(reader, &mut result)?;
            if u32::from_be_bytes(result) != 0 {
                return Err("server rejected None security".to_string());
            }
        }
        _ => unreachable!(),
    }

    // 3. client init (shared = false) — only for RFB 3.8+.
    if is_rfb38 {
        write_all(writer, &[0])?;
    }

    // 4. server init
    let mut wh = [0u8; 2];
    read_exact(reader, &mut wh)?;
    let width = u16::from_be_bytes(wh) as u32;
    read_exact(reader, &mut wh)?;
    let height = u16::from_be_bytes(wh) as u32;
    // server pixel format (16 bytes) + name
    let mut pf = [0u8; 16];
    read_exact(reader, &mut pf)?;
    let mut name_len_b = [0u8; 4];
    read_exact(reader, &mut name_len_b)?;
    let name_len = u32::from_be_bytes(name_len_b) as usize;
    // Always consume the name string, regardless of length, so the stream
    // stays in sync. A long/malformed length must not be silently skipped.
    if name_len > 0 && name_len < 1 << 20 {
        let mut name = vec![0u8; name_len];
        read_exact(reader, &mut name)?;
    } else if name_len != 0 {
        return Err(format!("server init: implausible name length {name_len}"));
    }
    vlog(&format!("handshake: server init done, {}x{}", width, height));
    Ok((width, height))
}

/// VNC DES password challenge.
///
/// The VNC password (max 8 bytes, zero-padded) becomes the DES key after
/// reversing the bit order of *each* byte (see RFB spec §6.2.1 / TightVNC
/// `vncEncryptBytes`). It is NOT a simple XOR 0xFF and the key array is NOT
/// reversed as a whole.
fn vnc_des_challenge(password: &str, challenge: &[u8; 16]) -> [u8; 16] {
    use des::Des;
    use des::cipher::generic_array::GenericArray;
    use des::cipher::{BlockEncrypt, KeyInit};

    let mut key = [0u8; 8];
    let bytes = password.as_bytes();
    for i in 0..8 {
        let b = if i < bytes.len() { bytes[i] } else { 0 };
        key[i] = b.reverse_bits();
    }
    let cipher = Des::new(GenericArray::from_slice(&key));
    let mut out = [0u8; 16];
    for block in 0..2 {
        let input = GenericArray::from_slice(&challenge[block * 8..block * 8 + 8]);
        let mut buf = *input;
        cipher.encrypt_block(&mut buf);
        out[block * 8..block * 8 + 8].copy_from_slice(&buf);
    }
    out
}

// ---------------------------------------------------------------------------
// Client messages
// ---------------------------------------------------------------------------

fn request_pixel_format_and_encodings(
    writer: &mut dyn Write,
    width: u32,
    height: u32,
) -> Result<(), String> {
    // SetPixelFormat: 32bpp, true-colour, little-endian, BGRA with shifts.
    // Message layout: msg-type (1) + padding (3) + pixel-format (16) = 20 bytes.
    let mut msg = Vec::with_capacity(20);
    msg.push(CLIENT_SET_PIXEL_FORMAT);
    msg.push(0);
    msg.push(0);
    msg.push(0); // padding (3 bytes)
    msg.push(32); // bpp
    msg.push(24); // depth
    msg.push(0);  // big-endian flag
    msg.push(1);  // true-colour flag
    msg.push(0x00); msg.push(0xFF); // red-max (U16 BE)
    msg.push(0x00); msg.push(0xFF); // green-max
    msg.push(0x00); msg.push(0xFF); // blue-max
    msg.push(16); // red-shift
    msg.push(8);  // green-shift
    msg.push(0);  // blue-shift
    msg.push(0);  // padding
    msg.push(0);
    msg.push(0);  // padding (3 bytes total)
    write_all(writer, &msg)?;

    // SetEncodings: raw, copyrect, rre, hextile + desktop resize pseudo.
    // Tight/ZRLE are intentionally NOT requested: their decoders only handle a
    // subset of tile/filter variants and a hit on an unsupported one makes the
    // whole read loop bail -> "Disconnected". TigerVNC is happy to fall back
    // to Hextile/Raw when Tight is not offered.
    // Layout: msg-type(1) + padding(1) + count(2) + encodings(4 each).
    let encodings = [
        ENCODING_RAW,
        ENCODING_COPY_RECT,
        ENCODING_RRE,
        ENCODING_HEXTILE,
        ENCODING_FB_RESIZE,
    ];
    let mut msg = Vec::with_capacity(4 + encodings.len() * 4);
    msg.push(CLIENT_SET_ENCODINGS);
    msg.push(0); // padding
    let count = encodings.len() as u16;
    msg.extend_from_slice(&count.to_be_bytes());
    for e in encodings {
        msg.extend_from_slice(&e.to_be_bytes());
    }
    write_all(writer, &msg)?;

    // One last thing: tell the server our client-init pixel format is 32-bit.
    // Request an initial framebuffer update (full screen).
    request_fb_update_full(writer, width, height)
}

fn request_fb_update_full(
    writer: &mut dyn Write,
    width: u32,
    height: u32,
) -> Result<(), String> {
    let mut msg = Vec::with_capacity(10);
    msg.push(CLIENT_FRAMEBUFFER_UPDATE_REQUEST);
    msg.push(0); // incremental = 0 (full)
    msg.extend_from_slice(&0u16.to_be_bytes());
    msg.extend_from_slice(&0u16.to_be_bytes());
    msg.extend_from_slice(&(width as u16).to_be_bytes());
    msg.extend_from_slice(&(height as u16).to_be_bytes());
    write_all(writer, &msg)
}

fn request_fb_update(writer: &mut dyn Write) -> Result<(), String> {
    let mut msg = Vec::with_capacity(10);
    msg.push(CLIENT_FRAMEBUFFER_UPDATE_REQUEST);
    msg.push(1); // incremental
    msg.extend_from_slice(&0u16.to_be_bytes());
    msg.extend_from_slice(&0u16.to_be_bytes());
    msg.extend_from_slice(&u16::MAX.to_be_bytes());
    msg.extend_from_slice(&u16::MAX.to_be_bytes());
    write_all(writer, &msg)
}

fn send_pointer(writer: &mut dyn Write, x: u16, y: u16, buttons: u8) -> Result<(), String> {
    let mut msg = Vec::with_capacity(6);
    msg.push(CLIENT_POINTER_EVENT);
    msg.push(buttons);
    msg.extend_from_slice(&x.to_be_bytes());
    msg.extend_from_slice(&y.to_be_bytes());
    write_all(writer, &msg)
}

fn send_key(writer: &mut dyn Write, keysym: u32, down: bool) -> Result<(), String> {
    let mut msg = Vec::with_capacity(8);
    msg.push(CLIENT_KEY_EVENT);
    msg.push(if down { 1 } else { 0 });
    msg.extend_from_slice(&[0u8; 2]); // padding
    msg.extend_from_slice(&keysym.to_be_bytes());
    write_all(writer, &msg)
}

// ---------------------------------------------------------------------------
// Read loop
// ---------------------------------------------------------------------------

fn read_loop(
    sessions: &Mutex<HashMap<i64, VncSession>>,
    handle: i64,
    reader: &mut TcpStream,
    writer: &mut TcpStream,
    rx: &std::sync::mpsc::Receiver<VncCommand>,
) -> Result<(), String> {
    // Local framebuffer + geometry; grown/resized by the decoder as needed.
    // Initialize the geometry from the handshake result (published on the
    // session), otherwise the first full-screen rect would be dropped as
    // out-of-range by the x+w > width check below.
    let (init_w, init_h) = {
        let sessions = sessions.lock().unwrap();
        let s = sessions.get(&handle).ok_or("session gone")?;
        let w = *s.width.lock().unwrap();
        let h = *s.height.lock().unwrap();
        (w, h)
    };
    let mut framebuf: Vec<u8> = vec![0u8; (init_w * init_h * 4) as usize];
    let mut fb_width: u32 = init_w;
    let mut fb_height: u32 = init_h;
    let mut frame_count: u64 = 0;

    loop {
        // Exit promptly when the session is being torn down. This matters even
        // while frames keep streaming in (e.g. an animated desktop), otherwise
        // disconnect()/worker.join() would never return.
        {
            let closed = {
                let guard = sessions.lock().unwrap();
                guard
                    .get(&handle)
                    .map(|s| s.closed.load(Ordering::SeqCst))
                    .unwrap_or(true)
            };
            if closed {
                return Ok(());
            }
        }

        // Drain any pending input commands first.
        while let Ok(cmd) = rx.try_recv() {
            match cmd {
                VncCommand::Pointer { x, y, buttons } => {
                    send_pointer(writer, x, y, buttons)?;
                }
                VncCommand::Key { keysym, down } => {
                    send_key(writer, keysym, down)?;
                }
                VncCommand::RequestFbUpdate => {
                    request_fb_update(writer)?;
                }
            }
        }

        // Read server message type. Use a SHORT timeout here: this is the only
        // point where the loop can promptly observe the `closed` flag so
        // disconnect()/worker.join() returns quickly, and a message type is
        // just 1 byte so a short timeout never aborts a mid-frame read.
        let _ = reader.set_read_timeout(Some(Duration::from_millis(500)));
        let mut typ = [0u8; 1];
        match reader.read_exact(&mut typ) {
            Ok(()) => {}
            Err(e) if e.kind() == io::ErrorKind::WouldBlock || e.kind() == io::ErrorKind::TimedOut => {
                // timeout: re-drain commands; exit promptly when the session is
                // being torn down so disconnect()/worker.join() can return.
                let closed = {
                    let guard = sessions.lock().unwrap();
                    guard
                        .get(&handle)
                        .map(|s| s.closed.load(Ordering::SeqCst))
                        .unwrap_or(true)
                };
                if closed {
                    return Ok(());
                }
                continue;
            }
            Err(e) => return Err(format!("read message type: {e}")),
        }
        // Long timeout for the message body: a full-screen hextile/tight frame
        // is several MB and can take longer than 500ms over an SSH tunnel. A
        // short timeout here would abort mid-frame and kill the connection
        // (the "disconnected" symptom). The body read always completes or EOFs
        // promptly once the server starts sending.
        let _ = reader.set_read_timeout(Some(Duration::from_secs(30)));

        // Byte-level diagnostics when REMOTEX_VNC_DUMP=1: for each message we
        // don't recognise, print the message type plus the next bytes so we can
        // tell whether the stream is misaligned or the server is sending an
        // extension we don't handle.
        if std::env::var("REMOTEX_VNC_DUMP").is_ok() && !matches!(typ[0], 0 | 1 | 2 | 3) {
            let mut dump = [0u8; 16];
            match reader.read_exact(&mut dump) {
                Ok(()) => vlog(&format!(
                    "read_loop: unknown type {typ:?} -> next bytes {dump:02X?} (ascii {:?})",
                    String::from_utf8_lossy(&dump)
                )),
                Err(_) => {}
            }
        }

        match typ[0] {
            MSG_FRAMEBUFFER_UPDATE => {
                let mut pad = [0u8; 1];
                read_exact(reader, &mut pad)?;
                let mut nrect_b = [0u8; 2];
                read_exact(reader, &mut nrect_b)?;
                let nrect = u16::from_be_bytes(nrect_b);
                for _ in 0..nrect {
                    decode_rectangle(
                        reader,
                        &mut framebuf,
                        &mut fb_width,
                        &mut fb_height,
                    )?;
                }
                // publish
                {
                    let guard = sessions.lock().unwrap();
                    let session = guard.get(&handle).ok_or("session gone")?;
                    *session.pixels.lock().unwrap() = framebuf.clone();
                    *session.frame_seq.lock().unwrap() += 1;
                }
                frame_count += 1;
                if frame_count % 30 == 0 {
                    vlog(&format!(
                        "read_loop: decoded {} frames, fb = {}x{}, framebuf_len = {}",
                        frame_count,
                        fb_width,
                        fb_height,
                        framebuf.len()
                    ));
                }
                // request next incremental update
                request_fb_update(writer)?;
            }
            MSG_BELL => {
                // no-op
            }
            MSG_SERVER_CUT_TEXT => {
                // RFB 3.8: type(1) + padding(3) + length(4) + text.
                let mut pad = [0u8; 3];
                read_exact(reader, &mut pad)?;
                let mut len_b = [0u8; 4];
                read_exact(reader, &mut len_b)?;
                let len = u32::from_be_bytes(len_b) as usize;
                let mut buf = vec![0u8; len];
                read_exact(reader, &mut buf)?;
                // clipboard: not wired yet
            }
            MSG_SET_COLOUR_MAP => {
                // RFB 3.8: type(1) + padding(1) + first(2) + n(2) + rgb(6 each).
                let mut pad = [0u8; 1];
                read_exact(reader, &mut pad)?;
                let mut first = [0u8; 2];
                read_exact(reader, &mut first)?;
                let mut n = [0u8; 2];
                read_exact(reader, &mut n)?;
                let n = u16::from_be_bytes(n);
                // skip rgb triplets (6 bytes each)
                let mut buf = vec![0u8; n as usize * 6];
                read_exact(reader, &mut buf)?;
            }
            other => {
                // Unknown message type (e.g. TigerVNC's EndOfContinuousUpdates
                // is 247/0xF7). We do NOT know its payload length, so we must
                // NOT read any further bytes — reading ahead would corrupt the
                // stream alignment and make every subsequent rect/message fail.
                // Just log and continue; the next message-type read will pick
                // up at the right byte (these pseudo-extension messages have
                // empty payloads in practice).
                vlog(&format!("read_loop: ignoring unknown server message type {other}"));
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Rectangle decoding
// ---------------------------------------------------------------------------

fn decode_rectangle(
    reader: &mut dyn Read,
    framebuf: &mut Vec<u8>,
    width: &mut u32,
    height: &mut u32,
) -> Result<(), String> {
    let mut hdr = [0u8; 12];
    read_exact(reader, &mut hdr)?;
    let x = u16::from_be_bytes([hdr[0], hdr[1]]) as u32;
    let y = u16::from_be_bytes([hdr[2], hdr[3]]) as u32;
    let w = u16::from_be_bytes([hdr[4], hdr[5]]) as u32;
    let h = u16::from_be_bytes([hdr[6], hdr[7]]) as u32;
    let enc = i32::from_be_bytes([hdr[8], hdr[9], hdr[10], hdr[11]]);

    // Desktop resize pseudo-encoding.
    if enc == ENCODING_FB_RESIZE {
        *width = w;
        *height = h;
        framebuf.resize((w * h * 4) as usize, 0);
        return Ok(());
    }

    if x + w > *width || y + h > *height {
        return Ok(()); // silently drop out-of-range rects
    }

    match enc {
        ENCODING_RAW => decode_raw(reader, framebuf, *width, *height, x, y, w, h),
        ENCODING_COPY_RECT => decode_copyrect(reader, framebuf, *width, *height, x, y, w, h),
        ENCODING_RRE => decode_rre(reader, framebuf, *width, *height, x, y, w, h),
        ENCODING_HEXTILE => decode_hextile(reader, framebuf, *width, *height, x, y, w, h),
        ENCODING_TIGHT => decode_tight(reader, framebuf, *width, *height, x, y, w, h),
        ENCODING_ZRLE => decode_zrle(reader, framebuf, *width, *height, x, y, w, h),
        other => Err(format!("unsupported encoding {other}")),
    }
}

fn pixel_at(buf: &[u8], stride: u32, x: u32, y: u32) -> [u8; 4] {
    let i = (y as usize * stride as usize + x as usize) * 4;
    [buf[i], buf[i + 1], buf[i + 2], buf[i + 3]]
}

fn put_pixel(buf: &mut [u8], stride: u32, x: u32, y: u32, px: [u8; 4]) {
    let i = (y as usize * stride as usize + x as usize) * 4;
    buf[i..i + 4].copy_from_slice(&px);
}

fn decode_raw(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    // 4 bytes per pixel (BGRA)
    let mut row = vec![0u8; (w * 4) as usize];
    for yy in 0..h {
        read_exact(reader, &mut row)?;
        let dst = ((y + yy) as usize * stride as usize + x as usize) * 4;
        framebuf[dst..dst + row.len()].copy_from_slice(&row);
    }
    Ok(())
}

fn decode_copyrect(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    let mut src = [0u8; 4];
    read_exact(reader, &mut src)?;
    let sx = u16::from_be_bytes([src[0], src[1]]) as u32;
    let sy = u16::from_be_bytes([src[2], src[3]]) as u32;
    // copy row by row to handle overlap correctly
    for yy in 0..h {
        let src_row = ((sy + yy) as usize) * (stride as usize) * 4 + (sx as usize) * 4;
        let dst_row = ((y + yy) as usize) * (stride as usize) * 4 + (x as usize) * 4;
        let len = (w as usize) * 4;
        let row = framebuf[src_row..src_row + len].to_vec();
        framebuf[dst_row..dst_row + len].copy_from_slice(&row);
    }
    Ok(())
}

fn decode_rre(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    let mut nsub = [0u8; 4];
    read_exact(reader, &mut nsub)?;
    let nsub = u32::from_be_bytes(nsub);
    // background pixel (4 bytes)
    let mut bg = [0u8; 4];
    read_exact(reader, &mut bg)?;
    for yy in 0..h {
        for xx in 0..w {
            put_pixel(framebuf, stride, x + xx, y + yy, bg);
        }
    }
    for _ in 0..nsub {
        let mut px = [0u8; 4];
        read_exact(reader, &mut px)?;
        let mut sub = [0u8; 8];
        read_exact(reader, &mut sub)?;
        let sx = u16::from_be_bytes([sub[0], sub[1]]) as u32;
        let sy = u16::from_be_bytes([sub[2], sub[3]]) as u32;
        let sw = u16::from_be_bytes([sub[4], sub[5]]) as u32;
        let sh = u16::from_be_bytes([sub[6], sub[7]]) as u32;
        // RRE sub-rectangle x/y are relative to the rectangle's top-left.
        for yy in 0..sh {
            for xx in 0..sw {
                put_pixel(framebuf, stride, x + sx + xx, y + sy + yy, px);
            }
        }
    }
    Ok(())
}

fn decode_hextile(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    // TigerVNC / RFB hextile subencoding bits (note: NOT the same layout as
    // the older "background/foreground" constants used elsewhere):
    //   bit0 (0x01) raw, bit1 (0x02) bg-specified, bit2 (0x04) fg-specified,
    //   bit3 (0x08) any-subrects, bit4 (0x10) subrects-coloured.
    const RAW: u8 = 0x01;
    const BG_SPECIFIED: u8 = 0x02;
    const FG_SPECIFIED: u8 = 0x04;
    const ANY_SUBRECTS: u8 = 0x08;
    const SUBRECTS_COLOURED: u8 = 0x10;

    let mut bg = [0u8; 4];
    let mut fg = [0u8; 4];

    // Tile size is 16x16, last tile in row/col is clipped.
    let mut tile_y = y;
    while tile_y < y + h {
        let tile_h = 16u32.min(y + h - tile_y);
        let mut tile_x = x;
        while tile_x < x + w {
            let tile_w = 16u32.min(x + w - tile_x);
            let mut subencoding = [0u8; 1];
            read_exact(reader, &mut subencoding)?;
            let se = subencoding[0];

            if se & RAW != 0 {
                // Raw tile: read tile_w * tile_h * 4 bytes verbatim.
                let n = (tile_w * tile_h * 4) as usize;
                let mut raw = vec![0u8; n];
                read_exact(reader, &mut raw)?;
                for yy in 0..tile_h {
                    let dst = ((tile_y + yy) as usize * stride as usize + tile_x as usize) * 4;
                    let src = (yy as usize) * (tile_w as usize) * 4;
                    let len = (tile_w as usize) * 4;
                    framebuf[dst..dst + len].copy_from_slice(&raw[src..src + len]);
                }
                tile_x += 16;
                continue;
            }

            if se & BG_SPECIFIED != 0 {
                let mut b = [0u8; 4];
                read_exact(reader, &mut b)?;
                bg = b;
            }
            if se & FG_SPECIFIED != 0 {
                let mut f = [0u8; 4];
                read_exact(reader, &mut f)?;
                fg = f;
            }

            // Fill the whole tile with the background colour first.
            for yy in 0..tile_h {
                for xx in 0..tile_w {
                    put_pixel(framebuf, stride, tile_x + xx, tile_y + yy, bg);
                }
            }

            if se & ANY_SUBRECTS != 0 {
                let mut nsub = [0u8; 1];
                read_exact(reader, &mut nsub)?;
                let nsub = nsub[0] as u32;
                let colour_flag = se & SUBRECTS_COLOURED != 0;
                for _ in 0..nsub {
                    let mut colour = [0u8; 4];
                    if colour_flag {
                        read_exact(reader, &mut colour)?;
                    } else {
                        colour = fg;
                    }
                    let mut sub = [0u8; 2];
                    read_exact(reader, &mut sub)?;
                    let sx = (sub[0] >> 4) as u32;
                    let sy = (sub[0] & 0x0F) as u32;
                    // width/height are stored as (n-1), so add 1 -> 1..16.
                    let sw = ((sub[1] >> 4) & 0x0F) as u32 + 1;
                    let sh = (sub[1] & 0x0F) as u32 + 1;
                    for yy in 0..sh {
                        for xx in 0..sw {
                            put_pixel(
                                framebuf,
                                stride,
                                tile_x + sx + xx,
                                tile_y + sy + yy,
                                colour,
                            );
                        }
                    }
                }
            }
            tile_x += 16;
        }
        tile_y += 16;
    }
    Ok(())
}

/// Read a Tight "compact" length (1-3 bytes). First byte's high bit 0 => 1 byte,
/// else 2 or 3 bytes (second byte high bit selects 3).
fn read_compact_len(reader: &mut dyn Read) -> Result<usize, String> {
    let mut b = [0u8; 1];
    read_exact(reader, &mut b)?;
    let first = b[0];
    if first & 0x80 == 0 {
        return Ok(first as usize);
    }
    let mut b2 = [0u8; 1];
    read_exact(reader, &mut b2)?;
    let second = b2[0];
    let mut len = ((first & 0x7F) as usize) << 8 | second as usize;
    if second & 0x80 != 0 {
        let mut b3 = [0u8; 1];
        read_exact(reader, &mut b3)?;
        len = ((len & 0x3FFF) << 8) | b3[0] as usize;
    }
    Ok(len)
}

fn decode_tight(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    // Tight: first byte is the compression-control byte. Low 4 bits = stream
    // type (0=copy, 1=zlib, 2=fill, 3=jpeg, 4=gradient). For zlib streams the
    // compact length follows. Tight pixels are 24-bit RGB (3 bytes).
    let mut control = [0u8; 1];
    read_exact(reader, &mut control)?;
    let c = control[0];
    let stream_type = c & 0x0F;

    match stream_type {
        0 => {
            // CopyFilter: raw pixels (3 bytes per pixel).
            let mut row = vec![0u8; (w * 3) as usize];
            for yy in 0..h {
                read_exact(reader, &mut row)?;
                for xx in 0..w {
                    let i = (xx as usize) * 3;
                    let dst = ((y + yy) as usize * stride as usize + (x + xx) as usize) * 4;
                    framebuf[dst] = row[i];
                    framebuf[dst + 1] = row[i + 1];
                    framebuf[dst + 2] = row[i + 2];
                    framebuf[dst + 3] = 0xFF;
                }
            }
        }
        1 => {
            // zlib stream; the decompressed payload starts with a filter id.
            let len = read_compact_len(reader)?;
            let mut data = vec![0u8; len];
            read_exact(reader, &mut data)?;
            let mut out = Vec::new();
            flate2::read::ZlibDecoder::new(data.as_slice())
                .read_to_end(&mut out)
                .map_err(|e| format!("tight inflate: {e}"))?;
            if out.is_empty() {
                return Ok(());
            }
            let filter = out[0];
            let pixels = &out[1..];
            match filter {
                0 => {
                    // CopyFilter: raw RGB triplets.
                    for yy in 0..h {
                        for xx in 0..w {
                            let i = (yy as usize * w as usize + xx as usize) * 3;
                            if i + 2 >= pixels.len() {
                                return Err("tight copy: truncated".to_string());
                            }
                            let dst = ((y + yy) as usize * stride as usize + (x + xx) as usize) * 4;
                            framebuf[dst] = pixels[i];
                            framebuf[dst + 1] = pixels[i + 1];
                            framebuf[dst + 2] = pixels[i + 2];
                            framebuf[dst + 3] = 0xFF;
                        }
                    }
                }
                1 => {
                    // PaletteFilter: palette size (1 byte); if >2 a bpp byte;
                    // then palette entries (3 bytes each) then indices.
                    if pixels.is_empty() {
                        return Err("tight palette: empty".to_string());
                    }
                    let pal_size = pixels[0] as usize;
                    let mut pos = 1usize;
                    let (bpp, index_bytes_per_px) = if pal_size <= 2 {
                        (8usize, 1usize)
                    } else {
                        if pos >= pixels.len() {
                            return Err("tight palette: missing bpp".to_string());
                        }
                        let b = pixels[pos] as usize;
                        pos += 1;
                        let per = if b == 0 { 0 } else if b == 1 { 1 } else if b <= 4 { 2 } else { 4 };
                        (b, per)
                    };
                    if pos + pal_size * 3 > pixels.len() {
                        return Err("tight palette: truncated palette".to_string());
                    }
                    let mut palette = Vec::with_capacity(pal_size);
                    for _ in 0..pal_size {
                        palette.push([pixels[pos], pixels[pos + 1], pixels[pos + 2], 0xFF]);
                        pos += 3;
                    }
                    let _ = bpp;
                    // Indices: one byte per pixel (or nibble for bpp=2 handled below).
                    for yy in 0..h {
                        for xx in 0..w {
                            let idx = yy as usize * w as usize + xx as usize;
                            let (byte_off, shift) = match index_bytes_per_px {
                                0 => (idx / 2, if idx % 2 == 0 { 4 } else { 0 }),
                                2 => (idx, 0), // 2-bit palettes: 4 px/byte handled as 1-byte idx (approx)
                                _ => (idx, 0),
                            };
                            if pos + byte_off >= pixels.len() {
                                return Err("tight palette: truncated indices".to_string());
                            }
                            let raw = pixels[pos + byte_off];
                            let pi = if index_bytes_per_px == 0 {
                                ((raw >> shift) & 0x0F) as usize
                            } else {
                                raw as usize
                            };
                            if pi >= palette.len() {
                                return Err("tight palette: index out of range".to_string());
                            }
                            let px = palette[pi];
                            let dst = ((y + yy) as usize * stride as usize + (x + xx) as usize) * 4;
                            framebuf[dst..dst + 4].copy_from_slice(&px);
                        }
                    }
                }
                _ => {
                    return Err(format!("tight filter {filter} not supported"));
                }
            }
        }
        2 => {
            // FillFilter: single RGB pixel.
            let mut px = [0u8; 3];
            read_exact(reader, &mut px)?;
            for yy in 0..h {
                for xx in 0..w {
                    let dst = ((y + yy) as usize * stride as usize + (x + xx) as usize) * 4;
                    framebuf[dst] = px[0];
                    framebuf[dst + 1] = px[1];
                    framebuf[dst + 2] = px[2];
                    framebuf[dst + 3] = 0xFF;
                }
            }
        }
        _ => {
            return Err(format!("tight stream type {stream_type} not supported"));
        }
    }
    Ok(())
}

fn decode_zrle(
    reader: &mut dyn Read,
    framebuf: &mut [u8],
    stride: u32,
    _height: u32,
    x: u32,
    y: u32,
    w: u32,
    h: u32,
) -> Result<(), String> {
    // ZRLE: after the 12-byte rect header comes a 4-byte length of the
    // zlib-compressed tile stream (64x64 tiles, per-tile subencoding).
    let mut len_b = [0u8; 4];
    read_exact(reader, &mut len_b)?;
    let compressed_len = u32::from_be_bytes(len_b) as usize;
    let mut data = vec![0u8; compressed_len];
    read_exact(reader, &mut data)?;
    let mut out = Vec::new();
    flate2::read::ZlibDecoder::new(data.as_slice())
        .read_to_end(&mut out)
        .map_err(|e| format!("zrle inflate: {e}"))?;

    let mut pos = 0usize;
    let mut tile_y = 0u32;
    while tile_y < h {
        let tile_h = 64u32.min(h - tile_y);
        let mut tile_x = 0u32;
        while tile_x < w {
            let tile_w = 64u32.min(w - tile_x);
            if pos >= out.len() {
                return Err("zrle: truncated stream".to_string());
            }
            let subenc = out[pos];
            pos += 1;
            match subenc {
                0 => {
                    // raw tile
                    let need = (tile_w * tile_h * 4) as usize;
                    if pos + need > out.len() {
                        return Err("zrle: truncated raw tile".to_string());
                    }
                    for yy in 0..tile_h {
                        let dst = ((y + tile_y + yy) as usize * stride as usize + (x + tile_x) as usize) * 4;
                        let src = pos + (yy as usize) * (tile_w as usize) * 4;
                        let len = (tile_w as usize) * 4;
                        framebuf[dst..dst + len].copy_from_slice(&out[src..src + len]);
                    }
                    pos += need;
                }
                1..=127 => {
                    // solid colour: palette index
                    let idx = subenc as usize;
                    // palette entries come after tile data; we can't easily
                    // interpret without the palette — bail.
                    return Err(format!("zrle palette tile {idx} not supported"));
                }
                128 => {
                    // plain solid colour: read 4 bytes
                    if pos + 4 > out.len() {
                        return Err("zrle: truncated solid".to_string());
                    }
                    let px = [out[pos], out[pos + 1], out[pos + 2], out[pos + 3]];
                    pos += 4;
                    for yy in 0..tile_h {
                        for xx in 0..tile_w {
                            put_pixel(framebuf, stride, x + tile_x + xx, y + tile_y + yy, px);
                        }
                    }
                }
                _ => {
                    return Err(format!("zrle subencoding {} not supported", subenc));
                }
            }
            tile_x += 64;
        }
        tile_y += 64;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::{Read, Write};
    use std::net::{TcpListener, TcpStream};

    /// NIST DES test vector: verifies the `des` crate behaves like standard DES
    /// (which VNC auth relies on).
    #[test]
    fn des_crate_nist_vector() {
        use des::cipher::generic_array::GenericArray;
        use des::cipher::{BlockEncrypt, KeyInit};
        let key = hex_decode("133457799BBCDFF1");
        let pt = hex_decode("0123456789ABCDEF");
        let mut block = GenericArray::clone_from_slice(&pt);
        let cipher = des::Des::new(GenericArray::from_slice(&key));
        cipher.encrypt_block(&mut block);
        let got: Vec<u8> = block.to_vec();
        assert_eq!(hex_encode(&got), "85E813540F0AB405");
    }

    fn hex_decode(s: &str) -> Vec<u8> {
        (0..s.len()).step_by(2).map(|i| u8::from_str_radix(&s[i..i + 2], 16).unwrap()).collect()
    }
    fn hex_encode(b: &[u8]) -> String {
        b.iter().map(|x| format!("{:02X}", x)).collect()
    }

    fn expect_bytes(r: &mut TcpStream, n: usize, what: &str) -> Vec<u8> {
        let mut buf = vec![0u8; n];
        let mut got = 0;
        while got < n {
            match r.read(&mut buf[got..]) {
                Ok(0) => panic!("mock server: EOF while reading {what}"),
                Ok(k) => got += k,
                Err(e) => panic!("mock server: read {what}: {e}"),
            }
        }
        buf
    }

    #[test]
    fn vnc_handshake_and_first_frame_protocol_bytes() {
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let addr = listener.local_addr().unwrap();

        let server = std::thread::spawn(move || {
            let (mut stream, _) = listener.accept().unwrap();
            let mut log: Vec<String> = Vec::new();
            let mut dbg = |what: String| log.push(what);

            // 1. version handshake: read client version, reply with 3.8.
            let v = expect_bytes(&mut stream, 12, "client version");
            dbg(format!("client version = {:?}", String::from_utf8_lossy(&v)));
            stream.write_all(b"RFB 003.008\n").unwrap();

            // 2. security types: offer [VNC Auth = 2].
            stream.write_all(b"\x01\x02").unwrap();
            let chosen = expect_bytes(&mut stream, 1, "chosen security");
            dbg(format!("client chose security type {}", chosen[0]));
            assert_eq!(chosen[0], 2);

            // 3. VNC Auth challenge.
            let challenge = [0x11u8; 16];
            stream.write_all(&challenge).unwrap();
            let resp = expect_bytes(&mut stream, 16, "vnc auth response");
            dbg(format!("vnc auth response = {resp:02X?}"));

            // 4. auth result OK, then SharedDesktop flag.
            stream.write_all(&[0u8; 4]).unwrap();
            let shared = expect_bytes(&mut stream, 1, "shared flag");
            dbg(format!("client shared flag = {}", shared[0]));

            // 5. ServerInit: 1920x1200.
            let mut si = Vec::new();
            si.extend_from_slice(&1920u16.to_be_bytes());
            si.extend_from_slice(&1200u16.to_be_bytes());
            si.extend_from_slice(&[32, 24, 0, 1, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 16, 8, 0, 0, 0, 0]);
            let name = b"mock tiger";
            si.extend_from_slice(&(name.len() as u32).to_be_bytes());
            si.extend_from_slice(name);
            stream.write_all(&si).unwrap();
            dbg("sent ServerInit".to_string());

            // 6. Read client messages until the full FBUpdateRequest.
            loop {
                let mt = expect_bytes(&mut stream, 1, "client message type");
                match mt[0] {
                    0 => {
                        let body = expect_bytes(&mut stream, 19, "SetPixelFormat body");
                        dbg(format!("client SetPixelFormat body = {body:02X?}"));
                    }
                    2 => {
                        let padc = expect_bytes(&mut stream, 3, "SetEncodings pad+count");
                        let n = u16::from_be_bytes([padc[1], padc[2]]) as usize;
                        let encs = expect_bytes(&mut stream, n * 4, "SetEncodings list");
                        dbg(format!("client SetEncodings ({n}) = {encs:02X?}"));
                    }
                    3 => {
                        let body = expect_bytes(&mut stream, 9, "FBUpdateRequest body");
                        let inc = body[0];
                        let x = u16::from_be_bytes([body[1], body[2]]);
                        let y = u16::from_be_bytes([body[3], body[4]]);
                        let w = u16::from_be_bytes([body[5], body[6]]);
                        let h = u16::from_be_bytes([body[7], body[8]]);
                        dbg(format!(
                            "client FBUpdateRequest incremental={inc} rect=({x},{y}) {w}x{h}"
                        ));
                        if inc == 0 {
                            break;
                        }
                    }
                    other => panic!("mock server: unexpected client msg type {other}"),
                }
            }

            // 7. Full-screen Raw FramebufferUpdate.
            let mut frm = Vec::new();
            frm.push(0u8);
            frm.push(0u8);
            frm.extend_from_slice(&1u16.to_be_bytes());
            frm.extend_from_slice(&0u16.to_be_bytes());
            frm.extend_from_slice(&0u16.to_be_bytes());
            frm.extend_from_slice(&1920u16.to_be_bytes());
            frm.extend_from_slice(&1200u16.to_be_bytes());
            frm.extend_from_slice(&0i32.to_be_bytes()); // Raw
            for i in 0..(1920 * 1200) {
                frm.extend_from_slice(&[
                    (i & 0xFF) as u8,
                    ((i >> 8) & 0xFF) as u8,
                    ((i >> 16) & 0xFF) as u8,
                    0xFF,
                ]);
            }
            stream.write_all(&frm).unwrap();
            dbg("sent FramebufferUpdate".to_string());

            // Keep the connection open briefly so the client can process the
            // frame, then close so the worker's read returns EOF and exits.
            std::thread::sleep(Duration::from_millis(300));
            drop(stream);
            dbg("mock server: closed connection".to_string());
            log
        });

        // Client: use the real public connect() API.
        let handle = connect(
            "127.0.0.1",
            addr.port(),
            Some("mypass"),
            Duration::from_secs(5),
        )
        .expect("client connect should return a handle");

        let server_log = server.join().unwrap();
        for line in &server_log {
            eprintln!("[mock-server] {line}");
        }

        eprintln!("[test] server joined, calling disconnect...");
        // Read session state without holding the global lock while we print.
        let mut w = 0u32;
        let mut h = 0u32;
        let mut flen = 0usize;
        let mut seq = 0u64;
        let mut closed = false;
        {
            let registry = sessions();
            let guard = registry.lock().unwrap();
            let s = guard.get(&handle).unwrap();
            w = *s.width.lock().unwrap();
            h = *s.height.lock().unwrap();
            flen = s.pixels.lock().unwrap().len();
            seq = *s.frame_seq.lock().unwrap();
            closed = s.closed.load(std::sync::atomic::Ordering::SeqCst);
        }
        eprintln!(
            "[test] connected={} width={w} height={h} framebuf_len={flen} frame_seq={seq} closed={closed}",
            true
        );

        assert_eq!(w, 1920);
        assert_eq!(h, 1200);
        assert_eq!(flen, 1920 * 1200 * 4, "framebuffer should be full");
        assert!(seq >= 1, "at least one frame decoded");

        eprintln!("[test] about to disconnect (join worker)...");
        disconnect(handle);
        eprintln!("[test] disconnect returned");
    }
}
