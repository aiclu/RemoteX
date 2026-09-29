using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using _1RM.Service;
using _1RM.View;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.ViewModel;

[TestClass]
public class FluentHomeLayoutTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle); action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void Layout(FrameworkElement element, double width)
    {
        element.Measure(new Size(width, 800)); element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    [TestMethod]
    public void ToolbarReflowsMeasuredButtonsWithoutReplacingSearchOrCaret() => Sta(() =>
    {
        var panel = new FluentHomeToolbarPanel();
        var search = new TextBox { Text = "#prime 中文", MinHeight = 36, CaretIndex = 7 };
        panel.Children.Add(search);
        for (var i = 0; i < 4; i++) panel.Children.Add(new Button { Content = "Action", Width = 72, MinHeight = 36 });
        foreach (var width in Enumerable.Repeat(new[] { 800.0, 480.0, 212.0 }, 12).SelectMany(x => x))
        {
            Layout(panel, width);
            Assert.AreEqual(width >= 560, panel.IsSingleRow);
            Assert.AreSame(search, panel.Children[0]); Assert.AreEqual(7, search.CaretIndex);
            Assert.AreEqual("#prime 中文", search.Text);
            var rectangles = panel.Children.Cast<FrameworkElement>().Select(c => c.TransformToAncestor(panel).TransformBounds(new Rect(c.RenderSize))).ToArray();
            foreach (var rect in rectangles) Assert.IsTrue(rect.Left >= 0 && rect.Right <= width + 0.01);
            for (var i = 0; i < rectangles.Length; i++)
            for (var j = i + 1; j < rectangles.Length; j++) Assert.IsFalse(rectangles[i].IntersectsWith(rectangles[j]), "Toolbar controls must not overlap.");
            Assert.AreEqual(panel.IsSingleRow ? 4 : 1, KeyboardNavigation.GetTabIndex(panel.Children[4]));
        }
    });
    [TestMethod]
    public void LongLabelsAndLargeFontsUseTheirMeasuredToolbarSize() => Sta(() =>
    {
        var panel = new FluentHomeToolbarPanel(); panel.Children.Add(new TextBox { MinHeight = 36 });
        foreach (var label in new[] { "Connection view", "Sort connections", "More operations", "Add connection" }) panel.Children.Add(new Button { Content = label, FontSize = 24, Padding = new Thickness(10, 4, 10, 4) });
        foreach (var width in new[] { 1200.0, 600.0, 212.0 })
        {
            Layout(panel, width);
            foreach (FrameworkElement child in panel.Children) Assert.IsTrue(child.ActualHeight >= child.DesiredSize.Height - 0.01);
            Assert.IsTrue(double.IsFinite(panel.DesiredSize.Height));
        }
    });
    [TestMethod]
    public void SourceAndConnectionColumnsAlignAndRestoreAfterNarrowLayout() => Sta(() =>
    {
        var header = new FluentHomeRowGrid { IsSourceHeader = true };
        var row = new FluentHomeRowGrid();
        foreach (var grid in new[] { header, row })
        {
            for (var col = 0; col < 4; col++) { var box = new Border { Height = 36 }; Grid.SetColumn(box, col); grid.Children.Add(box); }
        }
        var actions = new Border { Width = 124, Height = 32 }; Grid.SetColumn(actions, 4); Grid.SetColumnSpan(actions, 2); header.Children.Add(actions);
        var connect = new Button { Content = "Connect", MinWidth = 64 }; Grid.SetColumn(connect, 5); row.Children.Add(connect);
        foreach (var width in new[] { 700.0, 186.0, 700.0 })
        {
            Layout(header, width); Layout(row, width);
            foreach (var index in new[] { 1, 3 }) Assert.AreEqual(header.Children[index].TransformToAncestor(header).Transform(new Point()).X,
                row.Children[index].TransformToAncestor(row).Transform(new Point()).X, 0.01);
            Assert.AreEqual(width < 320 ? 1 : 0, Grid.GetRow(actions));
            Assert.AreEqual(width < 320 ? 0.0 : 60.0, header.ColumnDefinitions[4].Width.Value);
        }
    });
    private sealed class FilterState : INotifyPropertyChanged
    {
        public string SelectedTabName { get; set; } = "";
        public object? TagListViewModel { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    }
    [TestMethod]
    public void NarrowRowKeepsLongActionInsideCard() => Sta(() =>
    {
        var grid = new FluentHomeRowGrid();
        var resources = new ResourceDictionary { Source = new Uri("/RemoteX;component/Resources/Theme/FluentPreview.xaml", UriKind.Relative) };
        grid.Resources.MergedDictionaries.Add(resources);
        var button = new Button { Content = "Connect to server", FontSize = 24, MinWidth = 64, Style = (Style)resources["FluentHomeActionButton"] };
        Grid.SetColumn(button, 5); grid.Children.Add(button);
        Layout(grid, 186);
        Assert.IsTrue(button.TransformToAncestor(grid).Transform(new Point(button.ActualWidth, 0)).X <= 186);
        Assert.IsTrue(grid.ColumnDefinitions[3].ActualWidth >= 24);
        Assert.AreEqual("Connect to server", button.ToolTip);
    });
    [TestMethod]
    public void AllConnectionsHighlightDoesNotCompeteWithTagsOrManagement() => Sta(() =>
    {
        var workspace = new FluentWorkspace(); var state = new FilterState();
        System.Windows.Data.BindingOperations.ClearBinding((ContentControl)workspace.FindName("ServerPresenter"), Stylet.Xaml.View.ModelProperty);
        workspace.DataContext = new { ActiveServerViewModel = state };
        var button = (Button)workspace.FindName("AllConnectionsNavigation");
        button.ApplyTemplate();
        foreach (var selected in new[] { "", "prime", _1RM.View.ServerView.ServerPageViewModelBase.TAB_NONE_SELECTED, "" })
        {
            state.SelectedTabName = selected; state.Notify(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(selected == "" ? FontWeights.SemiBold : FontWeights.Normal, button.FontWeight);
        }
        state.TagListViewModel = new object(); state.Notify(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(FontWeights.Normal, button.FontWeight);
        Assert.IsNotNull(button.FocusVisualStyle);
        Assert.AreEqual(3.0, ((Border)button.Template.FindName("SelectedMark", button)).Width);
    });
    [TestMethod]
    public void HighContrastSelectionUsesSystemHighlightColors() => Sta(() =>
    {
        var resources = new ResourceDictionary(); FluentAppearanceService.ApplyPalette(resources, false, true);
        Assert.AreSame(SystemColors.HighlightBrush, resources["FluentSelection"]);
        Assert.AreSame(SystemColors.HighlightTextBrush, resources["FluentSelectionText"]);
    });
}
