using System;
using System.Windows;
using _1RM.Service;

namespace _1RM.View
{
    // Explicit opt-in: never installed on application resources or session hosts.
    public static class FluentPage
    {
        public static readonly DependencyProperty ScopeProperty = DependencyProperty.RegisterAttached(
            "Scope", typeof(bool), typeof(FluentPage), new PropertyMetadata(false, ScopeChanged));
        public static bool GetScope(DependencyObject target) => (bool)target.GetValue(ScopeProperty);
        public static void SetScope(DependencyObject target, bool value) => target.SetValue(ScopeProperty, value);
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(
            "Active", typeof(bool), typeof(FluentPage), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetActive(DependencyObject target) => (bool)target.GetValue(ActiveProperty);
        public static void SetActive(DependencyObject target, bool value) => target.SetValue(ActiveProperty, value);
        public static readonly DependencyProperty NarrowProperty = DependencyProperty.RegisterAttached(
            "Narrow", typeof(bool), typeof(FluentPage), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetNarrow(DependencyObject target) => (bool)target.GetValue(NarrowProperty);
        public static void SetNarrow(DependencyObject target, bool value) => target.SetValue(NarrowProperty, value);
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(ScopeState), typeof(FluentPage));
        private static void ScopeChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is FrameworkElement element)) return;
            (element.GetValue(StateProperty) as ScopeState)?.Dispose();
            element.SetValue(StateProperty, (bool)args.NewValue ? new ScopeState(element) : null);
        }
        private sealed class ScopeState : IDisposable
        {
            private readonly FrameworkElement _element;
            private readonly ResourceDictionary _palette = new ResourceDictionary();
            private FluentAppearanceService? _service;
            public ScopeState(FrameworkElement element)
            {
                _element = element;
                element.Loaded += Loaded;
                element.Unloaded += Unloaded;
                element.SizeChanged += SizeChanged;
                element.IsVisibleChanged += VisibilityChanged;
                if (element.IsLoaded) Loaded(element, new RoutedEventArgs());
            }
            private void Loaded(object sender, RoutedEventArgs args)
            {
                if (_service != null || System.ComponentModel.DesignerProperties.GetIsInDesignMode(_element)) return;
                _service = IoC.TryGet<FluentAppearanceService>();
                if (_service == null) return; // Startup/error UI must work before IoC is ready.
                _service.Changed += Refresh;
                Refresh(this, EventArgs.Empty);
            }
            private void Unloaded(object sender, RoutedEventArgs args)
            {
                if (_service != null) _service.Changed -= Refresh;
                _service = null;
            }
            private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
            {
                if (_element.IsVisible && _element.IsLoaded) Loaded(sender, new RoutedEventArgs());
                else if (!_element.IsVisible) Unloaded(sender, new RoutedEventArgs());
            }
            private void SizeChanged(object sender, SizeChangedEventArgs args) => SetNarrow(_element, GetActive(_element) && _element.ActualWidth < 720);
            private void Refresh(object? sender, EventArgs args)
            {
                var enabled = _service?.Enabled == true;
                if (enabled)
                {
                    _service!.ApplyPalette(_palette);
                    FluentAppearanceService.ApplyAliases(_palette);
                    if (!_element.Resources.MergedDictionaries.Contains(_palette)) _element.Resources.MergedDictionaries.Add(_palette);
                }
                else _element.Resources.MergedDictionaries.Remove(_palette);
                SetActive(_element, enabled);
                SetNarrow(_element, enabled && _element.ActualWidth < 720);
            }
            public void Dispose()
            {
                Unloaded(this, new RoutedEventArgs());
                _element.Loaded -= Loaded; _element.Unloaded -= Unloaded; _element.SizeChanged -= SizeChanged;
                _element.IsVisibleChanged -= VisibilityChanged;
                _element.Resources.MergedDictionaries.Remove(_palette);
                SetActive(_element, false); SetNarrow(_element, false);
            }
        }
    }
}
