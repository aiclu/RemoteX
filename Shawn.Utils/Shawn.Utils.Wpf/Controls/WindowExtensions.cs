using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Shawn.Utils.Wpf.Controls
{
    public static class WindowExtensions
    {
        #region Window Flashing API Stuff

        private const UInt32 FLASHW_STOP = 0; //Stop flashing. The system restores the window to its original state.
        private const UInt32 FLASHW_CAPTION = 1; //Flash the window caption.
        private const UInt32 FLASHW_TRAY = 2; //Flash the taskbar button.
        private const UInt32 FLASHW_ALL = 3; //Flash both the window caption and taskbar button.
        private const UInt32 FLASHW_TIMER = 4; //Flash continuously, until the FLASHW_STOP flag is set.
        private const UInt32 FLASHW_TIMERNOFG = 12; //Flash continuously until the window comes to the foreground.

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public UInt32 cbSize; //The size of the structure in bytes.
            public IntPtr hwnd; //A Handle to the Window to be Flashed. The window can be either opened or minimized.
            public UInt32 dwFlags; //The Flash Status.
            public UInt32 uCount; // number of times to flash the window
            public UInt32 dwTimeout; //The rate at which the Window is to be flashed, in milliseconds. If Zero, the function uses the default cursor blink rate.
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        #endregion Window Flashing API Stuff

        public static void Flash(this IntPtr hwnd, UInt32 count = UInt32.MaxValue, UInt32 interval = 500)
        {
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = hwnd,
                dwFlags = FLASHW_TRAY | FLASHW_TIMER,
                uCount = count,
                dwTimeout = interval
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        public static void Flash(this Window win, UInt32 count = UInt32.MaxValue, UInt32 interval = 500)
        {
            WindowInteropHelper h = new WindowInteropHelper(win);
            Flash(h.Handle, count, interval);
        }

        public static void FlashIfNotActive(this Window win, UInt32 count = UInt32.MaxValue, UInt32 interval = 500)
        {
            if (win.IsLoaded)
            {
                //Don't flash if the window is active
                win.Dispatcher.Invoke(() =>
                {
                    if (win.IsActive) return;
                    Flash(win, count, interval);
                });
            }
        }




        public static void StopFlash(IntPtr hwnd)
        {
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = hwnd,
                dwFlags = FLASHW_STOP,
                uCount = UInt32.MaxValue,
                dwTimeout = 0
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        public static void StopFlashingWindow(this Window win)
        {
            WindowInteropHelper h = new WindowInteropHelper(win);
            StopFlash(h.Handle);
        }



        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        public static IntPtr GetForegroundWindowHwnd()
        {
            return GetForegroundWindow();
        }

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            GetWindowThreadProcessId(activatedHandle, out var activeProcId);
            return activeProcId == procId;
        }

        public static bool IsActivated(this IntPtr hwnd)
        {
            return hwnd == GetForegroundWindow();
        }




        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        public static IntPtr GetParentEx(this IntPtr hwnd)
        {
            return GetParent(hwnd);
        }

        /// <summary>
        /// return last parent hwnd
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="hWndNewParent"></param>
        /// <returns></returns>
        public static IntPtr SetParentEx(this IntPtr hwnd, IntPtr hWndNewParent)
        {
            return SetParent(hwnd, hWndNewParent);
        }




        #region API

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        public enum ShowWindowStyles : short
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }
        public static int ShowWindowEx(this IntPtr hwnd, ShowWindowStyles style)
        {
            return ShowWindow(hwnd, (int)style);
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public delegate bool WndEnumProc(IntPtr hWnd, int lParam);
        [DllImport("user32.dll")]
        public static extern int EnumWindows(WndEnumProc lpEnumFunc, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #region WindowLong

        public enum GetWindowLongIndex
        {
            GWL_STYLE = -16,
            GWL_EXSTYLE = -20
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public enum WindowStyles : uint
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_CAPTION = 0x00C00000,      // 	创建一个有标题框的窗口
            WS_BORDER = 0x00800000,       // 	创建一个单边框的窗口
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,      // 创建一个有垂直滚动条的窗口。
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,   // 创建一个具有可调边框的窗口
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,
            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,
            WS_TILED = 0x00000000,
            WS_ICONIC = 0x20000000,
            WS_SIZEBOX = 0x00040000,
            WS_POPUPWINDOW = 0x80880000,
            WS_OVERLAPPEDWINDOW = 0x00CF0000,
            WS_TILEDWINDOW = 0x00CF0000,
            WS_CHILDWINDOW = 0x40000000
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static int GetWindowLongEx(this IntPtr hWnd, GetWindowLongIndex li)
        {
            return GetWindowLong(hWnd, (int)li);
        }
        public static int SetWindowLongEx(this IntPtr hWnd, GetWindowLongIndex li, IEnumerable<WindowStyles> styles)
        {
            int lStyle = hWnd.GetWindowLongEx(li);
            foreach (var style in styles)
            {
                lStyle &= ~(int)style;
            }
            return SetWindowLong(hWnd, (int)li, lStyle);
        }
        #endregion


        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

        public static bool SetWindowToForeground(this IntPtr hwnd)
        {
            return SetForegroundWindow(hwnd) == 0 ? true : false;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetFocus(HandleRef hWnd);
        [DllImport("user32")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32")]
        public static extern bool IsWindow(IntPtr hWnd);

        public static bool IsWindowEx(this IntPtr hWnd)
        {
            return IsWindow(hWnd);
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        // https://stackoverflow.com/a/57819801/8629624
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowTitle(this IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            var title = new StringBuilder(length);
            GetWindowText(hWnd, title, length);
            return title.ToString();
        }


        [Flags]
        internal enum WindowExStyles
        {
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_OVERLAPPEDWINDOW = 0x00000300,
            WS_EX_PALETTEWINDOW = 0x00000188,
            WS_EX_LAYERED = 0x00080000,
            WS_EX_NOACTIVATE = 0x08000000
        }

        #endregion
    }
}