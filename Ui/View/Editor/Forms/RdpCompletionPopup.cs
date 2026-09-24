using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;

namespace _1RM.View.Editor.Forms
{
    internal static class RdpCompletionPopup
    {
        internal static bool CanOpen(bool visible, bool focused, int count) => visible && focused && count > 0;

        internal static Size GetSize(double ownerWidth, double ownerHeight) =>
            new Size(Math.Max(1, Math.Min(420, ownerWidth - 32)), Math.Max(1, Math.Min(240, ownerHeight * 0.4)));

        internal static CompletionWindow? Show(IEnumerable<string> completions, TextArea area, Action<CompletionWindow> closed)
        {
            var entries = completions.ToArray();
            var owner = Window.GetWindow(area);
            if (owner == null || !CanOpen(area.IsVisible, area.IsKeyboardFocusWithin, entries.Length)) return null;
            var size = GetSize(owner.ActualWidth, owner.ActualHeight);
            var popup = new CompletionWindow(area)
            {
                CloseAutomatically = true, CloseWhenCaretAtBeginning = true,
                ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None,
                Width = size.Width, MaxWidth = size.Width, MinWidth = 0,
                Height = size.Height, MaxHeight = size.Height, MinHeight = 0,
                FontFamily = area.FontFamily, FontSize = area.FontSize,
                Background = area.TryFindResource("BackgroundBrush") as Brush ?? Brushes.White,
                Foreground = area.TryFindResource("BackgroundTextBrush") as Brush ?? Brushes.Black
            };
            if (FluentPage.GetActive(area))
            {
                // A completion window has a separate resource tree; opt it into the local palette.
                FluentPage.SetScope(popup, true);
                var fallback = new ResourceDictionary();
                foreach (var key in new[] { "FluentSurface", "FluentText", "FluentStroke", "FluentHover", "FluentAccent", "FluentAccentText" })
                    if (area.TryFindResource(key) is Brush brush) fallback[key] = brush;
                popup.Resources.MergedDictionaries.Add(fallback);
                popup.SetResourceReference(Control.BackgroundProperty, "FluentSurface");
                popup.SetResourceReference(Control.ForegroundProperty, "FluentText");
                popup.SetResourceReference(Control.BorderBrushProperty, "FluentStroke");
                popup.BorderThickness = new Thickness(1);
                popup.CompletionList.SetResourceReference(Control.BackgroundProperty, "FluentSurface");
                popup.CompletionList.SetResourceReference(Control.ForegroundProperty, "FluentText");
                var list = popup.CompletionList.ListBox;
                list.SetResourceReference(Control.BackgroundProperty, "FluentSurface");
                list.SetResourceReference(Control.ForegroundProperty, "FluentText");
                var style = new Style(typeof(ListBoxItem));
                style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
                style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28.0));
                style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("FluentText")));
                var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("FluentAccent")));
                selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("FluentAccentText")));
                style.Triggers.Add(selected);
                list.ItemContainerStyle = style;
            }
            foreach (var entry in entries) popup.CompletionList.CompletionData.Add(new RdpFileSettingCompletionData(entry));

            var isClosed = false;
            void Close() { if (!isClosed) popup.Close(); }
            void KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Close(); e.Handled = true; } }
            void OutsideClick(object sender, MouseButtonEventArgs e) { if (!area.IsMouseOver) Close(); }
            void Unloaded(object sender, RoutedEventArgs e) => Close();
            void Deactivated(object? sender, EventArgs e) => Close();
            void Resized(object sender, SizeChangedEventArgs e) => Close();
            void Scroll(object sender, ScrollChangedEventArgs e) { if (e.VerticalChange != 0 || e.HorizontalChange != 0) Close(); }
            void LostFocus(object sender, KeyboardFocusChangedEventArgs e) => area.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!area.IsKeyboardFocusWithin && !popup.IsKeyboardFocusWithin) Close();
            }), DispatcherPriority.Input);
            owner.PreviewKeyDown += KeyDown;
            popup.PreviewKeyDown += KeyDown;
            owner.PreviewMouseDown += OutsideClick;
            owner.Deactivated += Deactivated;
            owner.LocationChanged += Deactivated;
            owner.SizeChanged += Resized;
            owner.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scroll));
            area.Unloaded += Unloaded;
            area.LostKeyboardFocus += LostFocus;
            popup.Closed += (_, _) =>
            {
                isClosed = true;
                owner.PreviewKeyDown -= KeyDown;
                popup.PreviewKeyDown -= KeyDown;
                owner.PreviewMouseDown -= OutsideClick;
                owner.Deactivated -= Deactivated;
                owner.LocationChanged -= Deactivated;
                owner.SizeChanged -= Resized;
                owner.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scroll));
                area.Unloaded -= Unloaded;
                area.LostKeyboardFocus -= LostFocus;
                FluentPage.SetScope(popup, false);
                closed(popup);
            };
            try
            {
                popup.Show();
                if (entries.Length == 1) popup.CompletionList.SelectItem(entries[0]);
                return isClosed ? null : popup;
            }
            catch { Close(); throw; }
        }
    }
}
