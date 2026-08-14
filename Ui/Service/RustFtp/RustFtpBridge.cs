using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using _1RM.Model.Protocol.FileTransmit.Transmitters;

namespace _1RM.Service.RustFtp
{
    /// <summary>
    /// Session-level bridge to the Rust FTP/SFTP FFI. Manages one session handle,
    /// JSON marshalling for directory listings, and progress callbacks for
    /// upload/download. Thread-safe: each operation takes the session lock so the
    /// blocking Rust calls never interleave (mirrors the old SemaphoreSlim).
    ///
    /// FTP and SFTP live in separate handle spaces on the Rust side (FTP handles
    /// start at 1_000_000_000, SFTP at 2_000_000_000), so every instance method
    /// must dispatch to the matching sr_ftp_* / sr_sftp_* export.
    /// </summary>
    internal sealed class RustFtpBridge : IDisposable
    {
        private enum SessionKind
        {
            Ftp,
            Sftp,
        }

        private readonly SessionKind _kind;
        private readonly long _handle;
        private readonly object _gate = new();
        private bool _disposed;

        private RustFtpBridge(SessionKind kind, long handle)
        {
            _kind = kind;
            _handle = handle;
        }

        public static RustFtpBridge ConnectFtp(string host, ushort port, string user, string password)
        {
            var errBuf = new byte[1024];
            var rc = FtpRustNative.sr_ftp_connect(host, port, user, password, out var handle, errBuf, errBuf.Length);
            if (rc != FtpRustNative.SR_OK)
                throw new InvalidOperationException(ErrorText(rc, errBuf));
            return new RustFtpBridge(SessionKind.Ftp, handle);
        }

        public static RustFtpBridge ConnectSftp(string host, ushort port, string user, string? password, string? keyPath)
        {
            var errBuf = new byte[1024];
            var rc = FtpRustNative.sr_sftp_connect(host, port, user, password, keyPath, out var handle, errBuf, errBuf.Length);
            if (rc != FtpRustNative.SR_OK)
                throw new InvalidOperationException(ErrorText(rc, errBuf));
            return new RustFtpBridge(SessionKind.Sftp, handle);
        }

