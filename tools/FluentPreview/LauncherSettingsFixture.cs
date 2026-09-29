using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using _1RM.Service;
using _1RM.View.Settings.Launcher;

namespace RemoteX.UiTesting;

// Actual compiled settings view; no ConfigurationService, persisted settings or registered hotkeys.
public sealed class LauncherSettingsFixture
{
    public sealed class Model
    {
        public bool LauncherEnabled { get; set; } = true;
        public bool ShowCredentials { get; set; } = true;
        public bool AllowSaveInfoInQuickConnect { get; set; }
        public HotkeyModifierKeys LauncherHotKeyModifiers { get; set; } = _1RM.Service.HotkeyModifierKeys.Alt;
        public Key LauncherHotKeyKey { get; set; } = Key.M;
        public Dictionary<HotkeyModifierKeys, string> HotkeyModifierKeys => ConverterHotkeyModifierKeys.HotkeyModifierKeys;
        public List<MatchProviderInfo> AvailableMatcherProviders { get; } = KeywordMatchService.GetMatchProviderInfos();
    }

    public Model Data { get; } = new();
    public LauncherSettingView Page { get; }

    public LauncherSettingsFixture() => Page = new LauncherSettingView { DataContext = Data };

    public void CheckScrolling(bool leaveAtBottom)
    {
        var scroll = (ScrollViewer)Page.FindName("LauncherSettingsScroll");
        var options = (ItemsControl)Page.FindName("MatcherOptions");
        var editable = Descendants<CheckBox>(options).First(c => c.IsEnabled);
        var description = Descendants<TextBlock>(options).First(t => t.Text == Data.AvailableMatcherProviders[0].Title2);
        var footer = Descendants<CheckBox>(Page).Single(c => c.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == nameof(Model.AllowSaveInfoInQuickConnect));
        var modifiers = Data.LauncherHotKeyModifiers;
        var selectedMatchers = Data.AvailableMatcherProviders.Select(p => p.Enabled).ToArray();
        Require(scroll.ScrollableHeight > 0, "Short settings window should need scrolling.");
        Require(!Descendants<ScrollViewer>(options).Any(), "Matcher checkboxes must not own a scroll viewer.");

        void Layout()
        {
            Page.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Page.UpdateLayout();
        }
        void Wheel(UIElement origin, int delta)
        {
            origin.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.MouseWheelEvent });
            Layout();
        }
        foreach (UIElement origin in new UIElement[] { editable, description, options })
        {
            scroll.ScrollToTop(); Layout();
            Wheel(origin, -120);
            Require(scroll.VerticalOffset > 0, "Wheel over checkbox, description and matcher whitespace must scroll the page.");
        }
        for (var i = 0; i < 200 && scroll.VerticalOffset < scroll.ScrollableHeight; i++) Wheel(editable, -120);
        Require(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 0.01, "Wheel should reach the bottom.");
        var footerBounds = footer.TransformToAncestor(scroll).TransformBounds(new Rect(footer.RenderSize));
        Require(footerBounds.Top >= 0 && footerBounds.Bottom <= scroll.ViewportHeight + 0.01, "Quick Connect option should be visible at the bottom.");
        for (var i = 0; i < 200 && scroll.VerticalOffset > 0; i++) Wheel(editable, 120);
        Require(scroll.VerticalOffset == 0, "Wheel should return to the top.");
        Require(modifiers == Data.LauncherHotKeyModifiers && Data.LauncherHotKeyKey == Key.M, "Scrolling must not alter hotkeys.");
        Require(selectedMatchers.SequenceEqual(Data.AvailableMatcherProviders.Select(p => p.Enabled)), "Scrolling must not toggle matcher options.");
        if (leaveAtBottom) { scroll.ScrollToBottom(); Layout(); }
        Console.WriteLine($"PASS: launcher settings wheel, checkbox/description/whitespace, both directions, Quick Connect reachable, hotkeys unchanged; viewport={scroll.ViewportHeight:0.##}, extent={scroll.ExtentHeight:0.##}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
