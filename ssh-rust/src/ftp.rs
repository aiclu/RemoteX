//! FTP/FTPS file-transfer session backed by the `suppaftp` crate.
//!
//! Synchronous, blocking session: file transfers are long-running operations
//! driven from C# through the FFI surface in `lib.rs`. Mirrors the behaviour of
//! the old FluentFTP transmitter:
//!   - FTPS explicit (AUTH TLS), TLS 1.2, **accept any certificate**,
//!   - list / exists / delete / mkdir / rename,
//!   - download / upload with an optional progress callback
//!     (`extern "C" fn(u64)`), cancellation via an atomic flag.

use std::collections::HashMap;
use std::io::{Read, Write};
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::sync::{Arc, Mutex};

use serde::Serialize;

/// Global FTP session registry keyed by `i64` handle.
pub fn ftp_sessions() -> &'static Arc<Mutex<HashMap<i64, FtpSession>>> {
    static SESSIONS: std::sync::OnceLock<Arc<Mutex<HashMap<i64, FtpSession>>>> = std::sync::OnceLock::new();
    SESSIONS.get_or_init(|| Arc::new(Mutex::new(HashMap::new())))
}

/// One connected FTP session (FTPS, native-tls stream).
pub struct FtpSession {
    client: Mutex<suppaftp::NativeTlsFtpStream>,
}

/// JSON representation of one remote item, mirroring `RemoteItem` in C#.
#[derive(Serialize)]
pub struct RemoteItemDto {
    pub name: String,
    pub full_name: String,
    pub is_directory: bool,
    pub is_symlink: bool,
    pub size: u64,
    /// Unix epoch seconds, 0 if unknown.
    pub last_update: i64,
}

static NEXT_FTP_HANDLE: AtomicI64 = AtomicI64::new(1_000_000_000);

/// Progress callback signature shared with C# (`extern "C" fn(transferred: u64)`).
pub type ProgressCb = extern "C" fn(u64);

/// Connect to an FTP/FTPS (explicit) server. Accepts any TLS certificate,
/// mirroring the old FluentFTP `ValidateCertificate` handler.
pub fn connect(host: &str, port: u16, username: &str, password: &str) -> Result<i64, String> {
    let mut client = suppaftp::NativeTlsFtpStream::connect((host, port))
        .map_err(|e| format!("connect {host}:{port}: {e}"))?;
    client
        .login(username, password)
        .map_err(|e| format!("login: {e}"))?;

    // FTPS explicit (AUTH TLS) with TLS 1.2, accepting any certificate.
    let mut tls = native_tls::TlsConnector::builder();
    tls.min_protocol_version(Some(native_tls::Protocol::Tlsv12));
    tls.danger_accept_invalid_certs(true);
    let connector = suppaftp::NativeTlsConnector::from(tls.build().map_err(|e| format!("tls: {e}"))?);
    client = client
        .into_secure(connector, host)
        .map_err(|e| format!("AUTH TLS: {e}"))?;

    let handle = NEXT_FTP_HANDLE.fetch_add(1, Ordering::SeqCst);
    ftp_sessions()
        .lock()
        .unwrap()
        .insert(handle, FtpSession { client: Mutex::new(client) });
    Ok(handle)
}

pub fn disconnect(handle: i64) {
    let mut sessions = ftp_sessions().lock().unwrap();
    if let Some(mut s) = sessions.remove(&handle) {
        if let Ok(mut c) = s.client.lock() {
            let _ = c.quit();
        }
    }
}

/// Minimal UNIX-style LIST parser (`-rw-r--r-- 1 owner group 123 Jan 01 12:34 name`).
/// Symlinks appear as `l` in the mode column. DOS-style listings are skipped.
fn parse_list_line(line: &str, base: &str) -> Option<RemoteItemDto> {
    let line = line.trim_end_matches('\r');
    if line.is_empty() || line.starts_with("total ") {
        return None;
    }
    // mode + nlink + owner + group + size + date + name
    let mut parts = line.split_whitespace();
    let mode = parts.next()?;
    parts.next()?; // nlink
    parts.next()?; // owner
    parts.next()?; // group
    let size: u64 = parts.next()?.parse().ok()?;
    // month day time/year
    let _m = parts.next()?;
    let _d = parts.next()?;
    let _t = parts.next()?;
    let name = parts.collect::<Vec<_>>().join(" ");
    if name.is_empty() || name == "." || name == ".." {
        return None;
    }
    let is_dir = mode.starts_with('d');
    let is_symlink = mode.starts_with('l');
    let full = if base.ends_with('/') || base.is_empty() {
        format!("{base}{name}")
    } else {
        format!("{base}/{name}")
    };
    Some(RemoteItemDto {
        name,
        full_name: full,
        is_directory: is_dir,
        is_symlink,
        size: if is_dir { 0 } else { size },
        last_update: 0,
    })
}

