using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Shawn.Utils
{
    public static class ConsoleManager
    {
        private const string Kernel32_DllName = "kernel32.dll";
        [DllImport(Kernel32_DllName)]
        private static extern bool AllocConsole();
        [DllImport(Kernel32_DllName)]
        private static extern bool FreeConsole();
        [DllImport(Kernel32_DllName)]
        private static extern IntPtr GetConsoleWindow();
        [DllImport(Kernel32_DllName)]
        private static extern int GetConsoleOutputCP();
        public static bool HasConsole
        {
            get { return GetConsoleWindow() != IntPtr.Zero; }
        }
        /// Creates a new console instance if the process is not attached to a console already.    
        public static void Show()
        {
            if (!HasConsole)
            {
                AllocConsole();
                InvalidateOutAndError();
            }
        }
        /// If the process has a console attached to it, it will be detached and no longer visible. Writing to the System.Console is still possible, but no output will be shown.     
        public static void Hide()
        {
            if (HasConsole)
            {
                SetOutAndErrorNull();
                FreeConsole();
            }
        }
        public static void Toggle()
        {
            if (HasConsole)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }
        static void InvalidateOutAndError()
        {

#if NETCOREAPP
#else
            var type = typeof(System.Console);
            var @out = type.GetField("_out", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var @error = type.GetField("_error", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var initializeStdOutError = type.GetMethod("InitializeStdOutError", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            System.Diagnostics.Debug.Assert(@out != null);
            System.Diagnostics.Debug.Assert(@error != null);
            System.Diagnostics.Debug.Assert(initializeStdOutError != null);
            @out?.SetValue(null, null);
            @error?.SetValue(null, null); 
#endif
            //_InitializeStdOutError.Invoke(null, new object[] { true });
        }
        static void SetOutAndErrorNull()
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
    }
}
