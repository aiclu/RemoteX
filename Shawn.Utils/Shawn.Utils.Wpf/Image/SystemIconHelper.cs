using System;
using System.CodeDom;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Shawn.Utils.Wpf.Image
{
    public class SystemIconHelper
    {
        #region SHFILEINFO

        #region SHGetFileInfo

        [Flags]
        public enum ShellGetFileInfoFlags : uint
        {
            /// <summary>
            ///     Modify SHGFI_ICON, causing the function to retrieve the file's large icon. The SHGFI_ICON flag must also be set.
            /// </summary>
            LargeIcon = 0x0,

            /// <summary>
            ///     Modify SHGFI_ICON, causing the function to retrieve the file's small icon. Also used to modify SHGFI_SYSICONINDEX,
            ///     causing the function to return the handle to the system image list that contains small icon images. The SHGFI_ICON
            ///     and/or SHGFI_SYSICONINDEX flag must also be set.
            /// </summary>
            SmallIcon = 0x1,

            /// <summary>
            ///     Retrieve the handle to the icon that represents the file and the index of the icon within the system image list.
            ///     The handle is copied to
            ///     the hIcon member of the structure specified by psfi, and the index is copied to the iIcon member.
            /// </summary>
            Icon = 0x100,

            /// <summary>
            ///     Indicates that the function should not attempt to access the file specified by pszPath. Rather, it
            ///     should act as if the file specified by pszPath exists with the file attributes passed in dwFileAttributes.
            ///     This flag cannot be combined with the SHGFI_ATTRIBUTES, SHGFI_EXETYPE, or SHGFI_PIDL flags.
            /// </summary>
            UseFileAttributes = 0x10
        }

        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        //Struct used by SHGetFileInfo function
        [StructLayout(LayoutKind.Sequential)]
        protected struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };


        //Struct used by SHGetFileInfo function
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        protected struct SHFILEINFOW
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        //Import SHGetFileInfo function
        [DllImport("shell32.dll")]
        protected static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, ShellGetFileInfoFlags uFlags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        protected static extern IntPtr SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbSizeFileInfo, ShellGetFileInfoFlags uFlags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        protected static extern IntPtr SHGetFileInfoW(IntPtr pidl, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbSizeFileInfo, ShellGetFileInfoFlags uFlags);
        #endregion

        private static bool IsDirPath(string path)
        {
            if (path.EndsWith("/") || path.EndsWith("\\"))
                return true;
            if (Directory.Exists(path))
                return true;
            //if (File.Exists(path))
            //    return false;
            return false;
        }

        /// <summary>
        /// 获得文件或文件夹图标
        /// </summary>
        /// <param name="path">文件类型的扩展名或文件的绝对路径，如".jpg"</param>
        /// <param name="isDir"></param>
        /// <param name="isFile"></param>
        /// <returns></returns>
        public static BitmapSource? GetIcon(string path = "", bool? isDir = null, bool? isFile = null)
        {
            if (isDir != false && Directory.Exists(path))
            {
                return GetThumbnailFromShell(path, ShellGetFileInfoFlags.LargeIcon);
            }

            if (isFile != false && File.Exists(path))
            {
                return GetFileIcon(path);
            }

            var tmpPath = Path.Combine(Path.GetTempPath(), DateTime.Now.Millisecond.ToString());
            try
            {
                if (isDir == true)
                {
                    if (Directory.Exists(tmpPath) == false)
                        Directory.CreateDirectory(tmpPath);
                }
                else if (isDir == null || isFile == true)
                {
                    if (path == ".*")
                        path = "";
                    if (path.StartsWith("."))
                        tmpPath = tmpPath + path;
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    if (File.Exists(tmpPath) == false)
                    {
                        File.WriteAllText(tmpPath, "");
                    }
                }
                return GetThumbnailFromShell(tmpPath);
            }
            finally
            {
                if (Directory.Exists(tmpPath))
                {
                    Directory.Delete(tmpPath, true);
                }
                else if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
        }
        public static BitmapSource? GetFileIcon(string path)
        {
            if (File.Exists(path))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                var bmp = icon?.ToBitmap()?.ToBitmapImage();
                return bmp;
            }
            return null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);


        public static BitmapImage? GetThumbnailFromShell(string path, ShellGetFileInfoFlags icon = ShellGetFileInfoFlags.SmallIcon)
        {
            try
            {
                var shinfo = new SHFILEINFOW();
                // get the file info from the windows api
                SHGetFileInfoW(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), ShellGetFileInfoFlags.Icon | icon);
                // save it into a bitmap
                var thumbnail = ((Icon)Icon.FromHandle(shinfo.hIcon).Clone()).ToBitmap();
                // destroy the icon, as it isn't needed anymore
                DestroyIcon(shinfo.hIcon);
                return thumbnail.ToBitmapImage();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Fatal(path, e);
            }
            return null;
        }


        //public static BitmapSource? GetFolderIcon(IntPtr ptr)
        //{
        //    try
        //    {
        //        var shinfo = new SHFILEINFOW();
        //        SHGetFileInfoW(ptr, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), ShellGetFileInfoFlags.Icon | ShellGetFileInfoFlags.LargeIcon);
        //        using var i = System.Drawing.Icon.FromHandle(shinfo.hIcon);
        //        return i.ToBitmap().ToBitmapSource();
        //    }
        //    catch
        //    {
        //    }
        //    return null;
        //}



        public static async Task<BitmapSource> GetIconAsync(string path)
        {
            if (Directory.Exists(path))
            {
                return await GetFolderIconAsync(path);
            }
            else if (File.Exists(path))
            {
                return await GetFileIconAsync(path);
            }
            throw new FileNotFoundException(path + " not found!");
        }
        public static async Task<BitmapSource> GetFileIconAsync(string path)
        {
            var t = await Task.Run(() => GetFileIcon(path));
            return t!;
        }
        public static async Task<BitmapSource> GetFolderIconAsync(string path)
        {
            var t = await Task.Run(() => GetThumbnailFromShell(path, ShellGetFileInfoFlags.LargeIcon));
            return t!;
        }
        #endregion

        #region Microsoft.WindowsAPICodePack.Shell

        //public static BitmapSource GetIcon(string path)
        //{
        //    if (Directory.Exists(path))
        //    {
        //        return GetFolderIcon(path);
        //    }
        //    else if (File.Exists(path))
        //    {
        //        return GetFileIcon(path);
        //    }
        //    return null;
        //}
        //public static BitmapSource GetFileIcon(string path)
        //{
        //    var shellFile = ShellFile.FromFilePath(path);
        //    return shellFile.Thumbnail.LargeBitmapSource;
        //}
        //public static BitmapSource GetFolderIcon(string path)
        //{
        //    //var shellFile = ShellFile.FromParsingName(path);
        //    //return shellFile.Thumbnail.LargeBitmapSource;

        //    var shellFile = ShellFile.FromParsingName(path);
        //    var icon = shellFile.Thumbnail.LargeBitmap;
        //    icon.MakeTransparent(System.Drawing.Color.Black);
        //    return icon.ToBitmapSource();
        //}



        //public static async Task<BitmapSource> GetIconAsync(string path)
        //{
        //    if (Directory.Exists(path))
        //    {
        //        return await GetFolderIconAsync(path);
        //    }
        //    else if (File.Exists(path))
        //    {
        //        return await GetFileIconAsync(path);
        //    }
        //    return null;
        //}
        //public static async Task<BitmapSource> GetFileIconAsync(string path)
        //{
        //    var t = await Task.Run(() => GetFileIcon(path));
        //    return t;
        //}
        //public static async Task<BitmapSource> GetFolderIconAsync(string path)
        //{
        //    var t = await Task.Run(() => GetFolderIcon(path));
        //    return t;
        //} 
        #endregion

    }
}