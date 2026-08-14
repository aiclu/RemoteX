using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shawn.Utils.Wpf;

namespace _1RM.Utils
{
    /// <summary>
    /// Cached app-brand icons (RemoteX LOGO). Window icons (taskbar) should always
    /// use the app icon, not a session icon — session icons are typically small
    /// (16-32 px) and look blurry when upscaled for the taskbar.
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
                    var frame = BitmapFrame.Create(uri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
                    frame.Freeze();
                    _windowIcon = frame;
                }
                return _windowIcon;
            }
        }
    }
}
