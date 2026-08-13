using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Shawn.Utils.WpfResources.Theme.AttachProperty
{
    public static class TextBoxAttachProperty
    {
        private const int CaretIndexPropertyDefault = int.MinValue;

        public static void SetCaretIndex(DependencyObject dependencyObject, int i)
        {
            dependencyObject.SetValue(CaretIndexProperty, i);
        }

        public static int GetCaretIndex(DependencyObject dependencyObject)
        {
            return (int)dependencyObject.GetValue(CaretIndexProperty);
        }

        public static readonly DependencyProperty CaretIndexProperty =
            DependencyProperty.RegisterAttached(
                "CaretIndex",
                typeof(int),
                typeof(TextBoxAttachProperty),
                new FrameworkPropertyMetadata(
                    CaretIndexPropertyDefault,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    CaretIndexChanged));

        private static void CaretIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is not TextBox textBox || eventArgs.OldValue is not int oldValue || eventArgs.NewValue is not int newValue)
            {
                return;
            }

            if (oldValue == CaretIndexPropertyDefault && newValue != CaretIndexPropertyDefault)
            {
                textBox.SelectionChanged += SelectionChangedForCaretIndex;
            }
            else if (oldValue != CaretIndexPropertyDefault && newValue == CaretIndexPropertyDefault)
            {
                textBox.SelectionChanged -= SelectionChangedForCaretIndex;
            }

            if (newValue != textBox.CaretIndex)
            {
                textBox.CaretIndex = newValue;
            }
        }

        private static void SelectionChangedForCaretIndex(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is TextBox textBox)
            {
                SetCaretIndex(textBox, textBox.CaretIndex);
            }
        }
    }
}
