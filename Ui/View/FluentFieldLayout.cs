using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace _1RM.View
{
    // Opt-in only for form rows, not arbitrary grids, lists or control templates.
    public static class FluentFieldLayout
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(FluentFieldLayout), new PropertyMetadata(false, Changed));
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
        public static readonly DependencyProperty FlattenRowsProperty = DependencyProperty.RegisterAttached(
            "FlattenRows", typeof(bool), typeof(FluentFieldLayout), new PropertyMetadata(false));
        public static bool GetFlattenRows(DependencyObject target) => (bool)target.GetValue(FlattenRowsProperty);
        public static void SetFlattenRows(DependencyObject target, bool value) => target.SetValue(FlattenRowsProperty, value);
        private static readonly DependencyProperty SavedProperty = DependencyProperty.RegisterAttached("Saved", typeof(Action), typeof(FluentFieldLayout));
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is FrameworkElement element)) return;
            element.Loaded -= Loaded;
            if ((bool)args.NewValue)
            {
                element.Loaded += Loaded;
                if (element.IsLoaded) Apply(element);
            }
            else
            {
                (element.GetValue(SavedProperty) as Action)?.Invoke();
                element.ClearValue(SavedProperty);
            }
        }
        private static void Loaded(object sender, RoutedEventArgs args) => Apply((FrameworkElement)sender);
        private static void Apply(FrameworkElement element)
        {
            if (element.GetValue(SavedProperty) != null) return;
            if (element is StackPanel stack)
            {
                var orientation = stack.Orientation;
                stack.SetCurrentValue(StackPanel.OrientationProperty, Orientation.Vertical);
                element.SetValue(SavedProperty, new Action(() => stack.SetCurrentValue(StackPanel.OrientationProperty, orientation)));
            }
            else if (element is Grid grid && (grid.RowDefinitions.Count == 0 || GetFlattenRows(grid)) && grid.ColumnDefinitions.Count > 1)
            {
                var columns = grid.ColumnDefinitions.ToArray();
                var rows = grid.RowDefinitions.ToArray();
                var children = grid.Children.Cast<UIElement>().Select(child =>
                    (child, column: Grid.GetColumn(child), span: Grid.GetColumnSpan(child), row: Grid.GetRow(child), rowSpan: Grid.GetRowSpan(child))).ToArray();
                grid.ColumnDefinitions.Clear();
                grid.RowDefinitions.Clear();
                int rowIndex = 0;
                foreach (var entry in children.OrderBy(x => x.row).ThenBy(x => x.column))
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    entry.child.SetCurrentValue(Grid.ColumnProperty, 0);
                    entry.child.SetCurrentValue(Grid.ColumnSpanProperty, 1);
                    entry.child.SetCurrentValue(Grid.RowProperty, rowIndex++);
                    entry.child.SetCurrentValue(Grid.RowSpanProperty, 1);
                }
                element.SetValue(SavedProperty, new Action(() =>
                {
                    grid.RowDefinitions.Clear();
                    foreach (var row in rows) grid.RowDefinitions.Add(row);
                    foreach (var column in columns) grid.ColumnDefinitions.Add(column);
                    foreach (var entry in children)
                    {
                        entry.child.SetCurrentValue(Grid.ColumnProperty, entry.column);
                        entry.child.SetCurrentValue(Grid.ColumnSpanProperty, entry.span);
                        entry.child.SetCurrentValue(Grid.RowProperty, entry.row);
                        entry.child.SetCurrentValue(Grid.RowSpanProperty, entry.rowSpan);
                    }
                }));
            }
        }
    }
}
