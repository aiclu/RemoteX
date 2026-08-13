#if RUST_SSH
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shawn.Utils;

namespace _1RM.Service.RustSsh
{
    /// <summary>
    /// Managed façade over the Rust SSH core (<c>ssh_rust.dll</c>).
    ///
    /// Owns the session-handle lifecycle and a background poll thread that drains
    /// remote output via <c>sr_poll_read</c> and forwards it to <see cref="OnData"/>.
    /// Consumed by <c>RustSshHost</c> (the WPF terminal host).
    ///
    /// Only compiled into net9.0-windows builds.
    /// </summary>
    internal sealed class SshRustBridge : IDisposable
    {
        // Dispatch of received data: raised on the poll thread; subscribers must marshal
        // to the UI thread themselves (the terminal host does so via the WPF Dispatcher).
        public event Action<byte[]>? OnData;
        // Raised once when the remote side closes the session (EOF or disconnect).
        public event Action? OnClosed;
        // Raised on fatal internal errors (e.g. Rust panic). Carries a human-readable message.
        public event Action<string>? OnError;

        private const int PollChunkSize = 64 * 1024; // 64 KiB per poll frame
        private const int PollIntervalMs = 10;

        private readonly object _gate = new();
        private long _handle;
        private volatile bool _disposed;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;

        public bool IsConnected => !_disposed && _handle != 0;

        /// <summary>
        /// Establish a session. <paramref name="password"/> and <paramref name="keyPath"/>
        /// are mutually exclusive; pass null for whichever is not used.
        /// </summary>
        /// <returns>null on success, otherwise a human-readable error message.</returns>
        public string? Connect(
            string host,
            ushort port,
            string user,
            string? password,
            string? keyPath,
            int connectTimeoutMs = 10000)
        {
            if (_disposed) return "bridge is disposed";

            // Rust cdylib is x64-only for now.
            if (Environment.Is64BitProcess == false)
            {
                return "Rust SSH core is not available on 32-bit processes.";
            }

            var errBuf = new byte[1024];
            var rc = SshRustNative.sr_connect(host, port, user, password, keyPath, out var handle, errBuf, errBuf.Length);
            if (rc != SshRustNative.SR_OK)
            {
                return ErrorText(rc, errBuf);
            }

            _handle = handle;
            _cts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_cts.Token));
            return null;
        }

        /// <summary>
        /// Send terminal input bytes to the remote.
        /// </summary>
        public void Write(byte[] data)
        {
            if (_disposed || _handle == 0) return;
            lock (_gate)
            {
                if (_handle == 0) return;
                var rc = SshRustNative.sr_write(_handle, data, data.Length);
                if (rc != SshRustNative.SR_OK)
                {
                    SimpleLogHelper.Debug($"sr_write failed: {ErrorText(rc, null)}");
                }
            }
        }

        /// <summary>
        /// Notify the remote PTY of a resize.
        /// </summary>
        public void Resize(uint cols, uint rows)
        {
            if (_disposed || _handle == 0) return;
            SshRustNative.sr_resize(_handle, cols, rows);
        }

        private void PollLoop(CancellationToken token)
        {
            var buf = new byte[PollChunkSize];
            while (!token.IsCancellationRequested && !_disposed)
            {
                try
                {
                    int outLen;
                    var rc = SshRustNative.sr_poll_read(_handle, buf, buf.Length, out outLen);
                    if (rc == SshRustNative.SR_OK && outLen > 0)
                    {
                        var frame = new byte[outLen];
                        Array.Copy(buf, frame, outLen);
                        OnData?.Invoke(frame);
                    }
                    else if (rc == SshRustNative.SR_ERR_CLOSED || rc == SshRustNative.SR_ERR_INVALID_HANDLE)
                    {
                        // EOF or remote closed the session.
                        OnClosed?.Invoke();
                        return;
                    }
                    // SR_ERR_NO_DATA / other transient codes: just continue polling.
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug($"PollLoop error: {e.Message}");
                    OnError?.Invoke("SSH poll loop failed: " + e.Message);
                    return;
                }

                token.WaitHandle.WaitOne(PollIntervalMs);
            }
        }

        /// <summary>
        /// Disconnect and free the session. Idempotent and thread-safe.
        /// </summary>
        public void Disconnect()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            long handle;
            lock (_gate)
            {
                handle = _handle;
                _handle = 0;
            }

            if (handle != 0)
            {
                try
                {
                    SshRustNative.sr_disconnect(handle);
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug($"sr_disconnect error: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Map a Rust error code (and optional UTF-8 error buffer) to a readable message.
        /// </summary>
        private static string ErrorText(int rc, byte[]? errBuf)
        {
            var detail = "";
            if (errBuf != null)
            {
                var n = Array.IndexOf(errBuf, (byte)0);
                if (n > 0)
                {
                    detail = Encoding.UTF8.GetString(errBuf, 0, n);
                }
            }

            var message = rc switch
            {
                SshRustNative.SR_OK => "ok",
                SshRustNative.SR_ERR_INVALID_HANDLE => "invalid session handle",
                SshRustNative.SR_ERR_PANIC => "Rust core panic",
                SshRustNative.SR_ERR_INVALID_ARG => "invalid argument",
                SshRustNative.SR_ERR_CONNECT => "SSH connection failed",
                SshRustNative.SR_ERR_CLOSED => "session closed",
                SshRustNative.SR_ERR_NO_DATA => "no data available",
                _ => $"unknown error ({rc})",
            };

            return string.IsNullOrEmpty(detail) ? message : $"{message}: {detail}";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            _pollTask = null;
        }
    }
}
#endif
