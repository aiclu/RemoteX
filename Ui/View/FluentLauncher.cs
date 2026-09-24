using System.Windows;

namespace _1RM.View
{
    public static class FluentLauncher
    {
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
