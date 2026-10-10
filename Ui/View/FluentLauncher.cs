using System.Windows;

namespace _1RM.View
{
    public static class FluentLauncher
    {
        public static readonly DependencyProperty RowSelectedProperty = DependencyProperty.RegisterAttached("RowSelected", typeof(bool), typeof(FluentLauncher), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetRowSelected(DependencyObject target) => (bool)target.GetValue(RowSelectedProperty);
        public static void SetRowSelected(DependencyObject target, bool value) => target.SetValue(RowSelectedProperty, value);
        public static readonly DependencyProperty QuickModeProperty = DependencyProperty.RegisterAttached("QuickMode", typeof(bool), typeof(FluentLauncher), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetQuickMode(DependencyObject target) => (bool)target.GetValue(QuickModeProperty);
        public static void SetQuickMode(DependencyObject target, bool value) => target.SetValue(QuickModeProperty, value);
        public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached("Compact", typeof(bool), typeof(FluentLauncher), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetCompact(DependencyObject target) => (bool)target.GetValue(CompactProperty);
        public static void SetCompact(DependencyObject target, bool value) => target.SetValue(CompactProperty, value);
        public static readonly DependencyProperty FilterHeightProperty = DependencyProperty.RegisterAttached("FilterHeight", typeof(double), typeof(FluentLauncher), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.Inherits));
        public static double GetFilterHeight(DependencyObject target) => (double)target.GetValue(FilterHeightProperty);
        public static void SetFilterHeight(DependencyObject target, double value) => target.SetValue(FilterHeightProperty, value);
        public static readonly DependencyProperty IsNotePaneProperty = DependencyProperty.RegisterAttached("IsNotePane", typeof(bool), typeof(FluentLauncher), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetIsNotePane(DependencyObject target) => (bool)target.GetValue(IsNotePaneProperty);
        public static void SetIsNotePane(DependencyObject target, bool value) => target.SetValue(IsNotePaneProperty, value);
        public static readonly DependencyProperty RowHeightProperty = DependencyProperty.RegisterAttached("RowHeight", typeof(double), typeof(FluentLauncher), new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.Inherits));
        public static double GetRowHeight(DependencyObject target) => (double)target.GetValue(RowHeightProperty);
        public static void SetRowHeight(DependencyObject target, double value) => target.SetValue(RowHeightProperty, value);
        public static readonly DependencyProperty SearchHeightProperty = DependencyProperty.RegisterAttached("SearchHeight", typeof(double), typeof(FluentLauncher), new FrameworkPropertyMetadata(46.0, FrameworkPropertyMetadataOptions.Inherits));
        public static double GetSearchHeight(DependencyObject target) => (double)target.GetValue(SearchHeightProperty);
        public static void SetSearchHeight(DependencyObject target, double value) => target.SetValue(SearchHeightProperty, value);
        public static readonly DependencyProperty ActionHeightProperty = DependencyProperty.RegisterAttached("ActionHeight", typeof(double), typeof(FluentLauncher), new FrameworkPropertyMetadata(34.0, FrameworkPropertyMetadataOptions.Inherits));
        public static double GetActionHeight(DependencyObject target) => (double)target.GetValue(ActionHeightProperty);
        public static void SetActionHeight(DependencyObject target, double value) => target.SetValue(ActionHeightProperty, value);
    }
}
