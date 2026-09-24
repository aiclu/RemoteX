using System;
using System.Windows;
using _1RM.Service;

namespace _1RM.View.Host
{
    // Window-level tokens only: never alias legacy resources or activate form styles
    // on protocol hosts. Header/menu scopes opt in separately.
    public static class FluentSessionAppearance
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(FluentSessionAppearance), new PropertyMetadata(false, Changed));
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(
            "Active", typeof(bool), typeof(FluentSessionAppearance), new PropertyMetadata(false));
        public static bool GetActive(DependencyObject target) => (bool)target.GetValue(ActiveProperty);
        public static void SetActive(DependencyObject target, bool value) => target.SetValue(ActiveProperty, value);
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(State), typeof(FluentSessionAppearance));
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is not Window window) return;
            (window.GetValue(StateProperty) as State)?.Dispose();
            window.SetValue(StateProperty, (bool)args.NewValue ? new State(window) : null);
        }
        private sealed class State : IDisposable
        {
            private readonly Window _window;
            private readonly ResourceDictionary _palette = new ResourceDictionary();
            private FluentAppearanceService? _service;
            public State(Window window)
            {
                _window = window;
                window.Loaded += Loaded; window.Unloaded += Unloaded;
                window.IsVisibleChanged += VisibleChanged; window.Closed += Closed;
                if (window.IsLoaded && window.IsVisible) Attach();
            }
            private void Loaded(object sender, RoutedEventArgs args) => Attach();
            private void Unloaded(object sender, RoutedEventArgs args) => Detach();
            private void VisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
            {
                if (_window.IsVisible && _window.IsLoaded) Attach(); else if (!_window.IsVisible) Detach();
            }
            private void Attach()
            {
                if (_service != null) return;
                _service = IoC.Get<FluentAppearanceService>(); _service.Changed += Refresh;
                Refresh(this, EventArgs.Empty);
            }
            private void Refresh(object? sender, EventArgs args)
            {
                var active = _service?.Enabled == true;
                if (active)
                {
                    _service!.ApplyPalette(_palette);
                    if (!_window.Resources.MergedDictionaries.Contains(_palette)) _window.Resources.MergedDictionaries.Add(_palette);
                }
                else _window.Resources.MergedDictionaries.Remove(_palette);
                SetActive(_window, active);
            }
            private void Detach() { if (_service != null) _service.Changed -= Refresh; _service = null; }
            private void Closed(object? sender, EventArgs args) => Dispose();
            public void Dispose()
            {
                Detach(); _window.Loaded -= Loaded; _window.Unloaded -= Unloaded;
                _window.IsVisibleChanged -= VisibleChanged; _window.Closed -= Closed;
                _window.Resources.MergedDictionaries.Remove(_palette); SetActive(_window, false);
            }
        }
    }
}
