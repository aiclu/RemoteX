using System;
using System.Collections.Generic;
using System.Windows;
using _1RM.Service;
using Shawn.Utils.Wpf;

namespace _1RM.View
{
    // Explicitly attached only to application-owned dialogs, never native RDP UI.
    public static class FluentWindowBounds
    {
        public static readonly DependencyProperty BodyProperty = DependencyProperty.RegisterAttached(
            "Body", typeof(bool), typeof(FluentWindowBounds), new PropertyMetadata(false, BodyChanged));
        public static bool GetBody(DependencyObject target) => (bool)target.GetValue(BodyProperty);
        public static void SetBody(DependencyObject target, bool value) => target.SetValue(BodyProperty, value);
        private static void BodyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is not FrameworkElement element) return;
            if ((bool)args.NewValue) { element.Loaded += BodyLoaded; element.Unloaded += BodyUnloaded; if (element.IsLoaded) BodyLoaded(element, new RoutedEventArgs()); }
            else { element.Loaded -= BodyLoaded; element.Unloaded -= BodyUnloaded; BodyUnloaded(element, new RoutedEventArgs()); element.Resources.Remove("FluentDialogBodyMaxHeight"); }
        }
        private static readonly DependencyProperty BodyWindowProperty = DependencyProperty.RegisterAttached("BodyWindow", typeof(Window), typeof(FluentWindowBounds));
        private static void BodyLoaded(object sender, RoutedEventArgs args)
        {
            var element = (FrameworkElement)sender;
            BodyUnloaded(sender, args);
            var window = Window.GetWindow(element);
            if (window == null) return; // Detached rendering supplies its own work-area constraint.
            element.SetValue(BodyWindowProperty, window);
            window.SizeChanged += BodyWindowSizeChanged;
            UpdateBody(element, window);
        }
        private static void BodyUnloaded(object sender, RoutedEventArgs args)
        {
            var element = (FrameworkElement)sender;
            if (element.GetValue(BodyWindowProperty) is Window window) window.SizeChanged -= BodyWindowSizeChanged;
            element.ClearValue(BodyWindowProperty);
        }
        private static void BodyWindowSizeChanged(object sender, SizeChangedEventArgs args)
        {
            // The frame gets its own bounds from layout; the window event invalidates the subtree.
            if (sender is Window window) UpdateBodies(window, window);
        }
        private static void UpdateBodies(DependencyObject node, Window window)
        {
            if (node is FrameworkElement element && GetBody(element)) UpdateBody(element, window);
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                UpdateBodies(System.Windows.Media.VisualTreeHelper.GetChild(node, i), window);
        }
        private static void UpdateBody(FrameworkElement element, Window window)
        {
            var area = ScreenInfoEx.GetCurrentScreen(window).VirtualWorkingArea;
            var height = window.SizeToContent == SizeToContent.Manual && window.ActualHeight > 0 ? Math.Min(area.Height, window.ActualHeight) : area.Height;
            element.Resources["FluentDialogBodyMaxHeight"] = Math.Max(1, height - 168);
        }
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(FluentWindowBounds), new PropertyMetadata(false, Changed));
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(State), typeof(FluentWindowBounds));
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is Window window)) return;
            (window.GetValue(StateProperty) as State)?.Dispose();
            window.SetValue(StateProperty, (bool)args.NewValue ? new State(window) : null);
        }
        private sealed class State : IDisposable
        {
            private readonly Window _window;
            private readonly Dictionary<DependencyProperty, object> _original = new Dictionary<DependencyProperty, object>();
            private FluentAppearanceService? _appearance;
            private bool _applied;
            public State(Window window)
            {
                _window = window;
                window.Loaded += Loaded; window.Closed += Closed;
                window.IsVisibleChanged += VisibleChanged;
                window.LocationChanged += LocationChanged;
                window.DpiChanged += DpiChanged;
            }
            private void Loaded(object sender, RoutedEventArgs args)
            {
                if (_appearance != null) return;
                if (_original.Count == 0)
                    foreach (var property in new[] { Window.WidthProperty, Window.MaxWidthProperty, Window.MaxHeightProperty,
                        Window.MinWidthProperty, Window.MinHeightProperty, Window.SizeToContentProperty })
                        _original[property] = _window.GetValue(property);
                _appearance = IoC.Get<FluentAppearanceService>();
                _appearance.Changed += Refresh;
                Refresh(this, EventArgs.Empty);
            }
            private void VisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
            {
                if (_window.IsVisible && _window.IsLoaded) Loaded(sender, new RoutedEventArgs());
                else if (!_window.IsVisible) Detach();
            }
            private void LocationChanged(object? sender, EventArgs args) => Refresh(sender, args);
            private void DpiChanged(object sender, DpiChangedEventArgs args) => Refresh(sender, args);
            private void Refresh(object? sender, EventArgs args)
            {
                if (_appearance == null) return;
                if (!_appearance.Enabled) { Restore(); return; }
                var area = ScreenInfoEx.GetCurrentScreen(_window).VirtualWorkingArea;
                var originalWidth = (double)_original[Window.WidthProperty];
                var preferred = double.IsNaN(originalWidth) ? Math.Min(560, (double)_original[Window.MaxWidthProperty]) : originalWidth;
                var metrics = new FluentDialogMetrics(preferred, area.Width, area.Height);
                var widthLimit = metrics.WidthLimit;
                var heightLimit = metrics.HeightLimit;
                _window.SetCurrentValue(Window.MinWidthProperty, Math.Min((double)_original[Window.MinWidthProperty], widthLimit));
                _window.SetCurrentValue(Window.MinHeightProperty, Math.Min((double)_original[Window.MinHeightProperty], heightLimit));
                _window.SetCurrentValue(Window.MaxWidthProperty, widthLimit);
                _window.SetCurrentValue(Window.MaxHeightProperty, heightLimit);
                _window.SetCurrentValue(Window.WidthProperty, metrics.Width);
                _window.SetCurrentValue(Window.SizeToContentProperty, SizeToContent.Height);
                _window.Resources["FluentDialogBodyMaxHeight"] = metrics.BodyMaxHeight;
                _window.Resources["FluentDialogFieldMaxWidth"] = metrics.FieldMaxWidth;
                _applied = true;
            }
            private void Restore()
            {
                if (!_applied) return;
                foreach (var entry in _original) _window.SetCurrentValue(entry.Key, entry.Value);
                _window.Resources.Remove("FluentDialogBodyMaxHeight");
                _window.Resources.Remove("FluentDialogFieldMaxWidth");
                _applied = false;
            }
            private void Detach()
            {
                if (_appearance != null) _appearance.Changed -= Refresh;
                _appearance = null;
            }
            private void Closed(object? sender, EventArgs args) => Dispose();
            public void Dispose()
            {
                Detach(); Restore();
                _window.Loaded -= Loaded; _window.Closed -= Closed;
                _window.IsVisibleChanged -= VisibleChanged; _window.LocationChanged -= LocationChanged;
                _window.DpiChanged -= DpiChanged;
            }
        }
    }
}
