using System.Windows;
using System.Windows.Input;

namespace Shawn.Utils.Wpf.Controls
{
    /// <summary>
    /// Mouse click event to command by attach property
    /// in wpf: tabWindow:MouseMiddleClick.MouseMiddleDown="{Bind YourCmd}"
    /// ref: https://stackoverflow.com/questions/20288715/wpf-handle-mousedown-events-from-within-a-datatemplate
    /// </summary>
    public class MouseMiddleClick
    {
        public static readonly DependencyProperty MouseMiddleDownProperty =
            DependencyProperty.RegisterAttached("MouseMiddleDown", typeof(ICommand), typeof(MouseMiddleClick),
                new FrameworkPropertyMetadata(MouseMiddleDownPropertySetCallBack));

        public static void SetMouseMiddleDown(DependencyObject sender, ICommand value)
        {
            sender.SetValue(MouseMiddleDownProperty, value);
        }

        public static ICommand? GetMouseMiddleDown(DependencyObject sender)
        {
            return sender.GetValue(MouseMiddleDownProperty) as ICommand;
        }

        public static readonly DependencyProperty MouseMiddleDownParameterProperty =
            DependencyProperty.RegisterAttached("MouseMiddleDownParameter", typeof(object), typeof(MouseMiddleClick), new PropertyMetadata(null));

        public static void SetMouseMiddleDownParameter(DependencyObject sender, ICommand value)
        {
            sender.SetValue(MouseMiddleDownParameterProperty, value);
        }

        public static object? GetMouseMiddleDownParameter(DependencyObject sender)
        {
            return sender.GetValue(MouseMiddleDownParameterProperty);
        }

        private static void MouseMiddleDownPropertySetCallBack(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is UIElement element)
            {
                if (e.OldValue != null)
                {
                    element.RemoveHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(Handler));
                }
                if (e.NewValue != null)
                {
                    element.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(Handler), true);
                }
            }
        }

        private static void Handler(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton != MouseButtonState.Pressed) return;
            var element = sender as UIElement;
            var parameter = element?.GetValue(MouseMiddleDownParameterProperty);
            if (element?.GetValue(MouseMiddleDownProperty) is ICommand cmd)
            {
                if (cmd is RoutedCommand routedCmd)
                {
                    if (routedCmd.CanExecute(parameter, element))
                    {
                        routedCmd.Execute(parameter, element);
                    }
                }
                else
                {
                    if (cmd.CanExecute(parameter))
                    {
                        cmd.Execute(parameter);
                    }
                }
            }
        }
    }
}