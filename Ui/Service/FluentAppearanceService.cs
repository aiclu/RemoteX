using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace _1RM.Service
{
    // One system subscription, shared by the currently loaded opt-in surfaces.
    internal sealed class FluentAppearanceService : IDisposable
    {
        private readonly ConfigurationService _configuration;
        private EventHandler? _changed;
        private bool _disposed;
        public FluentAppearanceService(ConfigurationService configuration) => _configuration = configuration;
        public bool Enabled => _configuration.Theme.Fluent?.Enabled == true;
        public event EventHandler Changed
        {
            add
            {
                if (_disposed) return;
                if (_changed == null)
                {
                    SystemEvents.UserPreferenceChanged += SystemChanged;
                    SystemParameters.StaticPropertyChanged += ParametersChanged;
                }
                _changed += value;
            }
            remove
            {
                _changed -= value;
                if (_changed == null) Unsubscribe();
            }
        }
        private void SystemChanged(object sender, UserPreferenceChangedEventArgs e) => NotifyChanged();
        private void ParametersChanged(object? sender, PropertyChangedEventArgs e) => NotifyChanged();
        public void NotifyChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (_disposed || dispatcher == null || dispatcher.HasShutdownStarted) return;
            if (!dispatcher.CheckAccess()) { dispatcher.BeginInvoke(new Action(NotifyChanged)); return; }
            _changed?.Invoke(this, EventArgs.Empty);
        }
        public void ApplyPalette(ResourceDictionary resources) => ApplyPalette(resources,
            FluentPreferences.UseDark(_configuration.Theme.Fluent?.Theme, SystemUsesLight()), SystemParameters.HighContrast);

        internal static void ApplyPalette(ResourceDictionary resources, bool dark, bool highContrast)
        {
            Brush ColorBrush(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
            resources["FluentCanvas"] = highContrast ? SystemColors.WindowBrush : ColorBrush(dark ? "#202020" : "#F3F3F3");
            resources["FluentSurface"] = highContrast ? SystemColors.WindowBrush : ColorBrush(dark ? "#292929" : "#FFFFFF");
            resources["FluentText"] = highContrast ? SystemColors.WindowTextBrush : ColorBrush(dark ? "#F5F5F5" : "#1B1B1B");
            resources["FluentMuted"] = highContrast ? SystemColors.WindowTextBrush : ColorBrush(dark ? "#C0C0C0" : "#606060");
            resources["FluentStroke"] = highContrast ? SystemColors.WindowTextBrush : ColorBrush(dark ? "#484848" : "#DDDDDD");
            resources["FluentHover"] = highContrast ? SystemColors.ControlBrush : ColorBrush(dark ? "#383838" : "#E9E9E9");
            resources["FluentAccent"] = highContrast ? SystemColors.HighlightBrush : ColorBrush(dark ? "#60CDFF" : "#005FB8");
            resources["FluentAccentText"] = highContrast ? SystemColors.HighlightTextBrush : ColorBrush(dark ? "#002238" : "#FFFFFF");
        }
        internal static void ApplyAliases(ResourceDictionary resources)
        {
            foreach (var pair in new[] {
                ("BackgroundBrush", "FluentSurface"), ("BackgroundTextBrush", "FluentText"),
                ("PrimaryMidBrush", "FluentSurface"), ("PrimaryLightBrush", "FluentHover"),
                ("PrimaryDarkBrush", "FluentCanvas"), ("PrimaryTextBrush", "FluentText"),
                ("AccentMidBrush", "FluentAccent"), ("AccentLightBrush", "FluentAccent"),
                ("AccentDarkBrush", "FluentAccent"), ("AccentTextBrush", "FluentAccentText"),
                ("DefaultBorderBrush", "FluentStroke") })
            {
                resources[pair.Item1] = resources[pair.Item2];
                if (resources[pair.Item2] is SolidColorBrush brush)
                    resources[pair.Item1.Replace("Brush", "Color")] = brush.Color;
            }
        }
        private static bool SystemUsesLight()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return !Equals(key?.GetValue("AppsUseLightTheme"), 0);
            }
            catch (System.Security.SecurityException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
            catch (System.IO.IOException) { return true; }
        }
        private void Unsubscribe()
        {
            SystemEvents.UserPreferenceChanged -= SystemChanged;
            SystemParameters.StaticPropertyChanged -= ParametersChanged;
        }
        public void Dispose() { _disposed = true; Unsubscribe(); _changed = null; }
    }
}
