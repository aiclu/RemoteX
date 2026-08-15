using System;
using System.Runtime.InteropServices;
using System.Text;

namespace _1RM.Service.RustFtp
{
    /// <summary>
    /// P/Invoke declarations for the string-encryption FFI exported by the
    /// in-process Rust core (`ssh_rust.dll`). Re-implements the algorithms of
    /// the `1Remote.Security` package so existing stored passwords keep working
    /// (see docs/adr/0009). UTF-8 marshalling for all string parameters.
    /// </summary>
    internal static partial class StringEncryptRustNative
    {
        public const int SR_OK = 0;
        public const int SR_ERR_INVALID_ARG = -3;
        public const int SR_ERR_CONNECT = -4;

        [LibraryImport("ssh_rust", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_string_encrypt(
            string plain,
            string salt,
            string? secondaryKey,
            IntPtr outBuf,
            nint outCap,
            out nint outLen,
            IntPtr errBuf,
            nint errCap);

        [LibraryImport("ssh_rust", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int sr_string_decrypt(
            string cipher,
            string salt,
            string? secondaryKey,
            IntPtr outBuf,
            nint outCap,
            out nint outLen,
            IntPtr errBuf,
            nint errCap);
    }

    /// <summary>
    /// Managed wrapper over the Rust string-encryption FFI. Exposes the same
    /// shape as the old `1Remote.Security` usage so callers don't change.
    /// Thread-safe for independent calls; salt is passed per call.
    /// </summary>
    internal static class RustStringEncipher
    {
        /// <summary>Encrypt via the Rust core.</summary>
        public static string Encrypt(string plain, string salt)
        {
            return EncryptWithKey(plain, salt, null);
        }

        /// <summary>Encrypt with an optional secondary key (matches 1Remote.Security).</summary>
        public static string EncryptWithKey(string plain, string salt, string? secondaryKey)
        {
            // First pass: get required buffer size.
            var rc = StringEncryptRustNative.sr_string_encrypt(
                plain, salt, secondaryKey,
                IntPtr.Zero, 0, out var needed, IntPtr.Zero, 0);
            if (rc != StringEncryptRustNative.SR_OK)
            {
                throw new InvalidOperationException($"encrypt failed (rc={rc})");
            }
            var bytes = new byte[needed];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var errBuf = new byte[1024];
                var errPinned = GCHandle.Alloc(errBuf, GCHandleType.Pinned);
                try
                {
                    rc = StringEncryptRustNative.sr_string_encrypt(
                        plain, salt, secondaryKey,
                        handle.AddrOfPinnedObject(), bytes.Length,
                        out var outLen, errPinned.AddrOfPinnedObject(), errBuf.Length);
                    if (rc != StringEncryptRustNative.SR_OK)
                    {
                        var msg = ErrorText(errBuf);
                        throw new InvalidOperationException($"encrypt failed: {msg} (rc={rc})");
                    }
                    return Encoding.UTF8.GetString(bytes, 0, (int)outLen);
                }
                finally
                {
                    errPinned.Free();
                }
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>Decrypt via the Rust core. Returns null when not decryptable.</summary>
        public static string? Decrypt(string cipher, string salt)
        {
            return DecryptWithKey(cipher, salt, null);
        }

        /// <summary>Decrypt with an optional secondary key. Returns null when not decryptable.</summary>
        public static string? DecryptWithKey(string cipher, string salt, string? secondaryKey)
        {
            var rc = StringEncryptRustNative.sr_string_decrypt(
                cipher, salt, secondaryKey,
                IntPtr.Zero, 0, out var needed, IntPtr.Zero, 0);
            if (rc != StringEncryptRustNative.SR_OK)
            {
                // Not decryptable (wrong salt/key/cipher) — caller treats as plaintext.
                return null;
            }
            var bytes = new byte[needed];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var errBuf = new byte[1024];
                var errPinned = GCHandle.Alloc(errBuf, GCHandleType.Pinned);
                try
                {
                    rc = StringEncryptRustNative.sr_string_decrypt(
                        cipher, salt, secondaryKey,
                        handle.AddrOfPinnedObject(), bytes.Length,
                        out var outLen, errPinned.AddrOfPinnedObject(), errBuf.Length);
                    if (rc != StringEncryptRustNative.SR_OK)
                    {
                        return null;
                    }
                    return Encoding.UTF8.GetString(bytes, 0, (int)outLen);
                }
                finally
                {
                    errPinned.Free();
                }
            }
            finally
            {
                handle.Free();
            }
        }

        private static string ErrorText(byte[] errBuf)
        {
            var len = Array.IndexOf(errBuf, (byte)0);
            return len < 0 ? "" : Encoding.UTF8.GetString(errBuf, 0, len);
        }
    }
}
