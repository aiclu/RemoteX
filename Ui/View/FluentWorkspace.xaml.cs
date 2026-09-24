using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using _1RM.Service;
using _1RM.Utils;

namespace _1RM.View
{
    public partial class FluentWorkspace : UserControl
    {
        public static readonly DependencyProperty IsPreviewProperty = DependencyProperty.RegisterAttached(
            "IsPreview", typeof(bool), typeof(FluentWorkspace),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetIsPreview(DependencyObject target) => (bool)target.GetValue(IsPreviewProperty);
        public static void SetIsPreview(DependencyObject target, bool value) => target.SetValue(IsPreviewProperty, value);
        public static readonly DependencyProperty IsCompactProperty = DependencyProperty.RegisterAttached(
            "IsCompact", typeof(bool), typeof(FluentWorkspace),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetIsCompact(DependencyObject target) => (bool)target.GetValue(IsCompactProperty);
        public static void SetIsCompact(DependencyObject target, bool value) => target.SetValue(IsCompactProperty, value);

        private MainWindowViewModel? _vm;
        private ServerView.ServerPageViewModelBase? _tagPage;
        private FluentTagView? _tagView;
        private readonly ResourceDictionary _listPalette = new ResourceDictionary();
        private bool _listening;

        public FluentWorkspace()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += (_, _) => { if (IsLoaded) { Detach(); Attach(); Refresh(); } };
            SizeChanged += (_, _) => RefreshLayout();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) { Attach(); Refresh(); }
        private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();
        private void Attach()
        {
            if (_listening) return;
            _vm = DataContext as MainWindowViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmChanged;
            IoC.Get<FluentAppearanceService>().Changed += OnAppearanceChanged;
            _listening = true;
        }
        private void Detach()
        {
            DetachTags();
            if (!_listening) return;
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            IoC.Get<FluentAppearanceService>().Changed -= OnAppearanceChanged;
            _vm = null;
            _listening = false;
        }
        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainWindowViewModel.IsFluentPreview) or nameof(MainWindowViewModel.FluentTheme)
                or nameof(MainWindowViewModel.CurrentView) or nameof(MainWindowViewModel.IsShownList))
                Refresh();
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveServerViewModel)) RefreshTags();
        }
        private void DetachTags()
        {
            if (_tagPage != null) _tagPage.PropertyChanged -= OnTagPageChanged;
            _tagPage = null;
            Tags.ItemsSource = null;
            _tagView?.Dispose();
            _tagView = null;
        }
        private void RefreshTags()
        {
            var page = Enabled ? _vm?.ActiveServerViewModel : null;
            if (ReferenceEquals(page, _tagPage) && _tagView != null &&
                ReferenceEquals(_tagView.View.SourceCollection, page?.HeaderTags)) return;
            DetachTags();
            _tagPage = page;
            if (page == null) return;
            page.PropertyChanged += OnTagPageChanged;
            _tagView = new FluentTagView(page.HeaderTags);
            Tags.ItemsSource = _tagView.View;
        }
        private void OnTagPageChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ServerView.ServerPageViewModelBase.HeaderTags))
            {
                if (Dispatcher.CheckAccess()) RefreshTags();
                else QueueRefresh();
            }
        }
        private void OnAppearanceChanged(object? sender, EventArgs e) => QueueRefresh();
        private void QueueRefresh()
        {
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.BeginInvoke(new Action(() => { if (_listening) Refresh(); }));
        }
        private bool Enabled => _vm?.IsFluentPreview == true && _vm.IsShownList;

        private void Refresh()
        {
            if (!Dispatcher.CheckAccess()) { QueueRefresh(); return; }
            IoC.Get<FluentAppearanceService>().ApplyPalette(Resources);
            RefreshTags();

            ServerPresenter.Resources.MergedDictionaries.Remove(_listPalette);
            var listEnabled = Enabled && _vm?.CurrentView == EnumServerViewStatus.List;
            SetIsPreview(ServerPresenter, listEnabled);
            if (listEnabled)
            {
                foreach (var entry in new[] {
                    ("BackgroundBrush", "FluentSurface"), ("BackgroundTextBrush", "FluentText"),
                    ("PrimaryMidBrush", "FluentSurface"), ("PrimaryLightBrush", "FluentHover"),
                    ("PrimaryDarkBrush", "FluentCanvas"), ("PrimaryTextBrush", "FluentText"),
                    ("AccentMidBrush", "FluentAccent"), ("AccentTextBrush", "FluentAccentText") })
                    _listPalette[entry.Item1] = Resources[entry.Item2];
                ServerPresenter.Resources.MergedDictionaries.Add(_listPalette);
            }
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            var enabled = Enabled;
            var compact = ActualWidth < 720;
            SetIsCompact(Search, compact);
            SetIsCompact(ServerPresenter, ActualWidth < 650);
            NavColumn.Width = new GridLength(enabled ? (compact ? 56 : 176) : 0);
            Sidebar.Visibility = Toolbar.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            foreach (var label in new[] { ConnectionsLabel, SettingsLabel, AboutLabel, AppearanceLabel })
                label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            Tags.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            Shell.Background = enabled ? (Brush)Resources["FluentCanvas"] : Brushes.Transparent;
            ListSurface.Margin = enabled ? new Thickness(0, 0, 16, 16) : new Thickness(0);
            ListSurface.CornerRadius = new CornerRadius(enabled ? 8 : 0);
            ListSurface.Padding = new Thickness(enabled ? 8 : 0);
            ListSurface.BorderThickness = new Thickness(enabled ? 1 : 0);
            ListSurface.BorderBrush = (Brush)Resources["FluentStroke"];
            ListSurface.Background = enabled ? (Brush)Resources["FluentSurface"] : Brushes.Transparent;
            if (Window.GetWindow(this) is MainWindowView main)
                main.ApplyFluentChrome(_vm?.IsFluentPreview == true, (Brush)Resources["FluentCanvas"],
                    (Brush)Resources["FluentText"], (Brush)Resources["FluentHover"]);
        }

        private void OpenMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.DataContext = DataContext;
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.SetResourceReference(Control.BackgroundProperty, "FluentSurface");
                menu.SetResourceReference(Control.ForegroundProperty, "FluentText");
                // Popup resource lookup does not inherit the visual parent's local palette.
                foreach (var key in new[] { "FluentSurface", "FluentText", "FluentAccent", "FluentMuted", "FluentHover", "FluentStroke" })
                    menu.Resources[key] = Resources[key];
                menu.IsOpen = true;
            }
        }

        private void TagMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            // Context menus live outside the sidebar's visual tree.
            foreach (var key in new[] { "FluentSurface", "FluentText", "FluentAccent", "FluentMuted", "FluentHover", "FluentStroke" })
                menu.Resources[key] = Resources[key];
        }

        private void SearchKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _vm == null) return;
            var filter = TagAndKeywordEncodeHelper.DecodeKeyword(_vm.MainFilterString);
            _vm.SetMainFilterString(filter.KeyWords.Count == 0 ? null : filter.TagFilterList, null);
        }
    }
}
