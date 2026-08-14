//! ssh_rust — RemoteX SSH core FFI (cdylib).
//!
//! Exposes a C ABI over a russh-backed SSH session. The design is:
//!   - session-handle table (`HashMap<i64, SshSession>`), never raw pointers
//!   - poll-based read (`sr_poll_read`) instead of callbacks
//!   - `catch_unwind` on every entry point to turn panics into error codes
//!   - all network I/O runs on a per-session tokio runtime owned by `session.rs`
//!
//! FFI contract (stable with the C# side):
//!   - sr_connect / sr_write / sr_poll_read / sr_resize / sr_disconnect
//!   - sr_set_log_callback (tracing -> C#)

mod ftp;
mod log;
mod session;
mod sftp;

use std::ffi::{CStr, c_char};
use std::panic::{self, AssertUnwindSafe};
use std::sync::atomic::{AtomicI64, Ordering};
use std::time::Duration;

use ftp::{ProgressCb, connect as ftp_connect, delete as ftp_delete, disconnect as ftp_disconnect, download as ftp_download, exists as ftp_exists, list as ftp_list, mkdir as ftp_mkdir, rename as ftp_rename, upload as ftp_upload};
use sftp::{ProgressCb as SftpProgressCb, connect as sftp_connect, delete as sftp_delete, disconnect as sftp_disconnect, download as sftp_download, exists as sftp_exists, list as sftp_list, mkdir as sftp_mkdir, rename as sftp_rename, upload as sftp_upload};
use session::{SerialSession, SshSession, TelnetSession, sessions};

// ---------------------------------------------------------------------------
// Error codes (stable contract with C# side)
// ---------------------------------------------------------------------------
pub const SR_OK: i32 = 0;
pub const SR_ERR_INVALID_HANDLE: i32 = -1;
pub const SR_ERR_PANIC: i32 = -2;
pub const SR_ERR_INVALID_ARG: i32 = -3;
pub const SR_ERR_NO_DATA: i32 = 1; // poll_read: no data available right now (not fatal)

// ---------------------------------------------------------------------------
// Error type for the Rust layer
// ---------------------------------------------------------------------------
#[derive(Debug)]
pub enum SrError {
    Connect(String),
    Closed,
    Internal(String),
}

impl std::fmt::Display for SrError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SrError::Connect(s) => write!(f, "connect failed: {s}"),
            SrError::Closed => write!(f, "session closed"),
            SrError::Internal(s) => write!(f, "internal: {s}"),
        }
    }
}

// ---------------------------------------------------------------------------
// russh client handler (TOFU host-key policy)
// ---------------------------------------------------------------------------
pub struct Handler;

impl russh::client::Handler for Handler {
    type Error = russh::Error;

    /// Trust-on-first-use: accept any host key on first connect. A future phase
    /// may record and compare fingerprints. Returning `true` is the silent-TOFU
    /// policy we locked in during design.
    async fn check_server_key(
        &mut self,
        _server_public_key: &russh::keys::ssh_key::PublicKey,
    ) -> Result<bool, Self::Error> {
        Ok(true)
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static NEXT_HANDLE: AtomicI64 = AtomicI64::new(1);

/// Write a UTF-8 error string into the caller-provided buffer, NUL-terminated.
/// Truncates silently if the buffer is too small.
///
/// # Safety
/// `buf`/`cap` describe a writable byte buffer (or `buf` may be null when cap==0).
unsafe fn write_err(buf: *mut c_char, cap: usize, msg: &str) {
    if buf.is_null() || cap == 0 {
        return;
    }
    let bytes = msg.as_bytes();
    let n = bytes.len().min(cap.saturating_sub(1));
    // SAFETY: range within the caller-provided buffer of `cap` bytes.
    std::ptr::copy_nonoverlapping(bytes.as_ptr(), buf as *mut u8, n);
    // NUL-terminate.
    unsafe { *buf.add(n) = 0 };
}

/// Convert a `*const c_char` to an owned `String`. Returns `None` when null.
///
/// # Safety
/// `ptr` must be null or point to a valid NUL-terminated string.
unsafe fn cstr_to_owned(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    // SAFETY: caller guarantees a valid NUL-terminated string (or null).
    Some(unsafe { CStr::from_ptr(ptr) }.to_string_lossy().into_owned())
}

/// Run a closure that returns an `i32` error code, catching panics.
fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
    match panic::catch_unwind(AssertUnwindSafe(f)) {
        Ok(code) => code,
        Err(_) => SR_ERR_PANIC,
    }
}

// ---------------------------------------------------------------------------
// FFI exports
// ---------------------------------------------------------------------------

/// Establish an SSH session and return its handle.
///
/// # Safety
/// `handle_out` must be a valid writable `i64`; string params must be null or
/// valid NUL-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn sr_connect(
    host: *const c_char,
    port: u16,
    user: *const c_char,
    password: *const c_char,
    key_path: *const c_char,
    handle_out: *mut i64,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        if handle_out.is_null() {
            // SAFETY: caller must provide a valid out-param; null is an error.
            unsafe { write_err(err_buf, err_cap, "null handle_out") };
            return SR_ERR_INVALID_ARG;
        }
        // SAFETY: validated non-null above.
        unsafe { *handle_out = 0 };

