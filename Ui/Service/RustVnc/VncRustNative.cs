using System;
using System.Runtime.InteropServices;

namespace _1RM.Service.RustVnc;

/// <summary>
/// P/Invoke surface for the Rust RFB (VNC) client in <c>ssh_rust.dll</c>.
/// Return codes mirror the other Rust FFI modules (SR_OK=0, negative = error).
/// VNC session handles live in their own address space (3_000_000_000+).
/// </summary>
internal static partial class VncRustNative
{
    public const int SR_OK = 0;
    public const int SR_ERR_INVALID_HANDLE = -1;
    public const int SR_ERR_INVALID_ARG = -3;
    public const int SR_ERR_CONNECT = -4;
    public const int SR_ERR_CLOSED = -5;

    [LibraryImport("ssh_rust", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int sr_vnc_connect(string host, ushort port, string? password, ulong timeout_secs, out long handle, byte[] err_buf, int err_cap);

    [LibraryImport("ssh_rust", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int sr_vnc_poll(long handle, byte[] err_buf, int err_cap);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_get_size(long handle, out uint w, out uint h);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_get_frame(long handle, byte[] buf, int cap, out int out_len);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_frame_seq(long handle, out ulong seq);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_send_pointer(long handle, ushort x, ushort y, byte buttons);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_send_key(long handle, uint keysym, [MarshalAs(UnmanagedType.I1)] bool down);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_request_update(long handle);

    [LibraryImport("ssh_rust")]
    internal static partial int sr_vnc_disconnect(long handle);
}
