#if RUST_SSH
using System;
using System.Runtime.InteropServices;

namespace _1RM.Service.RustSsh
{
    /// <summary>
    /// P/Invoke declarations for the Rust SSH core (<c>ssh_rust.dll</c>, a cdylib).
    ///
    /// Only compiled into net9.0-windows builds (the sole target that carries the
    /// Rust-SSH terminal). See the conditional ItemGroup in Ui.csproj.
    ///
    /// FFI contract mirrored from <c>ssh-rust/src/lib.rs</c>. Keep the two sides
    /// in sync when either changes.
    /// </summary>
    internal static partial class SshRustNative
    {
        internal const string DllName = "ssh_rust";

        // Error codes (must match SR_* constants in Rust).
        internal const int SR_OK = 0;
        internal const int SR_ERR_INVALID_HANDLE = -1;
        internal const int SR_ERR_PANIC = -2;
        internal const int SR_ERR_INVALID_ARG = -3;
        internal const int SR_ERR_CONNECT = -4;
        internal const int SR_ERR_CLOSED = -5;
        internal const int SR_ERR_NO_DATA = 1; // poll_read: nothing available right now

        /// <summary>
        /// Allocate a new session handle.
        /// </summary>
        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_connect(
            string host,
            ushort port,
            string user,
            string? password,
            string? keyPath,
            out long handle,
            [Out] byte[] errBuf,
            int errCap);

        /// <summary>
        /// Send bytes to the remote (terminal input).
        /// </summary>
        [LibraryImport(DllName)]
        internal static partial int sr_write(
            long handle,
            [In] byte[] data,
            int len);

        /// <summary>
        /// Poll for one frame of remote output.
        /// </summary>
        [LibraryImport(DllName)]
        internal static partial int sr_poll_read(
            long handle,
            [Out] byte[] buf,
            int cap,
            out int outLen);

        /// <summary>
        /// Notify the remote PTY of a resize.
        /// </summary>
        [LibraryImport(DllName)]
        internal static partial int sr_resize(long handle, uint cols, uint rows);

        /// <summary>
        /// Disconnect and free a session handle. Idempotent.
        /// </summary>
        [LibraryImport(DllName)]
        internal static partial int sr_disconnect(long handle);
    }
}
#endif
