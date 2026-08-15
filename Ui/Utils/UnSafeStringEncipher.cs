using System;
using _1RM.Service.RustFtp;

namespace _1RM.Utils
{
    /// <summary>
    /// String encryption backed by the in-process Rust core (ssh_rust.dll).
    /// Re-implements the `1Remote.Security` algorithms so all previously stored
    /// ciphertexts remain decryptable (see docs/adr/0009).
    /// </summary>
    public static class UnSafeStringEncipher
    {
        private static string? _salt = null;
        public static void Init(string slat)
        {
            if (_salt == null)
            {
                _salt = slat;
            }
        }
        public static string SimpleEncrypt(string txt)
        {
            if (_salt == null) throw new InvalidOperationException("UnSafeStringEncipher.Init() must be called first");
            return RustStringEncipher.Encrypt(txt, _salt);
        }
        public static string? SimpleDecrypt(string encryptString)
        {
            if (_salt == null) throw new InvalidOperationException("UnSafeStringEncipher.Init() must be called first");
            return RustStringEncipher.Decrypt(encryptString, _salt);
        }

        public static string EncryptOnce(string str)
        {
            if (SimpleDecrypt(str) == null)
                return SimpleEncrypt(str);
            return str;
        }
        public static string DecryptOrReturnOriginalString(string originalString)
        {
            return SimpleDecrypt(originalString) ?? originalString;
        }
    }
}
