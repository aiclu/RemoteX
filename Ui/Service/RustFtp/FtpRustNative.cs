using System;
using System.Runtime.InteropServices;

namespace _1RM.Service.RustFtp
{
    /// <summary>
    /// P/Invoke declarations for the FTP/SFTP FFI surface of <c>ssh_rust.dll</c>.
    /// Return codes mirror <c>lib.rs</c>:
    ///   SR_OK=0, SR_ERR_NO_DATA=1, SR_ERR_INVALID_HANDLE=-1, SR_ERR_PANIC=-2,
    ///   SR_ERR_INVALID_ARG=-3, SR_ERR_CONNECT=-4.
    /// </summary>
    internal static partial class FtpRustNative
    {
        private const string DllName = "ssh_rust";

        public const int SR_OK = 0;
        public const int SR_ERR_INVALID_HANDLE = -1;
        public const int SR_ERR_PANIC = -2;
        public const int SR_ERR_INVALID_ARG = -3;
        public const int SR_ERR_CONNECT = -4;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ProgressCb(ulong transferred);

        // ---- FTP ----

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_connect(
            string host,
            ushort port,
            string user,
            string password,
            out long handle,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName)]
        internal static partial int sr_ftp_disconnect(long handle);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_list(
            long handle,
            string path,
            [Out] byte[] outBuf,
            int outCap,
            out nint outLen,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_exists(
            long handle,
            string path,
            out byte outExists,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_delete(
            long handle,
            string path,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_mkdir(
            long handle,
            string path,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_rename(
            long handle,
            string path,
            string newPath,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_download(
            long handle,
            string remotePath,
            string localPath,
            nint progressCb,
            nint cancel,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_ftp_upload(
            long handle,
            string localPath,
            string remotePath,
            nint progressCb,
            nint cancel,
            [Out] byte[] errBuf,
            int errCap);

        // ---- SFTP ----

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_connect(
            string host,
            ushort port,
            string user,
            string? password,
            string? keyPath,
            out long handle,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName)]
        internal static partial int sr_sftp_disconnect(long handle);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_list(
            long handle,
            string path,
            [Out] byte[] outBuf,
            int outCap,
            out nint outLen,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_exists(
            long handle,
            string path,
            out byte outExists,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_delete(
            long handle,
            string path,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_mkdir(
            long handle,
            string path,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_rename(
            long handle,
            string path,
            string newPath,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_download(
            long handle,
            string remotePath,
            string localPath,
            nint progressCb,
            nint cancel,
            [Out] byte[] errBuf,
            int errCap);

        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_sftp_upload(
            long handle,
            string localPath,
            string remotePath,
            nint progressCb,
            nint cancel,
            [Out] byte[] errBuf,
            int errCap);
    }
}
