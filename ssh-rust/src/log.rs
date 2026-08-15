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
    if !raw.is_null() {
        let cb: LogCallback = unsafe { std::mem::transmute(raw as *const std::ffi::c_void as *const ()) };
        // SAFETY: callback pointer came from set_callback, still valid (the C#
        // side keeps the delegate rooted for the process lifetime).
        if let Ok(c) = CString::new(msg) {
            cb(level, c.as_ptr());
        }
    }
}

/// Initialize a tracing subscriber that forwards events to the C# callback.
/// Called once; subsequent calls are ignored.
pub fn init() {
    static INIT: OnceLock<()> = OnceLock::new();
    let _ = INIT.get_or_init(|| {
        use tracing_subscriber::Layer;
        use tracing_subscriber::prelude::*;
        let layer = tracing_subscriber::fmt::layer()
            .with_writer(std::io::stderr)
            .with_target(false)
            .with_filter(tracing_subscriber::filter::LevelFilter::DEBUG);
        // Wrap so events ALSO forward to the C# callback.
        let layered = layer.and_then(ForwardLayer);
        let subscriber = tracing_subscriber::registry().with(layered);
        let _ = tracing::subscriber::set_global_default(subscriber);
    });
}

/// A tracing Layer that forwards each event to the C# log callback.
struct ForwardLayer;

impl<S> tracing_subscriber::Layer<S> for ForwardLayer
where
    S: tracing::Subscriber + for<'a> tracing_subscriber::registry::LookupSpan<'a>,
{
    fn on_event(
        &self,
        event: &tracing::Event<'_>,
        _ctx: tracing_subscriber::layer::Context<'_, S>,
    ) {
        let mut fields = Vec::new();
        let mut visitor = FieldVisitor { output: &mut fields };
        event.record(&mut visitor);
        let msg = if fields.is_empty() {
            String::new()
        } else {
            fields.join(" ")
        };
        let level = match *event.metadata().level() {
            tracing::Level::ERROR => LOG_ERROR,
            tracing::Level::WARN => LOG_WARN,
            tracing::Level::INFO => LOG_INFO,
            _ => LOG_DEBUG,
        };
        emit(level, &msg);
    }
}

struct FieldVisitor<'a> {
    output: &'a mut Vec<String>,
}

impl<'a> tracing::field::Visit for FieldVisitor<'a> {
    fn record_debug(&mut self, field: &tracing::field::Field, value: &dyn std::fmt::Debug) {
        self.output.push(format!("{}={:?}", field.name(), value));
    }
    fn record_str(&mut self, field: &tracing::field::Field, value: &str) {
        self.output.push(format!("{}={}", field.name(), value));
    }
    fn record_i64(&mut self, field: &tracing::field::Field, value: i64) {
        self.output.push(format!("{}={}", field.name(), value));
    }
    fn record_u64(&mut self, field: &tracing::field::Field, value: u64) {
        self.output.push(format!("{}={}", field.name(), value));
    }
    fn record_bool(&mut self, field: &tracing::field::Field, value: bool) {
        self.output.push(format!("{}={}", field.name(), value));
    }
}