pub fn list(handle: i64, path: &str) -> Result<String, String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();
    let lines = client
        .list(Some(path))
        .map_err(|e| format!("list {path}: {e}"))?;
    let mut out: Vec<RemoteItemDto> = Vec::new();
    for l in lines {
        if let Some(item) = parse_list_line(&l, path) {
            out.push(item);
        }
    }
    out.sort_by(|a, b| a.name.cmp(&b.name));
    serde_json::to_string(&out).map_err(|e| format!("serialize: {e}"))
}

pub fn exists(handle: i64, path: &str) -> Result<bool, String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();
    // Files -> SIZE succeeds; directories -> CWD into it succeeds.
    if client.size(path).is_ok() {
        return Ok(true);
    }
    let cur = client.pwd().unwrap_or_default();
    let in_dir = client.cwd(path).is_ok();
    if !cur.is_empty() {
        let _ = client.cwd(cur);
    }
    Ok(in_dir)
}

pub fn delete(handle: i64, path: &str) -> Result<(), String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();
    client.rm(path).map_err(|e| format!("delete {path}: {e}"))
}

pub fn mkdir(handle: i64, path: &str) -> Result<(), String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();
    // If CWD into it fails, create it.
    let cur = client.pwd().unwrap_or_default();
    if client.cwd(path).is_err() {
        client.mkdir(path).map_err(|e| format!("mkdir {path}: {e}"))?;
    }
    if !cur.is_empty() {
        let _ = client.cwd(cur);
    }
    Ok(())
}

pub fn rename(handle: i64, path: &str, new_path: &str) -> Result<(), String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();
    client
        .rename(path, new_path)
        .map_err(|e| format!("rename {path} -> {new_path}: {e}"))
}

/// Download `remote_path` to `local_path` with progress + cancellation.
pub fn download(
    handle: i64,
    remote_path: &str,
    local_path: &str,
    progress: Option<ProgressCb>,
    cancel: Option<&AtomicBool>,
) -> Result<(), String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();

    let mut stream = client
        .retr_as_stream(remote_path)
        .map_err(|e| format!("retr {remote_path}: {e}"))?;
    let mut out = std::fs::File::create(local_path).map_err(|e| format!("create {local_path}: {e}"))?;
    let mut buf = [0u8; 65536];
    let mut transferred: u64 = 0;
    loop {
        if cancel.is_some_and(|c| c.load(Ordering::Relaxed)) {
            client.finalize_retr_stream(stream).ok();
            std::fs::remove_file(local_path).ok();
            return Err("cancelled".to_string());
        }
        let n = stream.read(&mut buf).map_err(|e| format!("read: {e}"))?;
        if n == 0 {
            break;
        }
        out.write_all(&buf[..n]).map_err(|e| format!("write: {e}"))?;
        transferred += n as u64;
        if let Some(cb) = progress {
            cb(transferred);
        }
    }
    out.flush().ok();
    client
        .finalize_retr_stream(stream)
        .map_err(|e| format!("finalize: {e}"))?;
    Ok(())
}

/// Upload `local_path` to `remote_path` with progress + cancellation.
/// Ensures the parent remote directory exists first.
pub fn upload(
    handle: i64,
    local_path: &str,
    remote_path: &str,
    progress: Option<ProgressCb>,
    cancel: Option<&AtomicBool>,
) -> Result<(), String> {
    let sessions = ftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let mut client = session.client.lock().unwrap();

    // Ensure parent dir exists.
    if let Some(idx) = remote_path.rfind('/') {
        let parent = &remote_path[..idx];
        if !parent.is_empty() {
            let cur = client.pwd().unwrap_or_default();
            if client.cwd(parent).is_err() {
                client.mkdir(parent).map_err(|e| format!("mkdir {parent}: {e}"))?;
            }
            if !cur.is_empty() {
                let _ = client.cwd(cur);
            }
        }
    }

    let mut file = std::fs::File::open(local_path).map_err(|e| format!("open {local_path}: {e}"))?;
    let mut stream = client
        .put_with_stream(remote_path)
        .map_err(|e| format!("stor {remote_path}: {e}"))?;
    let mut buf = [0u8; 65536];
    let mut transferred: u64 = 0;
    loop {
        if cancel.is_some_and(|c| c.load(Ordering::Relaxed)) {
            client.finalize_put_stream(stream).ok();
            return Err("cancelled".to_string());
        }
        let n = file.read(&mut buf).map_err(|e| format!("read: {e}"))?;
        if n == 0 {
            break;
        }
        stream.write_all(&buf[..n]).map_err(|e| format!("write: {e}"))?;
        transferred += n as u64;
        if let Some(cb) = progress {
            cb(transferred);
        }
    }
    client
        .finalize_put_stream(stream)
        .map_err(|e| format!("finalize: {e}"))?;
    Ok(())
}
