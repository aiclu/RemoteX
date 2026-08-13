using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils.Wpf.FileSystem
{
    // https://stackoverflow.com/a/29198314/8629624
    public static class DriveLabelHelper
    {
        public const string SHELL = "shell32.dll";

        [DllImport(SHELL, CharSet = CharSet.Unicode)]
        public static extern uint SHParseDisplayName(string pszName, IntPtr zero, [Out] out IntPtr ppidl, uint sfgaoIn, [Out] out uint psfgaoOut);

        [DllImport(SHELL, CharSet = CharSet.Unicode)]
        public static extern uint SHGetNameFromIDList(IntPtr pidl, SIGDN sigdnName, [Out] out String ppszName);

        public enum SIGDN : uint
        {
            NORMALDISPLAY = 0x00000000,
            PARENTRELATIVEPARSING = 0x80018001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            PARENTRELATIVE = 0x80080001
        }

        //var x = GetDriveLabel(@"C:\")
        public static string GetDriveLabel(string driveNameAsLetterColonBackslash)
        {
            if (SHParseDisplayName(driveNameAsLetterColonBackslash, IntPtr.Zero, out var intPtr, 0, out _) == 0
                && SHGetNameFromIDList(intPtr, SIGDN.PARENTRELATIVEEDITING, out var name) == 0
                && name != null)
            {
                return name;
            }
            return string.Empty;
        }
    }
}
