//! ssh_rust — 1Remote SSH core FFI (cdylib).
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

mod session;
mod log;

use std::ffi::{CStr, c_char};
use std::panic::{self, AssertUnwindSafe};
use std::sync::atomic::{AtomicI64, Ordering};
use std::time::Duration;

use session::{SshSession, sessions};

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
                sessions().lock().unwrap().insert(handle, session);
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
}