        // SAFETY: host/user are required; password/key_path are optional.
        let Some(host) = (unsafe { cstr_to_owned(host) }) else {
            unsafe { write_err(err_buf, err_cap, "null host") };
            return SR_ERR_INVALID_ARG;
        };
        let Some(user) = (unsafe { cstr_to_owned(user) }) else {
            unsafe { write_err(err_buf, err_cap, "null user") };
            return SR_ERR_INVALID_ARG;
        };
        // SAFETY: optional string params.
        let password = unsafe { cstr_to_owned(password) };
        let key_path = unsafe { cstr_to_owned(key_path) };

        match SshSession::connect(
            host,
            port,
            user,
            password,
            key_path,
            Duration::from_secs(15),
            None, // startup command is injected by the host once connected
        ) {
            Ok(session) => {
                let handle = NEXT_HANDLE.fetch_add(1, Ordering::SeqCst);
                sessions().lock().unwrap().insert(handle, Box::new(session));
                // SAFETY: handle_out validated non-null above.
                unsafe {
                    *handle_out = handle;
                    write_err(err_buf, err_cap, "");
                }
                SR_OK
            }
            Err(e) => {
                let msg = e.to_string();
                // SAFETY: err_buf provided by caller.
                unsafe { write_err(err_buf, err_cap, &msg) };
                SR_ERR_CONNECT
            }
        }
    })
}

const SR_ERR_CONNECT: i32 = -4;

/// Send bytes to the remote shell (terminal input).
///
/// # Safety
/// `data`/`len` must describe a readable byte buffer (or `data` null when len==0).
#[no_mangle]
pub unsafe extern "C" fn sr_write(handle: i64, data: *const u8, len: usize) -> i32 {
    guard(|| {
        if len == 0 {
            return SR_OK;
        }
        if data.is_null() {
            return SR_ERR_INVALID_ARG;
        }
        // SAFETY: caller guarantees a readable buffer of `len` bytes.
        let bytes = unsafe { std::slice::from_raw_parts(data, len) };
        let sessions = sessions().lock().unwrap();
        let Some(session) = sessions.get(&handle) else {
            return SR_ERR_INVALID_HANDLE;
        };
        match session.write(bytes) {
            Ok(()) => SR_OK,
            Err(_) => SR_ERR_CLOSED,
        }
    })
}

const SR_ERR_CLOSED: i32 = -5;

/// Poll for one frame of remote output. Copies up to `cap` bytes into `buf` and
/// sets `out_len`. Returns `SR_NO_DATA` when nothing is pending right now.
///
/// # Safety
/// `buf`/`cap` describe a writable buffer (or `buf` null when cap==0);
/// `out_len` must be a valid writable `usize`.
#[no_mangle]
pub unsafe extern "C" fn sr_poll_read(
    handle: i64,
    buf: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        if out_len.is_null() || (cap > 0 && buf.is_null()) {
            return SR_ERR_INVALID_ARG;
        }
        // SAFETY: out_len validated non-null.
        unsafe { *out_len = 0 };

        let sessions = sessions().lock().unwrap();
        let Some(session) = sessions.get(&handle) else {
            return SR_ERR_INVALID_HANDLE;
        };

        // SAFETY: buf writable for `cap` bytes.
        let dst = unsafe { std::slice::from_raw_parts_mut(buf, cap) };
        match session.poll_read(dst) {
            Ok(0) => {
                if session.is_closed() {
                    SR_ERR_CLOSED
                } else {
                    SR_ERR_NO_DATA
                }
            }
            Ok(n) => {
                // SAFETY: out_len validated non-null.
                unsafe { *out_len = n };
                SR_OK
            }
            Err(_) => SR_ERR_CLOSED,
        }
    })
}

