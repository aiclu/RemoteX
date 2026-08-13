using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Shawn.Utils.WpfResources.Theme.AttachProperty
{
    public static class ContextMenu
    {
        public static readonly DependencyProperty CloseWhenMouseLeaveProperty =
            DependencyProperty.RegisterAttached("CloseWhenMouseLeave", typeof(bool?), typeof(ContextMenu), new FrameworkPropertyMetadata(CloseWhenMouseLeaveChanged) { BindsTwoWayByDefault = true });

        public static bool? GetCloseWhenMouseLeave(DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return (bool?)element.GetValue(CloseWhenMouseLeaveProperty);
        }

        public static void SetCloseWhenMouseLeave(DependencyObject element, bool? value)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            element.SetValue(CloseWhenMouseLeaveProperty, value);
        }

        private static void CloseWhenMouseLeaveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is not System.Windows.Controls.ContextMenu contextMenu || eventArgs.NewValue is not bool flag)
            {
                return;
            }

            if (flag)
            {
                contextMenu.MouseLeave += ContextMenuOnMouseLeave;
            }
            else
            {
                contextMenu.MouseLeave -= ContextMenuOnMouseLeave;
            }
        }

        private static void ContextMenuOnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.ContextMenu contextMenu)
                contextMenu.IsOpen = false;
        }
    }
}
