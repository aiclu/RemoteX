//! SFTP file-transfer session backed by `russh-sftp`.
//!
//! Each SFTP session owns its own SSH connection (reusing the same TOFU host-key
//! policy and auth logic as the terminal sessions) and a tokio runtime. All FFI
//! entry points block on that runtime via `block_on`; long transfers run on the
//! runtime's worker threads so the caller only blocks at the FFI boundary.

use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;

use serde::Serialize;
use tokio::io::{AsyncReadExt, AsyncWriteExt};

use crate::Handler;

/// Global SFTP session registry keyed by `i64` handle.
pub fn sftp_sessions() -> &'static Arc<Mutex<HashMap<i64, SftpSession>>> {
    static SESSIONS: OnceLock<Arc<Mutex<HashMap<i64, SftpSession>>>> = OnceLock::new();
    SESSIONS.get_or_init(|| Arc::new(Mutex::new(HashMap::new())))
}

/// One connected SFTP session.
pub struct SftpSession {
    runtime: Arc<tokio::runtime::Runtime>,
    sftp: Arc<russh_sftp::client::SftpSession>,
}

/// JSON representation of one remote item (shared shape with FTP).
#[derive(Serialize)]
pub struct SftpRemoteItemDto {
    pub name: String,
    pub full_name: String,
    pub is_directory: bool,
    pub is_symlink: bool,
    pub size: u64,
    /// Unix epoch seconds, 0 if unknown.
    pub last_update: i64,
}

static NEXT_SFTP_HANDLE: AtomicI64 = AtomicI64::new(2_000_000_000);

/// Progress callback signature shared with C# (`extern "C" fn(transferred: u64)`).
pub type ProgressCb = extern "C" fn(u64);

/// Connect to an SFTP server over a fresh SSH connection.
#[allow(clippy::too_many_arguments)]
pub fn connect(
    host: &str,
    port: u16,
    user: &str,
    password: Option<String>,
    key_path: Option<String>,
    timeout: Duration,
) -> Result<i64, String> {
    let runtime = tokio::runtime::Builder::new_multi_thread()
        .worker_threads(2)
        .enable_all()
        .build()
        .map_err(|e| format!("tokio runtime: {e}"))?;

    let connect_result = runtime.block_on(async {
        // Wrap the entire connect+auth+sftp-init in a single timeout so a hung
        // SFTP handshake (the "sftp init Timeout" failure mode reported by users
        // on slower / non-default-configured servers) cannot wedge the call.
        //
        // The inner async block is the unit of work, retryed up to `SFTP_CONNECT_ATTEMPTS`
        // times with a short back-off. The first attempt usually wins; retrying rescues
        // servers whose SFTP subsystem is slow to come up (e.g. systemd-forked
        // internal-sftp that needs ~2s of cold-start on the first connection).
        const SFTP_CONNECT_ATTEMPTS: u32 = 3;
        let mut last_err: Option<String> = None;
        for attempt in 1..=SFTP_CONNECT_ATTEMPTS {
            let attempt_result = tokio::time::timeout(timeout, async {
                let config = Arc::new(russh::client::Config {
                    inactivity_timeout: Some(Duration::from_secs(60 * 15)),
                    keepalive_interval: Some(Duration::from_secs(30)),
                    ..<_>::default()
                });
                let mut session = tokio::time::timeout(
                    Duration::from_secs(20),
                    russh::client::connect(config, (host, port), Handler {}),
                )
                .await
                .map_err(|_| format!("tcp connect to {host}:{port} timed out"))?
                .map_err(|e| format!("connect {host}:{port}: {e}"))?;

                // Authenticate.
                if let Some(pw) = &password {
                    session
                        .authenticate_password(user, pw)
                        .await
                        .map_err(|e| format!("auth: {e}"))?;
                } else if let Some(key_path) = &key_path {
                    let key = russh::keys::load_secret_key(key_path, None)
                        .map_err(|e| format!("load key {key_path}: {e}"))?;
                    let hash_alg = session
                        .best_supported_rsa_hash()
                        .await
                        .map_err(|e| format!("rsa hash: {e}"))?
                        .flatten();
                    session
                        .authenticate_publickey(user, russh::keys::PrivateKeyWithHashAlg::new(Arc::new(key), hash_alg))
                        .await
                        .map_err(|e| format!("auth publickey: {e}"))?;
                } else {
                    return Err("no password or key provided".to_string());
                }
                // Open a session channel, request the "sftp" subsystem and run
                // SFTP over it. NB: `request_subsystem` is mandatory — without it
                // the server never starts the SFTP subsystem and the version
                // exchange hangs until russh-sftp's internal request timeout.
                let channel = session
                    .channel_open_session()
                    .await
                    .map_err(|e| format!("channel: {e}"))?;
                channel
                    .request_subsystem(true, "sftp")
                    .await
                    .map_err(|e| format!("request sftp subsystem: {e}"))?;
                let stream = channel.into_stream();
                // `russh_sftp::client::SftpSession::new` carries an internal
                // timeout that is shorter than `timeout`. Treat any `Timeout`
                // error from it as transient and retry at the outer level.
                let sftp = match russh_sftp::client::SftpSession::new(stream).await {
                    Ok(s) => s,
                    Err(e) => {
                        let msg = e.to_string();
                        return Err(if msg.to_lowercase().contains("timeout") {
                            format!("sftp init: {msg} (attempt {attempt}/{SFTP_CONNECT_ATTEMPTS})")
                        } else {
                            format!("sftp init: {msg}")
                        });
                    }
                };
                Ok::<_, String>((session, sftp))
            })
            .await;

            match attempt_result {
                Ok(Ok(v)) => return Ok::<_, String>(v),
                Ok(Err(e)) => {
                    let is_timeout = e.to_lowercase().contains("timeout");
                    last_err = Some(e.clone());
                    if is_timeout && attempt < SFTP_CONNECT_ATTEMPTS {
                        eprintln!(
                            "[ssh_rust] sftp connect to {host}:{port} attempt {attempt} timed out, retrying in 2s: {e}"
                        );
                        tokio::time::sleep(Duration::from_secs(2)).await;
                        continue;
                    }
                    return Err(e);
                }
                Err(_) => {
                    last_err = Some(format!(
                        "sftp connect to {host}:{port} timed out after {}s (attempt {attempt}/{SFTP_CONNECT_ATTEMPTS}, check that the server allows the SFTP subsystem and is responsive)",
                        timeout.as_secs()
                    ));
                    if attempt < SFTP_CONNECT_ATTEMPTS {
                        eprintln!("[ssh_rust] {}", last_err.as_deref().unwrap_or(""));
                        tokio::time::sleep(Duration::from_secs(2)).await;
                        continue;
                    }
                    return Err(last_err.unwrap());
                }
            }
        }
        Err(last_err.unwrap_or_else(|| format!("sftp connect to {host}:{port} failed")))
    });

    let (session, sftp) = match connect_result {
        Ok(v) => v,
        Err(e) => return Err(e),
    };
    // Keep `session` alive for the connection lifetime.
    let _session = session;

    let handle = NEXT_SFTP_HANDLE.fetch_add(1, Ordering::SeqCst);
    sftp_sessions().lock().unwrap().insert(
        handle,
        SftpSession {
            runtime: Arc::new(runtime),
            sftp: Arc::new(sftp),
        },
    );
    Ok(handle)
}

