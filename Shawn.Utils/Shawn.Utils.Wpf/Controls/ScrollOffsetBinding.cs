using System.Windows;
using System.Windows.Controls;

namespace Shawn.Utils.Wpf.Controls
{
    /*****    USAGE
     
        <ScrollViewer local:ScrollViewerBinding.VerticalOffset="{Binding ScrollVertical}">
        </ScrollViewer>

    */


    /// <summary>
    /// https://stackoverflow.com/questions/2096143/two-way-binding-of-verticaloffset-property-on-scrollviewer
    /// Attached behaviour which exposes the horizontal and vertical offset values
    /// for a ScrollViewer, permitting binding.
    /// NOTE: This code could be simplified a little by finding common code between vertical / horizontal
    /// scrollbars. However, this was not doen for clarity in the associated blog post!
    ///
    /// </summary>
    public class ScrollViewerBinding
    {
        private static readonly DependencyProperty VerticalScrollBindingProperty = DependencyProperty.RegisterAttached("VerticalScrollBinding", typeof(bool?), typeof(ScrollViewerBinding));
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "VerticalOffset",
                typeof(double),
                typeof(ScrollViewerBinding),
                new FrameworkPropertyMetadata(
                    double.NaN,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVerticalOffsetPropertyChanged));
        public static string GetVerticalOffset(DependencyObject dp)
        {
            return (string)dp.GetValue(VerticalOffsetProperty);
        }
        public static void SetVerticalOffset(DependencyObject dp, string value)
        {
            dp.SetValue(VerticalOffsetProperty, value);
        }
        private static void OnVerticalOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scrollViewer = d as ScrollViewer;
            if (scrollViewer == null)
                return;
            BindVerticalOffset(scrollViewer);
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }
        private static void BindVerticalOffset(ScrollViewer? scrollViewer)
        {
            if (scrollViewer == null || scrollViewer.GetValue(VerticalScrollBindingProperty) != null)
                return;
            scrollViewer.SetValue(VerticalScrollBindingProperty, true);
            scrollViewer.ScrollChanged += (o, args) =>
            {
                if (args.VerticalChange != 0)
                {
                    scrollViewer.SetValue(VerticalOffsetProperty, args.VerticalOffset);
                }
            };
        }





        private static readonly DependencyProperty HorizontalScrollBindingProperty = DependencyProperty.RegisterAttached("HorizontalScrollBinding", typeof(bool?), typeof(ScrollViewerBinding));
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "HorizontalOffset",
                typeof(double),
                typeof(ScrollViewerBinding),
                new FrameworkPropertyMetadata(
                    double.NaN,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnHorizontalOffsetPropertyChanged));
        public static string GetHorizontalOffset(DependencyObject dp)
        {
            return (string)dp.GetValue(HorizontalOffsetProperty);
        }
        public static void SetHorizontalOffset(DependencyObject dp, string value)
        {
            dp.SetValue(HorizontalOffsetProperty, value);
        }
        private static void OnHorizontalOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer)
                return;
            BindHorizontalOffset(scrollViewer);
            scrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
        }
        private static void BindHorizontalOffset(ScrollViewer scrollViewer)
        {
            if (scrollViewer.GetValue(HorizontalScrollBindingProperty) != null)
                return;
            scrollViewer.SetValue(HorizontalScrollBindingProperty, true);
            scrollViewer.ScrollChanged += (o, args) =>
            {
                if (args.HorizontalChange != 0)
                {
                    scrollViewer.SetValue(HorizontalOffsetProperty, args.HorizontalOffset);
                }
            };
        }

    }
}
