using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Shawn.Utils.Wpf.FileSystem
{
    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/98d15984-12d9-47d1-a14d-6e887bf88333/how-to-get-quick-access-folder-path-in-windows-10-using-clsid-in-c-wpf-?forum=csharpgeneral
    /// <summary>
    /// 支持访问 win10 的快速访问文件夹
    /// </summary>
    public class QuickAccessFolder
    {
        #region Declarations
        public enum HRESULT : int
        {
            S_OK = 0,
            S_FALSE = 1,
            E_NOINTERFACE = unchecked((int)0x80004002),
            E_NOTIMPL = unchecked((int)0x80004001),
            E_FAIL = unchecked((int)0x80004005)
        }

        public enum SIGDN : uint
        {
            SIGDN_NORMALDISPLAY = 0,
            SIGDN_PARENTRELATIVEPARSING = 0x80018001,
            SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
            SIGDN_PARENTRELATIVEEDITING = 0x80031001,
            SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
            SIGDN_FILESYSPATH = 0x80058000,
            SIGDN_URL = 0x80068000,
            SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            SIGDN_PARENTRELATIVE = 0x80080001,
            SIGDN_PARENTRELATIVEFORUI = 0x80094001
        }

        public enum SICHINTF : uint
        {
            SICHINT_DISPLAY = 0,
            SICHINT_ALLFIELDS = 0x80000000,
            SICHINT_CANONICAL = 0x10000000,
            SICHINT_TEST_FILESYSPATH_IF_NOT_EQUAL = 0x20000000
        }

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem
        {
            HRESULT BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            HRESULT GetParent(out IShellItem ppsi);
            HRESULT GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            HRESULT GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            HRESULT Compare(IShellItem psi, SICHINTF hint, out int piOrder);
        }

        [ComImport]
        [Guid("70629033-e363-4a28-a567-0db78006e6d7")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IEnumShellItems
        {
            HRESULT Next(uint celt, out IShellItem rgelt, out uint pceltFetched);
            HRESULT Skip(uint celt);
            HRESULT Reset();
            HRESULT Clone(out IEnumShellItems ppenum);
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern HRESULT SHILCreateFromPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath, out IntPtr ppIdl, ref uint rgflnOut);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern HRESULT SHCreateItemFromIDList(IntPtr pidl, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);
        #endregion

        private static bool IsOsWindows8OrNewer => Environment.OSVersion.Version >= new Version(6, 2);


        public class  SubFolder
        {
            public readonly string FolderName;
            public readonly string FolderPath;

            public SubFolder(string folderName, string folderPath)
            {
                Debug.Assert(string.IsNullOrEmpty(folderName) == false);
                Debug.Assert(string.IsNullOrEmpty(folderPath) == false);
                FolderName = folderName;
                FolderPath = folderPath;
            }
        }
        public class FolderInfo
        {
            public readonly string FolderName;
            public readonly List<SubFolder> SubFolders = new List<SubFolder>();

            public FolderInfo(string folderName)
            {
                Debug.Assert(string.IsNullOrEmpty(folderName) == false);
                FolderName = folderName;
            }
        }

        public static FolderInfo? GetQuickAccess()
        {
            return Get("shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}");
        }

        public static FolderInfo? GetFrequentFolder()
        {
            return Get("shell:::{3936E9E4-D92C-4EEE-A85A-BC16D5EA0819}");
        }

        private static FolderInfo? Get(string shellPath)
        {
            //const string shellPath = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}"; // quick access
            //const string shellPath = "shell:::{3936E9E4-D92C-4EEE-A85A-BC16D5EA0819}"; // frequent folder win10
            uint rgflnOut = 0;
            // get top name "quick access" or "frequent folder" in local language
            if (IsOsWindows8OrNewer
            && HRESULT.S_OK == SHILCreateFromPath(shellPath, out var pidlFull, ref rgflnOut))
            {
                if (HRESULT.S_OK == SHCreateItemFromIDList(pidlFull, typeof(IShellItem).GUID, out var pShellItem)
                    && HRESULT.S_OK == pShellItem.BindToHandler(IntPtr.Zero, new Guid("94f60519-2850-4924-aa5a-d15e84868039"), typeof(IEnumShellItems).GUID, out var pEnum))
                {
                    string topFolderName = String.Empty;
                    string topFolderPath = String.Empty;

                    if (HRESULT.S_OK == pShellItem.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out var topFolderNamePtr))
                    {
                        topFolderName = Marshal.PtrToStringUni(topFolderNamePtr) ?? string.Empty;
                        Marshal.FreeCoTaskMem(topFolderNamePtr);
                    }

                    // out of range
                    //if (HRESULT.S_OK == pShellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var topFolderPathPtr))
                    //{
                    //    topFolderPath = Marshal.PtrToStringUni(topFolderPathPtr) ?? string.Empty;
                    //    Marshal.FreeCoTaskMem(topFolderPathPtr);
                    //}

                    if (string.IsNullOrEmpty(topFolderName) == false)
                    {
                        var ret = new FolderInfo(topFolderName);
                        // get items 
                        var pEnumShellItems = Marshal.GetObjectForIUnknown(pEnum) as IEnumShellItems;
                        while (HRESULT.S_OK == pEnumShellItems?.Next(1, out var psi, out var nFetched) && nFetched == 1)
                        {
                            var displayName = string.Empty;
                            var fullPath = string.Empty;
                            if (HRESULT.S_OK == psi.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out var displayNamePtr))
                            {
                                displayName = Marshal.PtrToStringUni(displayNamePtr) ?? string.Empty;
                                Marshal.FreeCoTaskMem(displayNamePtr);
                            }

                            if (HRESULT.S_OK == psi.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var fullPathPtr))
                            {
                                fullPath = Marshal.PtrToStringUni(fullPathPtr) ?? string.Empty;
                                Marshal.FreeCoTaskMem(fullPathPtr);
                            }

                            if (string.IsNullOrEmpty(displayName) == false && string.IsNullOrEmpty(fullPath) == false)
                            {
                                ret.SubFolders.Add(new SubFolder(displayName, fullPath));
                            }
                        }

                        if (ret.SubFolders.Count > 0)
                            return ret;
                    }
                }
            }

            return null;
        }


        //public static void Test()
        //{
        //    HRESULT hr = HRESULT.E_FAIL;
        //    IntPtr pidlFull = IntPtr.Zero;
        //    uint rgflnOut = 0;
        //    //string sPath = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}"; // quick access
        //    string sPath = "shell:::{3936E9E4-D92C-4EEE-A85A-BC16D5EA0819}"; // frequent folder win10
        //    hr = SHILCreateFromPath(sPath, out pidlFull, ref rgflnOut);
        //    if (hr == HRESULT.S_OK)
        //    {
        //        IntPtr pszName = IntPtr.Zero;
        //        IShellItem pShellItem;
        //        hr = SHCreateItemFromIDList(pidlFull, typeof(IShellItem).GUID, out pShellItem);
        //        if (hr == HRESULT.S_OK)
        //        {
        //            hr = pShellItem.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out pszName);
        //            if (hr == HRESULT.S_OK)
        //            {
        //                string sDisplayName = Marshal.PtrToStringUni(pszName);
        //                Console.WriteLine(string.Format("Folder Name : {0}", sDisplayName));
        //                Marshal.FreeCoTaskMem(pszName);
        //            }
        //            IEnumShellItems pEnumShellItems = null;
        //            IntPtr pEnum;
        //            Guid BHID_EnumItems = new Guid("94f60519-2850-4924-aa5a-d15e84868039");
        //            hr = pShellItem.BindToHandler(IntPtr.Zero, BHID_EnumItems, typeof(IEnumShellItems).GUID, out pEnum);
        //            if (hr == HRESULT.S_OK)
        //            {
        //                pEnumShellItems = Marshal.GetObjectForIUnknown(pEnum) as IEnumShellItems;
        //                IShellItem psi = null;
        //                uint nFetched = 0;
        //                while (HRESULT.S_OK == pEnumShellItems.Next(1, out psi, out nFetched) && nFetched == 1)
        //                {
        //                    pszName = IntPtr.Zero;
        //                    hr = psi.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out pszName);
        //                    hr = psi.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out pszName);
        //                    if (hr == HRESULT.S_OK)
        //                    {
        //                        string sDisplayName = Marshal.PtrToStringUni(pszName);
        //                        Console.WriteLine(string.Format("\tItem Name : {0}", sDisplayName));
        //                        Marshal.FreeCoTaskMem(pszName);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