pub fn disconnect(handle: i64) {
    sftp_sessions().lock().unwrap().remove(&handle);
}

pub fn list(handle: i64, path: &str) -> Result<String, String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let path = path.to_string();

    let items = runtime
        .block_on(async move {
            let mut out: Vec<SftpRemoteItemDto> = Vec::new();
            let mut entries = sftp.read_dir(&path).await.map_err(|e| format!("read_dir {path}: {e}"))?;
            for entry in entries.by_ref() {
                let name = entry.file_name();
                let md = entry.metadata();
                let is_dir = md.is_dir();
                let size = md.len();
                let is_symlink = md.is_symlink();
                let full = entry.path();
                out.push(SftpRemoteItemDto {
                    name,
                    full_name: full,
                    is_directory: is_dir,
                    is_symlink,
                    size: if is_dir { 0 } else { size },
                    last_update: md.modified().map(|t| t.duration_since(std::time::UNIX_EPOCH).map(|d| d.as_secs() as i64).unwrap_or(0)).unwrap_or(0),
                });
            }
            out.sort_by(|a, b| a.name.cmp(&b.name));
            serde_json::to_string(&out).map_err(|e| format!("serialize: {e}"))
        })
        .map_err(|e: String| e)?;
    Ok(items)
}

pub fn exists(handle: i64, path: &str) -> Result<bool, String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let path = path.to_string();

    Ok(runtime.block_on(async move { sftp.metadata(&path).await.is_ok() }))
}

pub fn delete(handle: i64, path: &str) -> Result<(), String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let path = path.to_string();

    runtime
        .block_on(async move {
            let md = sftp.metadata(&path).await.map_err(|e| format!("stat {path}: {e}"))?;
            if md.is_dir() {
                sftp.remove_dir(&path).await.map_err(|e| format!("rmdir {path}: {e}"))
            } else {
                sftp.remove_file(&path).await.map_err(|e| format!("rm {path}: {e}"))
            }
        })
        .map_err(|e: String| e)?;
    Ok(())
}

