using System.Windows.Controls;
using System.Windows.Media;

namespace _1RM.View
{
    public partial class MainWindowView
    {
        private void MainMenuChrome_OnIsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (MainMenuChrome.Visibility != System.Windows.Visibility.Collapsed) return;
            // Keep the classic popup instance and its bindings, but never leave it
            // floating after its Fluent-mode anchor has been removed.
            ResetClassicMenu(PopupMenu, CbPopForMenu);
        }

        internal static void ResetClassicMenu(System.Windows.Controls.Primitives.Popup? popup, System.Windows.Controls.Primitives.ToggleButton? toggle)
        {
            popup?.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
            toggle?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
        }

        // Scope colors to the title bar and system buttons, never to editor/dialog resources.
        internal void ApplyFluentChrome(bool enabled, Brush canvas, Brush text, Brush hover)
        {
            if (enabled) MainChrome.Background = canvas;
            else MainChrome.SetResourceReference(Panel.BackgroundProperty, "PrimaryMidBrush");

            foreach (var element in new System.Windows.FrameworkElement[] { WinTitleBar, ChromeButtons })
            {
                if (enabled)
                {
                    element.Resources["PrimaryMidBrush"] = canvas;
                    element.Resources["PrimaryDarkBrush"] = canvas;
                    element.Resources["PrimaryLightBrush"] = hover;
                    element.Resources["PrimaryTextBrush"] = text;
                }
                else
                {
                    foreach (var key in new[] { "PrimaryMidBrush", "PrimaryDarkBrush", "PrimaryLightBrush", "PrimaryTextBrush" })
                        element.Resources.Remove(key);
                }
            }
        }
    }
}
