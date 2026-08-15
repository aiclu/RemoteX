using System;
using System.Runtime.InteropServices;
using System.Text;
using Shawn.Utils;

namespace _1RM.Service.RustLog
{
    /// <summary>
    /// Wires the Rust core's <c>tracing</c> output into <see cref="SimpleLogHelper"/>.
    /// The Rust cdylib exports <c>sr_set_log_callback</c>; we register a delegate
    /// that forwards each message at the matching Syslog level. The delegate must
    /// stay rooted for the process lifetime (the Rust side holds the pointer).
    /// </summary>
    internal static partial class RustLogBridge
    {
        private static readonly LogCallback Callback = OnRustLog;

        public static void Install()
        {
            // The Rust logging subscriber is idempotent; calling this more than
            // once (e.g. multiple sessions) is harmless.
            _ = Native.sr_set_log_callback(Callback);
        }

        private static void OnRustLog(int level, IntPtr msgPtr)
        {
            if (msgPtr == IntPtr.Zero) return;
            var msg = Marshal.PtrToStringUTF8(msgPtr);
            if (string.IsNullOrEmpty(msg)) return;
            try
            {
                switch (level)
                {
                    case 7: // LOG_DEBUG
                        SimpleLogHelper.Debug(msg);
                        break;
                    case 6: // LOG_INFO
                        SimpleLogHelper.Info(msg);
                        break;
                    case 4: // LOG_WARN
                        SimpleLogHelper.Warning(msg);
                        break;
                    case 3: // LOG_ERROR
                        SimpleLogHelper.Error(msg);
                        break;
                    default:
                        SimpleLogHelper.Info(msg);
                        break;
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LogCallback(int level, IntPtr msgPtr);

        private static partial class Native
        {
            [LibraryImport("ssh_rust")]
            internal static partial int sr_set_log_callback(LogCallback? cb);
        }
    }
}
