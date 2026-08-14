using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shawn.Utils.Wpf;

namespace _1RM.Utils
{
    /// <summary>
    /// Cached app-brand icons (RemoteX LOGO). Window icons (taskbar) should always
    /// use the app icon, not a session icon — session icons are typically small
    /// (16-32 px) and look blurry when upscaled for the taskbar.
    ///
    /// ICO loading uses a native <see cref="System.Drawing.Icon"/> stream so
    /// Windows picks the best-matching frame for the requested size, instead of
    /// the default 16×16 frame that <c>BitmapFrame.Create</c> returns for ICO
    /// files (which is what made the taskbar icon look blurry).
    /// </summary>
    public static class AppIcons
    {
        private static readonly object Gate = new();
        private static ImageSource? _windowIcon;

        /// <summary>
        /// The app icon for window taskbar use, loaded from the compiled LOGO.ico
        /// resource (Release) / LOGO_D.ico (Debug) so it stays crisp at every size.
        /// </summary>
        public static ImageSource WindowIcon
        {
            get
            {
                if (_windowIcon != null) return _windowIcon;
                lock (Gate)
                {
                    if (_windowIcon != null) return _windowIcon;
#if DEBUG
                    const string ico = "LOGO_D.ico";
#else
                    const string ico = "LOGO.ico";
#endif
                    var uri = ResourceUriHelper.GetUriFromCurrentAssembly(ico);
                    _windowIcon = LoadMultiFrameIco(uri);
                    _windowIcon.Freeze();
                }
                return _windowIcon;
            }
        }

        /// <summary>
        /// Load an ICO file referenced by a pack URI and return the largest frame
        /// as a frozen <see cref="BitmapSource"/>. The largest frame is the
        /// sharpest fit for the taskbar (which renders at 32×32 or higher on
        /// modern DPI settings).
        /// </summary>
        private static BitmapSource LoadMultiFrameIco(System.Uri uri)
        {
            var info = System.Windows.Application.GetResourceStream(uri);
            using var ico = new Icon(info.Stream, 256, 256);
            var hIcon = ico.Handle;
            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