pub fn mkdir(handle: i64, path: &str) -> Result<(), String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let path = path.to_string();

    runtime
        .block_on(async move {
            if sftp.metadata(&path).await.is_err() {
                sftp.create_dir(&path).await.map_err(|e| format!("mkdir {path}: {e}"))?;
            }
            Ok::<_, String>(())
        })
        .map_err(|e: String| e)?;
    Ok(())
}

pub fn rename(handle: i64, path: &str, new_path: &str) -> Result<(), String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let path = path.to_string();
    let new_path = new_path.to_string();

    runtime
        .block_on(async move {
            sftp.rename(&path, &new_path).await.map_err(|e| format!("rename {path} -> {new_path}: {e}"))
        })
        .map_err(|e: String| e)?;
    Ok(())
}

/// Download a remote file to a local path with progress + cancellation.
pub fn download(
    handle: i64,
    remote_path: &str,
    local_path: &str,
    progress: Option<ProgressCb>,
    cancel: Option<&AtomicBool>,
) -> Result<(), String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let remote_path = remote_path.to_string();
    let local_path = local_path.to_string();
    let cancel_flag: Arc<AtomicBool> = match cancel {
        Some(c) => Arc::new(AtomicBool::new(c.load(Ordering::Relaxed))),
        None => Arc::new(AtomicBool::new(false)),
    };

    runtime
        .block_on(async move {
            let mut file = sftp.open(&remote_path).await.map_err(|e| format!("open {remote_path}: {e}"))?;
            let mut out = tokio::fs::File::create(&local_path).await.map_err(|e| format!("create {local_path}: {e}"))?;
            let mut buf = vec![0u8; 65536];
            let mut transferred: u64 = 0;
            loop {
                if cancel_flag.load(Ordering::Relaxed) {
                    tokio::fs::remove_file(&local_path).await.ok();
                    return Err("cancelled".to_string());
                }
                let n = file.read(&mut buf).await.map_err(|e| format!("read: {e}"))?;
                if n == 0 {
                    break;
                }
                out.write_all(&buf[..n]).await.map_err(|e| format!("write: {e}"))?;
                transferred += n as u64;
                if let Some(cb) = progress {
                    cb(transferred);
                }
            }
            out.flush().await.map_err(|e| format!("flush: {e}"))?;
            Ok::<_, String>(())
        })
        .map_err(|e: String| e)?;
    Ok(())
}

/// Upload a local file to a remote path with progress + cancellation.
/// Ensures the parent remote directory exists first.
pub fn upload(
    handle: i64,
    local_path: &str,
    remote_path: &str,
    progress: Option<ProgressCb>,
    cancel: Option<&AtomicBool>,
) -> Result<(), String> {
    let sessions = sftp_sessions().lock().unwrap();
    let session = sessions
        .get(&handle)
        .ok_or_else(|| "invalid handle".to_string())?;
    let sftp = session.sftp.clone();
    let runtime = session.runtime.clone();
    let local_path = local_path.to_string();
    let remote_path = remote_path.to_string();
    let cancel_flag: Arc<AtomicBool> = match cancel {
        Some(c) => Arc::new(AtomicBool::new(c.load(Ordering::Relaxed))),
        None => Arc::new(AtomicBool::new(false)),
    };

    runtime
        .block_on(async move {
            // Ensure parent dir exists.
            if let Some(idx) = remote_path.rfind('/') {
                let parent = remote_path[..idx].to_string();
                if !parent.is_empty() && sftp.metadata(&parent).await.is_err() {
                    sftp.create_dir(&parent).await.map_err(|e| format!("mkdir {parent}: {e}"))?;
                }
            }
            let mut file = tokio::fs::File::open(&local_path).await.map_err(|e| format!("open {local_path}: {e}"))?;
            let mut out = sftp.create(&remote_path).await.map_err(|e| format!("create {remote_path}: {e}"))?;
            let mut buf = vec![0u8; 65536];
            let mut transferred: u64 = 0;
            loop {
                if cancel_flag.load(Ordering::Relaxed) {
                    return Err("cancelled".to_string());
                }
                let n = file.read(&mut buf).await.map_err(|e| format!("read: {e}"))?;
                if n == 0 {
                    break;
                }
                out.write_all(&buf[..n]).await.map_err(|e| format!("write: {e}"))?;
                transferred += n as u64;
                if let Some(cb) = progress {
                    cb(transferred);
                }
            }
            out.flush().await.map_err(|e| format!("flush: {e}"))?;
            Ok::<_, String>(())
        })
        .map_err(|e: String| e)?;
    Ok(())
}
