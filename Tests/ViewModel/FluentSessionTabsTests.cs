using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using _1RM;
using _1RM.Service;
using _1RM.View;
using _1RM.View.Host;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteX.UiTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class FluentSessionTabsTests
{
    public TestContext TestContext { get; set; } = null!;
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => {
            var previous = IoC.GetByType;
            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                IoC.GetByType = (_, _) => null;
                action();
            }
            catch (Exception ex) { failure = ex; }
            finally { IoC.GetByType = previous; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "The isolated tab layout must not hang.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Appearance(SessionHeaderFixture fixture, bool dark = false, bool highContrast = false, double font = 13, double width = 1080)
    {
        FluentAppearanceService.ApplyPalette(fixture.View.Resources, dark, highContrast);
        fixture.View.Resources["GlobalFontSizeBody"] = font;
        fixture.View.Resources["GlobalFontFamily"] = new FontFamily("Microsoft YaHei");
        fixture.Layout(width);
        fixture.SetFluent(true);
        // Only header aliases, as in the application; the protocol content keeps its resources.
        foreach (var element in new FrameworkElement[] { fixture.Header, fixture.WindowButtons })
        {
            FluentAppearanceService.ApplyPalette(element.Resources, dark, highContrast);
            FluentAppearanceService.ApplyAliases(element.Resources);
        }
        fixture.Layout(width);
    }
    private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;
    private static void Centered(FrameworkElement element, Visual header)
    {
        var center = element.TransformToAncestor(header).Transform(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        Assert.AreEqual(15.0, center.Y, 1.0, $"{element.Name} vertical center: {center.Y}");
    }
    // Isolated input-state injection tests WPF template triggers; not a claim of desktop input testing.
    private static void State(UIElement element, string name, bool value)
    {
        var key = (DependencyPropertyKey)typeof(UIElement).GetField(name + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        element.SetValue(key, value);
        SessionHeaderFixture.Drain();
    }

    [TestMethod]
    public void WholeHeaderKeepsThirtyDipHeightAndCentersTenToTwentyDipFonts() => Sta(() =>
    {
        using var fixture = new SessionHeaderFixture();
        foreach (var font in new[] { 10.0, 12, 14, 16, 18, 20 })
        {
            Appearance(fixture, font: font);
            Assert.AreEqual(30.0, fixture.Header.ActualHeight, 0.01);
            Assert.AreEqual(90.0, fixture.ContentPanel.ActualHeight, 0.01);
            Assert.AreEqual(30.0, fixture.ContentPanel.TranslatePoint(new Point(), fixture.View).Y, 0.01);
            Assert.IsFalse(FluentPage.GetActive(fixture.ContentPanel), "Fluent must not leak into the protocol content.");
            Assert.AreEqual(20.0, fixture.Logo.ActualWidth, 0.01); Centered(fixture.Logo, fixture.Header);
            Assert.AreEqual(24.0, fixture.Add.ActualHeight, 0.01); Centered(fixture.Add, fixture.Header);
            fixture.CheckActionBounds();
            for (var i = 0; i < fixture.Items.Count; i++)
            {
                var item = fixture.Item(i);
                Assert.AreEqual(30.0, item.ActualHeight, 0.01);
                var icon = fixture.Part<Grid>(i, "TabIcon");
                Assert.AreEqual(16.0, icon.ActualWidth, 0.01); Centered(icon, fixture.Header);
                var text = fixture.Part<TextBlock>(i, "TabTitle");
                Assert.AreEqual(font, text.FontSize, 0.01);
                Assert.IsTrue(text.ActualHeight <= 30 && text.ActualHeight >= text.DesiredSize.Height - 0.01);
                Centered(text, fixture.Header);
                foreach (var name in new[] { "TabClose", "TabReconnect" })
                {
                    var button = fixture.Part<Button>(i, name);
                    Assert.AreEqual(24.0, button.ActualWidth, 0.01); Assert.AreEqual(24.0, button.ActualHeight, 0.01);
                    Centered(button, fixture.Header);
                }
                if (i == 0) continue;
                var previous = fixture.Part<Border>(i - 1, "DragablzHeaderItemBorder");
                var current = fixture.Part<Border>(i, "DragablzHeaderItemBorder");
                var gap = current.TranslatePoint(new Point(), fixture.Header).X - previous.TranslatePoint(new Point(previous.ActualWidth, 0), fixture.Header).X;
                Assert.AreEqual(4.0, gap, 1.0, "Adjacent rounded surfaces need a 4 DIP gap.");
            }
        }
    });

    [TestMethod]
    public void PaletteSelectionAndMarkerColorsStayLocalAndSwitchImmediately() => Sta(() =>
    {
        using var fixture = new SessionHeaderFixture();
        foreach (var (dark, highContrast) in new[] { (false, false), (true, false), (false, true) })
        {
            Appearance(fixture, dark, highContrast);
            var canvas = ColorOf((Brush)fixture.Header.FindResource("FluentCanvas"));
            Assert.AreEqual(canvas, ColorOf(fixture.Header.Background));
            foreach (var panel in SessionHeaderFixture.Descendants<Panel>(fixture.Header).Where(x => x.Style == fixture.View.FindResource("SessionHeaderSurface")))
                Assert.AreEqual(canvas, ColorOf(panel.Background));
            var legacy = (Border)fixture.Tabs.Template.FindName("HeaderLegacyColorBorder", fixture.Tabs);
            Assert.AreEqual(Visibility.Collapsed, legacy.Visibility);
            fixture.Tabs.SelectedIndex = 0; fixture.Layout();
            var marker = fixture.Part<Border>(0, "FluentSelectedMark");
            Assert.AreEqual(Visibility.Visible, marker.Visibility);
            Assert.AreEqual(24.0, marker.ActualWidth, 0.01); Assert.AreEqual(2.0, marker.ActualHeight, 0.01);
            Assert.AreEqual(ColorOf((Brush)fixture.Header.FindResource("FluentAccent")), ColorOf(marker.Background));
            Assert.AreEqual(fixture.Item(0).ActualWidth / 2, marker.TranslatePoint(new Point(12, 0), fixture.Item(0)).X, 0.01);
            Assert.AreEqual(ColorOf((Brush)fixture.Header.FindResource("FluentSurface")), ColorOf(fixture.Part<Border>(0, "DragablzHeaderItemBorder").Background));
            fixture.Tabs.SelectedIndex = 1; fixture.Layout();
            Assert.AreEqual(Visibility.Collapsed, marker.Visibility);
            marker = fixture.Part<Border>(1, "FluentSelectedMark");
            Assert.AreEqual(Visibility.Visible, marker.Visibility);
            Assert.AreEqual((Color)ColorConverter.ConvertFromString("#D97706"), ColorOf(marker.Background));
            Assert.AreEqual(new Thickness(1), fixture.Part<Border>(1, "DragablzHeaderItemBorder").BorderThickness);
        }
    });

    [TestMethod]
    public void HoverFocusAndDisabledStatesUseFluentResources() => Sta(() =>
    {
        using var fixture = new SessionHeaderFixture(); Appearance(fixture);
        var item = fixture.Item(2);
        var border = fixture.Part<Border>(2, "DragablzHeaderItemBorder");
        State(item, "IsMouseOver", true);
        Assert.AreEqual(ColorOf((Brush)fixture.Header.FindResource("FluentHover")), ColorOf(border.Background));
        State(item, "IsKeyboardFocusWithin", true);
        Assert.AreEqual(ColorOf((Brush)fixture.Header.FindResource("FluentAccent")), ColorOf(border.BorderBrush));
        Assert.AreEqual(new Thickness(1), border.BorderThickness);
        State(item, "IsMouseOver", false); State(item, "IsKeyboardFocusWithin", false);
        var close = fixture.Part<Button>(2, "TabClose");
        State(close, "IsKeyboardFocused", true);
        Assert.AreEqual(ColorOf((Brush)fixture.Header.FindResource("FluentAccent")), ColorOf(((Border)close.Template.FindName("Chrome", close)).BorderBrush));
        close.IsEnabled = false;
        Assert.AreEqual(0.45, close.Opacity, 0.01);
    });

    [TestMethod]
    public void FluentSuffixReservationMatchesExistingSlotsAndNeverChangesClassic()
    {
        var converter = new SessionHeaderReserveConverter();
        foreach (var fluent in new[] { false, true })
        foreach (var left in new[] { false, true })
        foreach (var launcher in new[] { false, true })
        foreach (var fullScreen in new[] { false, true })
        foreach (var closeAll in new[] { Visibility.Visible, Visibility.Hidden, Visibility.Collapsed })
        {
            var expected = !fluent ? 0 : (launcher ? 32 : 0) + (left ? 0 : 185 + (fullScreen ? 45 : 0) + (closeAll != Visibility.Collapsed ? 45 : 0));
            Assert.AreEqual((double)expected, converter.Convert(new object[] { fluent, left, launcher, fullScreen, closeAll }, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [TestMethod]
    public void OverflowSettingsCommandsAndClassicRestorationPreserveExistingBehavior() => Sta(() =>
    {
        using var fixture = new SessionHeaderFixture(16, longTitle: true);
        Appearance(fixture, width: 640);
        fixture.CheckActionBounds();
        Assert.IsTrue(fixture.Scroller.ScrollableWidth > 0);
        fixture.Scroller.ScrollToRightEnd(); fixture.Layout(640);
        Assert.IsTrue(fixture.Scroller.HorizontalOffset > 0);
        fixture.Scroller.ScrollToLeftEnd(); fixture.Layout(640);
        Assert.AreEqual(0.0, fixture.Scroller.HorizontalOffset, 0.01);
        var title = fixture.Part<TextBlock>(2, "TabTitle");
        Assert.AreEqual(250.0, title.MaxWidth, 0.01);
        Assert.IsTrue(title.ActualWidth > 0 && title.ActualWidth <= 250.01);
        Assert.AreEqual(TextTrimming.CharacterEllipsis, title.TextTrimming);
        Assert.AreEqual(fixture.Items[2].DisplayName, fixture.Part<Grid>(2, "Trapezoid").ToolTip);
        Assert.IsNotNull(fixture.Part<Thumb>(0, "PART_Thumb"));
        Assert.IsNotNull(fixture.Part<Grid>(0, "DragablzHeaderItemContent").ContextMenu);
        var close = fixture.Part<Button>(0, "TabClose");
        Assert.AreEqual("DataContext.CmdClose", BindingOperations.GetBinding(close, Button.CommandProperty)!.Path.Path);
        Assert.AreEqual(typeof(TabWindowView), BindingOperations.GetBinding(close, Button.CommandProperty)!.RelativeSource.AncestorType);
        Assert.AreEqual(fixture.Items[0].Content.ConnectionId, close.CommandParameter);
        fixture.Part<Button>(0, "TabReconnect").Command.Execute(null);
        fixture.Add.Command.Execute(null);
        Assert.AreEqual(1, fixture.Items[0].CmdReconnect.Calls); Assert.AreEqual(1, fixture.CmdInvokeLauncher.Calls);
        var rightLogoX = fixture.Logo.TranslatePoint(new Point(), fixture.Header).X;
        fixture.SettingsPage.TabWindowCloseButtonOnLeft = true; fixture.SettingsPage.Refresh(); fixture.Layout(640);
        Assert.AreEqual(HorizontalAlignment.Left, fixture.WindowButtons.HorizontalAlignment);
        fixture.CheckActionBounds();
        Assert.IsTrue(fixture.Logo.TranslatePoint(new Point(), fixture.Header).X > rightLogoX);
        fixture.SettingsPage.TabHeaderShowCloseButton = false;
        fixture.SettingsPage.TabHeaderShowReConnectButton = false;
        fixture.SettingsPage.TabHeaderShowIconButton = false;
        fixture.SettingsPage.LauncherEnabled = false;
        fixture.SettingsPage.Refresh(); fixture.Layout(640);
        Assert.AreEqual(Visibility.Collapsed, close.Visibility);
        Assert.AreEqual(Visibility.Collapsed, fixture.Part<Button>(0, "TabReconnect").Visibility);
        Assert.AreEqual(Visibility.Collapsed, fixture.Part<Border>(0, "TabIconBorder").Visibility);
        Assert.AreEqual(Visibility.Collapsed, fixture.Add.Visibility);
        fixture.SetFluent(false); fixture.Layout(640);
        Assert.AreEqual(20.0, fixture.Part<Grid>(0, "TabIcon").Width);
        Assert.AreEqual(13.0, close.Width);
        Assert.AreEqual(25.0, fixture.Add.Width); Assert.AreEqual(30.0, fixture.Add.Height);
        Assert.AreEqual(Visibility.Collapsed, fixture.Part<Border>(0, "FluentSelectedMark").Visibility);
        Assert.AreEqual(Visibility.Visible, ((Border)fixture.Tabs.Template.FindName("HeaderLegacyColorBorder", fixture.Tabs)).Visibility);
        Assert.AreEqual(30.0, fixture.Header.ActualHeight, 0.01);
        Assert.AreEqual(90.0, fixture.ContentPanel.ActualHeight, 0.01);
    });

    [TestMethod]
    public void LargeFontOverflowReservesLauncherAfterRepeatedWidthAndSideChanges() => Sta(() =>
    {
        using var fixture = new SessionHeaderFixture(20, longTitle: true);
        foreach (var left in new[] { false, true, false })
        foreach (var width in new[] { 1080.0, 600, 480, 640, 1080 })
        {
            fixture.SettingsPage.TabWindowCloseButtonOnLeft = left;
            fixture.SettingsPage.Refresh();
            Appearance(fixture, font: 20, width: width);
            fixture.CheckActionBounds();
            Assert.AreEqual(30.0, fixture.Header.ActualHeight, 0.01);
            Assert.IsTrue(fixture.Scroller.ScrollableWidth > 0);
            Centered(fixture.Add, fixture.Header);
        }
    });

    [TestMethod]
    public void ContentBordersAndProductionDragBindingsRemainUnchanged()
    {
        var source = XDocument.Load(Path.Combine(SessionHeaderFixture.FindRoot(), "Ui/View/Host/TabWindowView.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.AreEqual("5,0,5,5", source.Descendants().Single(e => (string?)e.Attribute(x + "Key") == "TabContentBorderWithColor").Value);
        Assert.AreEqual("0,0,0,0", source.Descendants().Single(e => (string?)e.Attribute(x + "Key") == "TabContentBorderWithOutColor").Value);
        Assert.IsTrue(source.Descendants().Any(e => e.Name.LocalName == "TabablzControl.InterTabController"));
        var middle = source.Descendants().Attributes().Single(a => a.Name.LocalName == "MouseMiddleClick.MouseMiddleDown");
        StringAssert.Contains(middle.Value, "DataContext.CmdClose");
        Assert.AreEqual(30.0, TabWindowView.TITLE_BAR_HEIGHT);
    }

    [TestMethod]
    public void RenderWholeHeaderLightDarkHighContrastAndOverflowAtThreeScales() => Sta(() =>
    {
        foreach (var dark in new[] { false, true })
        foreach (var scenario in new[] { "Normal", "Marked", "LargeOverflow", "HiddenButtons", "HighContrast" })
        {
            using var fixture = new SessionHeaderFixture(scenario == "LargeOverflow" ? 16 : 3, longTitle: true);
            var width = scenario == "LargeOverflow" ? 640 : 1200;
            Appearance(fixture, dark, scenario == "HighContrast", scenario == "LargeOverflow" ? 20 : 13, width);
            if (scenario == "Marked") fixture.Tabs.SelectedIndex = 1;
            if (scenario == "HiddenButtons")
            {
                fixture.SettingsPage.TabHeaderShowCloseButton = false;
                fixture.SettingsPage.TabHeaderShowReConnectButton = false;
                fixture.SettingsPage.Refresh();
            }
            if (scenario == "Normal")
            {
                State(fixture.Item(1), "IsMouseOver", true);
                State(fixture.Item(2), "IsKeyboardFocusWithin", true);
            }
            fixture.Layout(width, 60);
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            {
                var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(60 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(fixture.View);
                var path = Path.Combine(TestContext.TestResultsDirectory!, $"SessionTabs-{scenario}-{(dark ? "Dark" : "Light")}-{scale * 100:0}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var file = File.Create(path))
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                }
                TestContext.AddResultFile(path);
            }
        }
    });
}
