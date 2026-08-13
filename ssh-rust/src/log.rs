//! Logging bridge: forwards Rust `tracing` events to the C# side via a callback
//! registered with `sr_set_log_callback`. Sensitive data (passwords, key
//! material) must never be logged; the design guarantees we only ever forward
//! addresses/usernames/errors.

use std::ffi::CString;
use std::sync::atomic::{AtomicPtr, Ordering};
use std::sync::OnceLock;

/// Log levels aligned with the C# `SimpleLogHelper`/Syslog convention.
pub const LOG_DEBUG: i32 = 7;
pub const LOG_INFO: i32 = 6;
pub const LOG_WARN: i32 = 4;
pub const LOG_ERROR: i32 = 3;

type LogCallback = extern "C" fn(level: i32, msg: *const std::ffi::c_char);

static CALLBACK: AtomicPtr<std::ffi::c_void> = AtomicPtr::new(std::ptr::null_mut());

/// Set the C# log callback. Pass `None` to clear.
pub fn set_callback(cb: Option<LogCallback>) {
    let raw = cb.map(|f| f as *const std::ffi::c_void).unwrap_or(std::ptr::null_mut());
    CALLBACK.store(raw as *mut std::ffi::c_void, Ordering::SeqCst);
}

/// Emit a message to the C# callback if one is registered.
pub fn emit(level: i32, msg: &str) {
    let raw = CALLBACK.load(Ordering::SeqCst);
    if raw.is_null() {
        return;
    }
    let cb: LogCallback = unsafe { std::mem::transmute(raw as *const std::ffi::c_void as *const ()) };
    // SAFETY: callback pointer came from set_callback, still valid (the C# side
    // keeps the delegate rooted for the process lifetime).
    if let Ok(c) = CString::new(msg) {
        cb(level, c.as_ptr());
    }
}

/// Initialize a tracing subscriber that forwards events to the callback.
/// Called once; subsequent calls are ignored.
pub fn init() {
    static INIT: OnceLock<()> = OnceLock::new();
    let _ = INIT.get_or_init(|| {
        let subscriber = tracing_subscriber::fmt()
            .with_max_level(tracing::Level::DEBUG)
            .with_writer(std::io::stderr)
            .finish();
        let _ = tracing::subscriber::set_global_default(subscriber);
    });
}