        public bool Exists(string path)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var errBuf = new byte[1024];
                var rc = _kind == SessionKind.Ftp
                    ? FtpRustNative.sr_ftp_exists(_handle, path, out var exists, errBuf, errBuf.Length)
                    : FtpRustNative.sr_sftp_exists(_handle, path, out exists, errBuf, errBuf.Length);
                if (rc != FtpRustNative.SR_OK)
                    throw new InvalidOperationException(ErrorText(rc, errBuf));
                return exists != 0;
            }
        }

        public List<RemoteItem> ListDirectoryItems(string path)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var errBuf = new byte[1024];
                var outBuf = new byte[256 * 1024];
                var rc = _kind == SessionKind.Ftp
                    ? FtpRustNative.sr_ftp_list(_handle, path, outBuf, outBuf.Length, out var outLen, errBuf, errBuf.Length)
                    : FtpRustNative.sr_sftp_list(_handle, path, outBuf, outBuf.Length, out outLen, errBuf, errBuf.Length);
                if (rc != FtpRustNative.SR_OK)
                    throw new InvalidOperationException(ErrorText(rc, errBuf));
                var json = Encoding.UTF8.GetString(outBuf, 0, (int)outLen);
                var dtos = JsonConvert.DeserializeObject<List<RemoteItemDto>>(json) ?? new List<RemoteItemDto>();
                var ret = new List<RemoteItem>(dtos.Count);
                foreach (var dto in dtos)
                {
                    ret.Add(new RemoteItem
                    {
                        Name = dto.name,
                        FullName = dto.full_name,
                        IsDirectory = dto.is_directory,
                        IsSymlink = dto.is_symlink,
                        ByteSize = dto.size,
                        LastUpdate = dto.last_update > 0 ? DateTimeOffset.FromUnixTimeSeconds(dto.last_update).LocalDateTime : DateTime.MinValue,
                    });
                }
                return ret;
            }
        }

        public void Delete(string path)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var errBuf = new byte[1024];
                var rc = _kind == SessionKind.Ftp
                    ? FtpRustNative.sr_ftp_delete(_handle, path, errBuf, errBuf.Length)
                    : FtpRustNative.sr_sftp_delete(_handle, path, errBuf, errBuf.Length);
                if (rc != FtpRustNative.SR_OK)
                    throw new InvalidOperationException(ErrorText(rc, errBuf));
            }
        }

        public void CreateDirectory(string path)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var errBuf = new byte[1024];
                var rc = _kind == SessionKind.Ftp
                    ? FtpRustNative.sr_ftp_mkdir(_handle, path, errBuf, errBuf.Length)
                    : FtpRustNative.sr_sftp_mkdir(_handle, path, errBuf, errBuf.Length);
                if (rc != FtpRustNative.SR_OK)
                    throw new InvalidOperationException(ErrorText(rc, errBuf));
            }
        }

        public void RenameFile(string path, string newPath)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var errBuf = new byte[1024];
                var rc = _kind == SessionKind.Ftp
                    ? FtpRustNative.sr_ftp_rename(_handle, path, newPath, errBuf, errBuf.Length)
                    : FtpRustNative.sr_sftp_rename(_handle, path, newPath, errBuf, errBuf.Length);
                if (rc != FtpRustNative.SR_OK)
                    throw new InvalidOperationException(ErrorText(rc, errBuf));
            }
        }

        /// <summary>
        /// Download a remote file to a local path. Runs the blocking Rust call on
        /// a worker thread so the caller's async context is not held up.
        /// </summary>
        public Task DownloadFileAsync(string remotePath, string localPath, Action<ulong>? progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    using var progressPtr = ProgressCallback.Create(progress, ct);
                    var errBuf = new byte[1024];
                    var rc = _kind == SessionKind.Ftp
                        ? FtpRustNative.sr_ftp_download(_handle, remotePath, localPath, progressPtr.Pointer, progressPtr.CancelPointer, errBuf, errBuf.Length)
                        : FtpRustNative.sr_sftp_download(_handle, remotePath, localPath, progressPtr.Pointer, progressPtr.CancelPointer, errBuf, errBuf.Length);
                    if (rc != FtpRustNative.SR_OK)
                        throw new InvalidOperationException(ErrorText(rc, errBuf));
                }
            }, ct);
        }

        /// <summary>
        /// Upload a local file to a remote path.
        /// </summary>
        public Task UploadFileAsync(string localPath, string remotePath, Action<ulong>? progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    using var progressPtr = ProgressCallback.Create(progress, ct);
                    var errBuf = new byte[1024];
                    var rc = _kind == SessionKind.Ftp
                        ? FtpRustNative.sr_ftp_upload(_handle, localPath, remotePath, progressPtr.Pointer, progressPtr.CancelPointer, errBuf, errBuf.Length)
                        : FtpRustNative.sr_sftp_upload(_handle, localPath, remotePath, progressPtr.Pointer, progressPtr.CancelPointer, errBuf, errBuf.Length);
                    if (rc != FtpRustNative.SR_OK)
                        throw new InvalidOperationException(ErrorText(rc, errBuf));
                }
            }, ct);
        }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                if (_kind == SessionKind.Ftp)
                    FtpRustNative.sr_ftp_disconnect(_handle);
                else
                    FtpRustNative.sr_sftp_disconnect(_handle);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RustFtpBridge));
        }

        private static string ErrorText(int rc, byte[] errBuf)
        {
            var msg = Encoding.UTF8.GetString(errBuf).TrimEnd('\0');
            return $"{msg} (rc={rc})";
        }
    }

    /// <summary>DTO mirroring the Rust JSON listing.</summary>
    internal class RemoteItemDto
    {
        public string name = "";
        public string full_name = "";
        public bool is_directory;
        public bool is_symlink;
        public ulong size;
        public long last_update;
    }

    /// <summary>
    /// Owns the unmanaged progress callback + cancel flag for one transfer.
    /// The managed delegate is kept alive for the duration of the call so the
    /// GC does not collect it while Rust holds the function pointer.
    ///
    /// The cancel flag is a pinned 1-byte buffer written with volatile semantics
    /// on cancellation — memory layout compatible with Rust's <c>AtomicBool</c>
    /// (1 byte, 0/1) that the transfer loop polls each chunk.
    /// </summary>
    internal sealed class ProgressCallback : IDisposable
    {
        private readonly FtpRustNative.ProgressCb? _managedDelegate;
        private readonly byte[] _cancelFlag = new byte[1];
        private readonly GCHandle _cancelPinned;
        private readonly CancellationTokenRegistration _registration;

        public IntPtr Pointer { get; }
        public IntPtr CancelPointer { get; }

        private ProgressCallback(FtpRustNative.ProgressCb? cb, CancellationToken ct)
        {
            if (cb != null)
            {
                _managedDelegate = cb;
                Pointer = Marshal.GetFunctionPointerForDelegate(cb);
            }
            else
            {
                Pointer = IntPtr.Zero;
            }
            _cancelPinned = GCHandle.Alloc(_cancelFlag, GCHandleType.Pinned);
            CancelPointer = Marshal.UnsafeAddrOfPinnedArrayElement(_cancelFlag, 0);
            _registration = ct.Register(() => Volatile.Write(ref _cancelFlag[0], 1));
        }

        public static ProgressCallback Create(Action<ulong>? progress, CancellationToken ct)
        {
            FtpRustNative.ProgressCb? cb = null;
            if (progress != null)
            {
                cb = transferred => progress(transferred);
            }
            return new ProgressCallback(cb, ct);
        }

        public void Dispose()
        {
            _registration.Dispose();
            if (_cancelPinned.IsAllocated) _cancelPinned.Free();
        }
    }
}
