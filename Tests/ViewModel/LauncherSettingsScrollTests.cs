using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Service;

namespace Tests.ViewModel;

[TestClass]
public class LauncherSettingsScrollTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle); action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class Fixture
    {
        public MatchProviderInfo[] Providers { get; } = Enumerable.Range(0, 12)
            .Select(i => new MatchProviderInfo { Title1 = $"Matcher {i}", Title2 = $"Example {i}", IsEditable = i != 0, Enabled = i == 0 }).ToArray();
        public Grid Stage { get; }
        public ScrollViewer Scroll { get; }
        public ItemsControl Options { get; }
        public CheckBox Footer { get; } = new() { Content = "Quick Connect", Height = 40 };

        public Fixture(double fontSize = 13)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "RemoteX.sln"))) root = root.Parent;
            var document = XDocument.Load(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
                "Ui/View/Settings/Launcher/LauncherSettingView.xaml"));
            var items = document.Descendants().Single(e => (string?)e.Attribute("ItemsSource") == "{Binding AvailableMatcherProviders}");
            // Load the real matcher template/host without application startup, configuration or hotkey registration.
            var gridXml = new XElement(items.Parent!);
            foreach (var ns in document.Root!.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                var value = ns.Value;
                if (value.StartsWith("clr-namespace:") && !value.Contains(";assembly=")) value += ";assembly=RemoteX";
                gridXml.SetAttributeValue(ns.Name, value);
            }
            var grid = (Grid)XamlReader.Parse(gridXml.ToString());
            grid.DataContext = new { AvailableMatcherProviders = Providers };
            grid.Resources["GlobalFontSizeSmall"] = fontSize - 1;
            grid.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, fontSize);
            var content = new StackPanel();
            content.Children.Add(new Border { Height = 80 }); content.Children.Add(grid); content.Children.Add(Footer);
            Scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Stage = new Grid { Width = 760, Height = 340 }; Stage.Children.Add(Scroll);
            Layout();
            Options = Descendants<ItemsControl>(grid).Single(c => ReferenceEquals(c.ItemsSource, Providers));
        }

        public void Layout()
        {
            Stage.Measure(new Size(760, 340)); Stage.Arrange(new Rect(0, 0, 760, 340)); Stage.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Stage.UpdateLayout();
        }
        public void Wheel(UIElement origin, int delta)
        {
            origin.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.MouseWheelEvent });
            Layout();
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WheelOverMatcherContentReachesPageScrollViewer(bool overDescription) => Sta(() =>
    {
        var fixture = new Fixture();
        Assert.IsTrue(fixture.Scroll.ScrollableHeight > 0);
        UIElement origin = overDescription
            ? Descendants<TextBlock>(fixture.Options).First(t => t.Text == fixture.Providers[1].Title2)
            : Descendants<CheckBox>(fixture.Options).First(c => ReferenceEquals(c.DataContext, fixture.Providers[1]));
        fixture.Wheel(origin, -120);
        Assert.IsTrue(fixture.Scroll.VerticalOffset > 0, "The matcher host must not swallow wheel input when it has no independent scroll range.");
    });

    [TestMethod]
    public void MatcherOptionsKeepCheckboxBindingsWithoutIndependentScrolling() => Sta(() =>
    {
        var fixture = new Fixture();
        Assert.AreEqual(0, Descendants<ScrollViewer>(fixture.Options).Count());
        Assert.AreEqual(fixture.Providers.Length, fixture.Options.Items.Count);
        var checkboxes = Descendants<CheckBox>(fixture.Options).ToArray();
        Assert.IsFalse(checkboxes[0].IsEnabled); Assert.AreEqual(true, checkboxes[0].IsChecked);
        var editable = checkboxes.Single(c => ReferenceEquals(c.DataContext, fixture.Providers[1]));
        editable.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true); fixture.Layout();
        Assert.IsTrue(fixture.Providers[1].Enabled);
        fixture.Providers[1].Enabled = false; fixture.Layout();
        Assert.AreEqual(false, editable.IsChecked);
    });

    [DataTestMethod]
    [DataRow(13.0)]
    [DataRow(24.0)]
    public void PageWheelReachesFooterAndReturnsToTop(double fontSize) => Sta(() =>
    {
        var fixture = new Fixture(fontSize);
        var origin = Descendants<CheckBox>(fixture.Options).First(c => c.IsEnabled);
        for (var i = 0; i < 100 && fixture.Scroll.VerticalOffset < fixture.Scroll.ScrollableHeight; i++) fixture.Wheel(origin, -120);
        Assert.AreEqual(fixture.Scroll.ScrollableHeight, fixture.Scroll.VerticalOffset, 0.01);
        var bounds = fixture.Footer.TransformToAncestor(fixture.Scroll).TransformBounds(new Rect(fixture.Footer.RenderSize));
        Assert.IsTrue(bounds.Top >= 0 && bounds.Bottom <= fixture.Scroll.ActualHeight + 0.01, "Quick Connect must be reachable in the viewport.");
        for (var i = 0; i < 100 && fixture.Scroll.VerticalOffset > 0; i++) fixture.Wheel(origin, 120);
        Assert.AreEqual(0.0, fixture.Scroll.VerticalOffset);
    });
}
