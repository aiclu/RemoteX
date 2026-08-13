using System.Windows;
using System.Windows.Documents;

namespace Shawn.Utils.Wpf.Controls
{
    public static class HyperlinkHelper
    {
        public static bool GetIsOpenExternal(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsOpenExternalProperty);
        }

        public static void SetIsOpenExternal(DependencyObject obj, bool value)
        {
            obj.SetValue(IsOpenExternalProperty, value);
        }

        public static readonly DependencyProperty IsOpenExternalProperty =
            DependencyProperty.RegisterAttached("IsOpenExternal", typeof(bool), typeof(HyperlinkHelper), new UIPropertyMetadata(false, OnIsOpenExternalChanged));

        private static void OnIsOpenExternalChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is Hyperlink hyperlink)
                if ((bool)args.NewValue)
                {
                    hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                }
                else
                {
                    hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
                }
        }

        private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            OpenUriBySystem(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        public static void OpenUriBySystem(string uri)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = uri
            };
            System.Diagnostics.Process.Start(psi);
        }
    }
}