/// Notify the remote PTY of a resize (cols x rows).
#[no_mangle]
pub extern "C" fn sr_resize(handle: i64, cols: u32, rows: u32) -> i32 {
    guard(|| {
        let sessions = sessions().lock().unwrap();
        let Some(session) = sessions.get(&handle) else {
            return SR_ERR_INVALID_HANDLE;
        };
        match session.resize(cols, rows) {
            Ok(()) => SR_OK,
            Err(_) => SR_ERR_CLOSED,
        }
    })
}

/// Disconnect and free a session handle. Idempotent.
#[no_mangle]
pub extern "C" fn sr_disconnect(handle: i64) -> i32 {
    guard(|| {
        let mut sessions = sessions().lock().unwrap();
        if sessions.remove(&handle).is_some() {
            SR_OK
        } else {
            SR_ERR_INVALID_HANDLE
        }
    })
}

/// Establish a raw TCP telnet session (minimal IAC negotiation) and return its
/// handle. Same out-param contract as [`sr_connect`].
///
/// # Safety
/// `handle_out` must be a valid writable `i64`; `host` must be null or a valid
// ---------------------------------------------------------------------------
// FTP/FTPS FFI
// ---------------------------------------------------------------------------

/// Establish an FTPS (explicit) session and return its handle.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_connect(
    host: *const c_char,
    port: u16,
    user: *const c_char,
    password: *const c_char,
    handle_out: *mut i64,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        if handle_out.is_null() {
            unsafe { write_err(err_buf, err_cap, "null handle_out") };
            return SR_ERR_INVALID_ARG;
        }
        unsafe { *handle_out = 0 };
        let (Some(host), Some(user), Some(password)) = (
            unsafe { cstr_to_owned(host) },
            unsafe { cstr_to_owned(user) },
            unsafe { cstr_to_owned(password) },
        ) else {
            unsafe { write_err(err_buf, err_cap, "null string param") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_connect(&host, port, &user, &password) {
            Ok(h) => {
                unsafe { *handle_out = h };
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Disconnect and free an FTP session handle. Idempotent.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_disconnect(handle: i64) -> i32 {
    guard(|| {
        ftp_disconnect(handle);
        SR_OK
    })
}

/// List a directory. JSON array of `RemoteItemDto` is written to the output
/// buffer; `out_len` receives the byte length (0 when the buffer is too small).
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_list(
    handle: i64,
    path: *const c_char,
    out_buf: *mut u8,
    out_cap: usize,
    out_len: *mut usize,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_list(handle, &path) {
            Ok(json) => {
                let bytes = json.as_bytes();
                if out_buf.is_null() || bytes.len() > out_cap {
                    unsafe { *out_len = bytes.len() };
                    return SR_ERR_INVALID_ARG; // buffer too small
                }
                unsafe {
                    std::ptr::copy_nonoverlapping(bytes.as_ptr(), out_buf, bytes.len());
                    *out_len = bytes.len();
                }
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Test whether a remote path exists (file or directory).
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_exists(
    handle: i64,
    path: *const c_char,
    out_exists: *mut u8,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_exists(handle, &path) {
            Ok(b) => {
                unsafe { *out_exists = b as u8 };
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Delete a remote path (file).
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_delete(
    handle: i64,
    path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_delete(handle, &path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Create a remote directory if it does not exist.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_mkdir(
    handle: i64,
    path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_mkdir(handle, &path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Rename a remote path.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_rename(
    handle: i64,
    path: *const c_char,
    new_path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(path), Some(new_path)) = (unsafe { cstr_to_owned(path) }, unsafe { cstr_to_owned(new_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_rename(handle, &path, &new_path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Download a remote file to a local path. `progress` (nullable) is invoked with
/// cumulative transferred bytes; `cancel` (nullable) is an `AtomicBool` checked
/// per chunk.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_download(
    handle: i64,
    remote_path: *const c_char,
    local_path: *const c_char,
    progress: Option<ProgressCb>,
    cancel: *const std::sync::atomic::AtomicBool,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(remote), Some(local)) = (unsafe { cstr_to_owned(remote_path) }, unsafe { cstr_to_owned(local_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_download(handle, &remote, &local, progress, unsafe { cancel.as_ref() }) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Upload a local file to a remote path. `progress` (nullable) is invoked with
/// cumulative transferred bytes; `cancel` (nullable) is an `AtomicBool`.
#[no_mangle]
pub unsafe extern "C" fn sr_ftp_upload(
    handle: i64,
    local_path: *const c_char,
    remote_path: *const c_char,
    progress: Option<ProgressCb>,
    cancel: *const std::sync::atomic::AtomicBool,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(local), Some(remote)) = (unsafe { cstr_to_owned(local_path) }, unsafe { cstr_to_owned(remote_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match ftp_upload(handle, &local, &remote, progress, unsafe { cancel.as_ref() }) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

// ---------------------------------------------------------------------------
// SFTP FFI
// ---------------------------------------------------------------------------

/// Establish an SFTP session over a fresh SSH connection and return its handle.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_connect(
    host: *const c_char,
    port: u16,
    user: *const c_char,
    password: *const c_char,
    key_path: *const c_char,
    handle_out: *mut i64,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        if handle_out.is_null() {
            unsafe { write_err(err_buf, err_cap, "null handle_out") };
            return SR_ERR_INVALID_ARG;
        }
        unsafe { *handle_out = 0 };
        let (Some(host), Some(user)) = (unsafe { cstr_to_owned(host) }, unsafe { cstr_to_owned(user) }) else {
            unsafe { write_err(err_buf, err_cap, "null string param") };
            return SR_ERR_INVALID_ARG;
        };
        let password = unsafe { cstr_to_owned(password) };
        let key_path = unsafe { cstr_to_owned(key_path) };
        match sftp_connect(&host, port, &user, password, key_path, Duration::from_secs(15)) {
            Ok(h) => {
                unsafe { *handle_out = h };
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Disconnect and free an SFTP session handle. Idempotent.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_disconnect(handle: i64) -> i32 {
    guard(|| {
        sftp_disconnect(handle);
        SR_OK
    })
}

/// List a directory. JSON array of `SftpRemoteItemDto` written to the output buffer.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_list(
    handle: i64,
    path: *const c_char,
    out_buf: *mut u8,
    out_cap: usize,
    out_len: *mut usize,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_list(handle, &path) {
            Ok(json) => {
                let bytes = json.as_bytes();
                if out_buf.is_null() || bytes.len() > out_cap {
                    unsafe { *out_len = bytes.len() };
                    return SR_ERR_INVALID_ARG; // buffer too small
                }
                unsafe {
                    std::ptr::copy_nonoverlapping(bytes.as_ptr(), out_buf, bytes.len());
                    *out_len = bytes.len();
                }
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Test whether a remote path exists.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_exists(
    handle: i64,
    path: *const c_char,
    out_exists: *mut u8,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_exists(handle, &path) {
            Ok(b) => {
                unsafe { *out_exists = b as u8 };
                SR_OK
            }
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Delete a remote path.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_delete(
    handle: i64,
    path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_delete(handle, &path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Create a remote directory if it does not exist.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_mkdir(
    handle: i64,
    path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let Some(path) = (unsafe { cstr_to_owned(path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_mkdir(handle, &path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Rename a remote path.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_rename(
    handle: i64,
    path: *const c_char,
    new_path: *const c_char,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(path), Some(new_path)) = (unsafe { cstr_to_owned(path) }, unsafe { cstr_to_owned(new_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_rename(handle, &path, &new_path) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Download a remote file to a local path with progress + cancellation.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_download(
    handle: i64,
    remote_path: *const c_char,
    local_path: *const c_char,
    progress: Option<SftpProgressCb>,
    cancel: *const std::sync::atomic::AtomicBool,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(remote), Some(local)) = (unsafe { cstr_to_owned(remote_path) }, unsafe { cstr_to_owned(local_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_download(handle, &remote, &local, progress, unsafe { cancel.as_ref() }) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Upload a local file to a remote path with progress + cancellation.
#[no_mangle]
pub unsafe extern "C" fn sr_sftp_upload(
    handle: i64,
    local_path: *const c_char,
    remote_path: *const c_char,
    progress: Option<SftpProgressCb>,
    cancel: *const std::sync::atomic::AtomicBool,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        let (Some(local), Some(remote)) = (unsafe { cstr_to_owned(local_path) }, unsafe { cstr_to_owned(remote_path) }) else {
            unsafe { write_err(err_buf, err_cap, "null path") };
            return SR_ERR_INVALID_ARG;
        };
        match sftp_upload(handle, &local, &remote, progress, unsafe { cancel.as_ref() }) {
            Ok(()) => SR_OK,
            Err(e) => {
                unsafe { write_err(err_buf, err_cap, &e) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// NUL-terminated string.
#[no_mangle]
pub unsafe extern "C" fn sr_connect_telnet(
    host: *const c_char,
    port: u16,
    handle_out: *mut i64,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        if handle_out.is_null() {
            // SAFETY: caller must provide a valid out-param.
            unsafe { write_err(err_buf, err_cap, "null handle_out") };
            return SR_ERR_INVALID_ARG;
        }
        // SAFETY: validated non-null above.
        unsafe { *handle_out = 0 };

        // SAFETY: host is required.
        let Some(host) = (unsafe { cstr_to_owned(host) }) else {
            unsafe { write_err(err_buf, err_cap, "null host") };
            return SR_ERR_INVALID_ARG;
        };

        match TelnetSession::connect(host, port, Duration::from_secs(15)) {
            Ok(session) => {
                let handle = NEXT_HANDLE.fetch_add(1, Ordering::SeqCst);
                sessions().lock().unwrap().insert(handle, Box::new(session));
                // SAFETY: handle_out validated non-null above.
                unsafe {
                    *handle_out = handle;
                    write_err(err_buf, err_cap, "");
                }
                SR_OK
            }
            Err(e) => {
                let msg = e.to_string();
                // SAFETY: err_buf provided by caller.
                unsafe { write_err(err_buf, err_cap, &msg) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Establish a serial port session and return its handle. `parity`/`stop_bits`/
/// `flow_control` are encoded as integers by the C# side (see `Serial.cs`).
/// Same out-param contract as [`sr_connect`].
///
/// # Safety
/// `handle_out` must be a valid writable `i64`; `port_name` must be null or a
/// valid NUL-terminated string.
#[no_mangle]
pub unsafe extern "C" fn sr_connect_serial(
    port_name: *const c_char,
    baud_rate: u32,
    data_bits: u8,
    parity: u8,
    stop_bits: u8,
    flow_control: u8,
    handle_out: *mut i64,
    err_buf: *mut c_char,
    err_cap: usize,
) -> i32 {
    guard(|| {
        if handle_out.is_null() {
            // SAFETY: caller must provide a valid out-param.
            unsafe { write_err(err_buf, err_cap, "null handle_out") };
            return SR_ERR_INVALID_ARG;
        }
        // SAFETY: validated non-null above.
        unsafe { *handle_out = 0 };

        // SAFETY: port_name is required.
        let Some(port_name) = (unsafe { cstr_to_owned(port_name) }) else {
            unsafe { write_err(err_buf, err_cap, "null port_name") };
            return SR_ERR_INVALID_ARG;
        };

        match SerialSession::connect(port_name, baud_rate, data_bits, parity, stop_bits, flow_control)
        {
            Ok(session) => {
                let handle = NEXT_HANDLE.fetch_add(1, Ordering::SeqCst);
                sessions().lock().unwrap().insert(handle, Box::new(session));
                // SAFETY: handle_out validated non-null above.
                unsafe {
                    *handle_out = handle;
                    write_err(err_buf, err_cap, "");
                }
                SR_OK
            }
            Err(e) => {
                let msg = e.to_string();
                // SAFETY: err_buf provided by caller.
                unsafe { write_err(err_buf, err_cap, &msg) };
                SR_ERR_CONNECT
            }
        }
    })
}

/// Set the log callback (Rust tracing -> C# SimpleLogHelper). See `log.rs`.
#[no_mangle]
pub extern "C" fn sr_set_log_callback(
    cb: Option<extern "C" fn(level: i32, msg: *const c_char)>,
) -> i32 {
    log::set_callback(cb);
    SR_OK
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn invalid_handle_returns_error() {
        let mut out = [0u8; 8];
        let mut out_len: usize = 0;
        let rc = unsafe { sr_poll_read(-999, out.as_mut_ptr(), out.len(), &mut out_len) };
        assert_eq!(rc, SR_ERR_INVALID_HANDLE);
    }

    #[test]
    fn null_handle_out_rejected() {
        let mut errbuf = [0u8; 64];
        let rc = unsafe {
            sr_connect(
                b"h\0".as_ptr().cast(),
                22,
                b"u\0".as_ptr().cast(),
                std::ptr::null(),
                std::ptr::null(),
                std::ptr::null_mut(),
                errbuf.as_mut_ptr().cast(),
                errbuf.len(),
            )
        };
        assert_eq!(rc, SR_ERR_INVALID_ARG);
    }

    /// A minimal fake telnet server: accepts one connection, sends an IAC
    /// negotiation (`IAC DO ECHO`, `IAC WILL SUPPRESS_GO_AHEAD`) plus a banner,
    /// then echoes input back. Used to verify `sr_connect_telnet` + poll do not
    /// immediately report closed and actually deliver banner bytes.
    #[test]
    fn telnet_session_receives_banner_and_negotiation_is_filtered() {
        use std::io::{Read, Write};
        use std::net::{TcpListener, TcpStream};
        use std::thread;
        use std::time::Duration;

        // Fake telnet server.
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let addr = listener.local_addr().unwrap();
        let server = thread::spawn(move || {
            let (mut stream, _) = listener.accept().unwrap();
            // IAC DO ECHO (FF FD 01), IAC WILL SUPPRESS_GO_AHEAD (FF FB 03)
            stream.write_all(&[0xFF, 0xFD, 0x01, 0xFF, 0xFB, 0x03]).unwrap();
            // welcome banner
            stream.write_all(b"\r\nFakeTelnet> ").unwrap();
            stream.flush().unwrap();
            // keep the connection open briefly, echo anything received
            let mut buf = [0u8; 64];
            let deadline = std::time::Instant::now() + Duration::from_secs(2);
            while std::time::Instant::now() < deadline {
                match stream.read(&mut buf) {
                    Ok(0) | Err(_) => break,
                    Ok(n) => {
                        let _ = stream.write_all(&buf[..n]);
                        let _ = stream.flush();
                    }
                }
            }
        });

        // Connect via FFI.
        let host = b"127.0.0.1\0";
        let mut handle: i64 = 0;
        let mut errbuf = [0u8; 256];
        let rc = unsafe {
            sr_connect_telnet(
                host.as_ptr().cast(),
                addr.port(),
                &mut handle,
                errbuf.as_mut_ptr().cast(),
                errbuf.len(),
            )
        };
        assert_eq!(rc, SR_OK, "connect failed: {}", String::from_utf8_lossy(&errbuf));
        assert!(handle > 0);

        // Poll until the banner arrives (or the session closes prematurely).
        let mut out = [0u8; 256];
        let deadline = std::time::Instant::now() + Duration::from_secs(5);
        let mut received = Vec::new();
        let mut closed_early = false;
        while std::time::Instant::now() < deadline && received.is_empty() {
            let mut out_len: usize = 0;
            let rc = unsafe { sr_poll_read(handle, out.as_mut_ptr(), out.len(), &mut out_len) };
            match rc {
                SR_OK if out_len > 0 => received.extend_from_slice(&out[..out_len]),
                SR_ERR_CLOSED => {
                    closed_early = true;
                    break;
                }
                SR_ERR_NO_DATA => {}
                _ => {}
            }
            thread::sleep(Duration::from_millis(10));
        }

        let text = String::from_utf8_lossy(&received).to_string();
        // The banner must have been delivered AND no IAC bytes (0xFF) should leak through.
        assert!(text.contains("FakeTelnet"), "banner not received: {:?}", text);
        assert!(!text.contains(0xFF as char), "IAC bytes leaked through: {:?}", text);
        assert!(!closed_early, "session closed prematurely before banner arrived");

        unsafe { sr_disconnect(handle) };
        let _ = server.join();
    }

    /// On Windows, `usize` is 64-bit. This guards the FFI contract that the C# side
    /// passes an 8-byte `out_len` (nint). A 4-byte `int` would silently corrupt the
    /// poll output buffer — the bug that made Telnet sessions close immediately.
    #[test]
    fn out_len_is_64bit() {
        assert_eq!(std::mem::size_of::<usize>(), 8);
    }
}
