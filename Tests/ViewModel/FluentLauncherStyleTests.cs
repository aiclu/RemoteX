using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using _1RM;
using _1RM.Model;
using _1RM.View;
using _1RM.View.Launcher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteX.UiTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class FluentLauncherStyleTests
{
    public TestContext TestContext { get; set; } = null!;
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => {
            var previous = IoC.GetByType;
            try { RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle); IoC.GetByType = (_, _) => null; action(); }
            catch (Exception ex) { failure = ex; }
            finally { IoC.GetByType = previous; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(50)), "Launcher layout must not hang.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;
    private static void State(UIElement element, string name, bool value)
    {
        var key = (DependencyPropertyKey)typeof(UIElement).GetField(name + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        element.SetValue(key, value); LauncherFixture.Drain();
    }
    [TestMethod]
    public void ActualResultTemplateUsesPaleSelectionHoverAndIndependentFocus() => Sta(() =>
    {
        using var fixture = new LauncherFixture();
        foreach (var (dark, highContrast) in new[] { (false, false), (true, false), (false, true) })
        {
            fixture.Appearance(dark, highContrast); fixture.Prepare();
            var item = fixture.Result(0);
            var surface = (Border)item.Template.FindName("Surface", item);
            Assert.AreEqual(ColorOf((Brush)fixture.Results.FindResource("FluentSelection")), ColorOf(surface.Background));
            var indicator = (Border)item.Template.FindName("SelectionIndicator", item);
            Assert.AreEqual(3.0, indicator.ActualWidth, 0.01);
            Assert.AreEqual(Visibility.Visible, indicator.Visibility);
            var title = LauncherFixture.Descendants<LauncherHighlightedText>(item).Single(x => x.Name == "ResultTitle");
            Assert.AreEqual(ColorOf((Brush)fixture.Results.FindResource("FluentSelectionText")), ColorOf(title.Foreground));
            var other = fixture.Result(1); var otherSurface = (Border)other.Template.FindName("Surface", other);
            State(other, "IsMouseOver", true);
            Assert.AreEqual(ColorOf((Brush)fixture.Results.FindResource("FluentHover")), ColorOf(otherSurface.Background));
            State(other, "IsKeyboardFocusWithin", true);
            Assert.AreEqual(ColorOf((Brush)fixture.Results.FindResource("FluentText")), ColorOf(otherSurface.BorderBrush));
            State(other, "IsMouseOver", false); State(other, "IsKeyboardFocusWithin", false);
            fixture.ResultList.SelectedIndex = 1; fixture.Layout();
            Assert.AreEqual(Visibility.Collapsed, indicator.Visibility);
            fixture.ResultList.SelectedIndex = 0;
        }
    });
    [TestMethod]
    public void FiltersDoNotCoverSearchAndInputInstanceAndCaretSurviveRelayout() => Sta(() =>
    {
        using var fixture = new LauncherFixture(); fixture.Appearance(); fixture.Prepare();
        var search = fixture.Search;
        Assert.AreEqual(Visibility.Collapsed, fixture.FilterStrip.Visibility);
        search.Text = "#研发 服务器"; search.CaretIndex = 5;
        fixture.Model.TagFilters.Add(TagFilter.Create("研发环境很长的标签", TagFilter.FilterType.Included));
        fixture.Model.TagFilters.Add(TagFilter.Create("排除", TagFilter.FilterType.Excluded));
        foreach (var font in new[] { 10.0, 13, 20 })
        {
            fixture.Prepare(font, 340, 650);
            Assert.AreSame(search, fixture.Search); Assert.AreEqual(5, search.CaretIndex);
            Assert.AreEqual(Visibility.Visible, fixture.FilterStrip.Visibility);
            Assert.IsTrue(fixture.FilterStrip.TranslatePoint(new Point(), fixture.Results).Y >= search.TranslatePoint(new Point(0, search.ActualHeight), fixture.Results).Y - 0.1);
            Assert.IsTrue(fixture.ResultList.TranslatePoint(new Point(), fixture.Results).Y >= fixture.FilterStrip.TranslatePoint(new Point(0, fixture.FilterStrip.ActualHeight), fixture.Results).Y - 0.1);
        }
        fixture.Model.TagFilters.Clear(); fixture.Prepare();
        Assert.AreEqual(Visibility.Collapsed, fixture.FilterStrip.Visibility);
    });
    [TestMethod]
    public void HiddenAccountsHaveNeitherTextNorTooltipsInResultsAndActionHeader() => Sta(() =>
    {
        using var fixture = new LauncherFixture(); fixture.Appearance(); fixture.Model.ShowCredentials = false; fixture.Prepare();
        foreach (var content in LauncherFixture.Descendants<LauncherHighlightedText>(fixture.Results).Where(x => x.Name == "ResultSubtitle"))
        {
            Assert.AreEqual(Visibility.Collapsed, content.Visibility); Assert.IsNull(content.Title); Assert.IsNull(content.ToolTip);
        }
        fixture.Prepare(mode: "actions");
        var subtitle = (TextBlock)fixture.Results.FindName("ActionSubtitle");
        Assert.AreEqual(Visibility.Collapsed, subtitle.Visibility); Assert.AreEqual("", subtitle.Text); Assert.IsNull(subtitle.ToolTip);
        fixture.Model.ShowCredentials = true; fixture.Prepare(mode: "actions");
        Assert.AreEqual(fixture.Model.SelectedItem!.SubTitle, subtitle.Text);
        fixture.Model.ShowCredentials = false; fixture.Prepare();
        Assert.AreEqual("", subtitle.Text); Assert.IsNull(subtitle.ToolTip);
    });
    [TestMethod]
    public void LongHighlightedTitleUsesOneEllipsizedTextBlockWithSameMatchRanges() => Sta(() =>
    {
        var vm = new ServerTitleViewModel("开发 Development 很长的连接标题");
        var flags = vm.Text.Select((_, i) => i < 2 || i is >= 5 and <= 8).ToList(); vm.HighLight(flags);
        var text = new LauncherHighlightedText { Title = vm };
        Assert.AreEqual(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.AreEqual(vm.Text, string.Concat(text.Inlines.Cast<Run>().Select(x => x.Text)));
        var expanded = text.Inlines.Cast<Run>().SelectMany(run => Enumerable.Repeat(run.TextDecorations?.Count > 0, run.Text.Length)).ToList();
        CollectionAssert.AreEqual(flags, expanded);
        Assert.IsInstanceOfType<StackPanel>(vm.DisplayNameControl);
        vm.UnHighLightAll(); Assert.AreEqual(Visibility.Collapsed, vm.HighlightVisibility);
    });
    [TestMethod]
    public void ThousandsOfResultsStayVirtualizedAndScrollToLastItem() => Sta(() =>
    {
        using var fixture = new LauncherFixture(1200); fixture.Appearance(); fixture.Prepare();
        Assert.IsTrue(LauncherFixture.Descendants<ListBoxItem>(fixture.ResultList).Count() < 60);
        var scroll = LauncherFixture.Descendants<ScrollViewer>(fixture.ResultList).First();
        Assert.IsTrue(scroll.CanContentScroll); Assert.IsTrue(scroll.ScrollableHeight > 0);
        scroll.ScrollToEnd(); fixture.Layout();
        Assert.IsTrue(scroll.VerticalOffset > 0);
        Assert.IsNotNull(fixture.ResultList.ItemContainerGenerator.ContainerFromIndex(1199));
        Assert.IsTrue(LauncherFixture.Descendants<ListBoxItem>(fixture.ResultList).Count() < 60);
        fixture.Prepare(mode: "quick");
        LauncherFixture.Descendants<ScrollViewer>(fixture.QuickList).First().ScrollToEnd(); fixture.Layout();
        Assert.IsNotNull(fixture.QuickList.ItemContainerGenerator.ContainerFromIndex(1199));
    });
    [TestMethod]
    public void HighlightSubscriptionsReleaseOnUnloadAndRecycledTitleReplacement() => Sta(() =>
    {
        var first = new ServerTitleViewModel("first title");
        var second = new ServerTitleViewModel("second title");
        var text = new LauncherHighlightedText { Title = first };
        text.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        first.HighLight(first.Text.Select((_, i) => i < 2).ToList());
        Assert.AreEqual(2, text.Inlines.Count);
        text.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        first.UnHighLightAll();
        Assert.AreEqual(2, text.Inlines.Count, "Unloaded rows must not receive notifications.");
        text.Title = second;
        text.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        first.HighLight(first.Text.Select(_ => true).ToList());
        Assert.AreEqual(second.Text, new TextRange(text.ContentStart, text.ContentEnd).Text);
        second.HighLight(second.Text.Select((_, i) => i < 3).ToList());
        Assert.AreEqual(2, text.Inlines.Count);
        text.Title = null;
        Assert.AreEqual(0, text.Inlines.Count);
        text.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });
    [TestMethod]
    public void TenToTwentyDipFontsFitTwoLinesAndKeepMetadataInsideTheRow() => Sta(() =>
    {
        using var fixture = new LauncherFixture(); fixture.Appearance();
        foreach (var font in new[] { 10.0, 13, 16, 20 })
        foreach (var workWidth in new[] { 1920.0, 320 })
        {
            fixture.Prepare(font, workWidth);
            var row = fixture.Result(0);
            var title = LauncherFixture.Descendants<LauncherHighlightedText>(row).Single(x => x.Name == "ResultTitle");
            var subtitle = LauncherFixture.Descendants<LauncherHighlightedText>(row).Single(x => x.Name == "ResultSubtitle");
            Assert.AreEqual(font, title.FontSize, 0.01);
            Assert.AreEqual(FontWeights.SemiBold, title.FontWeight);
            Assert.AreEqual(TextTrimming.CharacterEllipsis, title.TextTrimming);
            Assert.IsTrue(title.ActualHeight + subtitle.ActualHeight <= row.ActualHeight - 8, "Two text lines need vertical breathing room.");
            Assert.AreEqual(title.TranslatePoint(new Point(), row).X, subtitle.TranslatePoint(new Point(), row).X, 0.01);
            foreach (var text in LauncherFixture.Descendants<TextBlock>(row))
                Assert.IsTrue(text.TranslatePoint(new Point(text.ActualWidth, 0), row).X <= row.ActualWidth + 0.1, "No title or metadata may overflow horizontally.");
        }
    });
    [TestMethod]
    public void AllLauncherSurfacesRenderInBothThemesAndThreeScales() => Sta(() =>
    {
        using var fixture = new LauncherFixture();
        var directory = Path.Combine(TestContext.TestRunResultsDirectory!, "launcher"); Directory.CreateDirectory(directory);
        foreach (var dark in new[] { false, true })
        foreach (var mode in new[] { "results", "actions", "quick", "note" })
        {
            fixture.Appearance(dark); fixture.Prepare(font: mode == "actions" ? 20 : 13, mode: mode, note: mode == "note");
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            {
                var image = new RenderTargetBitmap((int)Math.Ceiling(fixture.View.ActualWidth * scale), (int)Math.Ceiling(fixture.View.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                image.Render(fixture.View);
                var path = Path.Combine(directory, $"{mode}-{(dark ? "dark" : "light")}-{scale * 100:0}.png");
                using var file = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(file); TestContext.AddResultFile(path);
            }
        }
        fixture.Prepare(fluent: false);
        Assert.IsNotNull(fixture.Result(0).Template.FindName("GridHighLight", fixture.Result(0)), "Classic template must be restored.");
        Assert.AreEqual(40.0, FluentLauncher.GetRowHeight(fixture.View));
    });
}
