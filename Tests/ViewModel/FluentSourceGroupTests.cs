using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Service;
using _1RM.View;
using _1RM.View.ServerView;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass]
public class FluentSourceGroupTests
{
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle); action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static ResourceDictionary Resources() => new() { Source = new Uri("/RemoteX;component/Resources/Theme/FluentServerViews.xaml", UriKind.Relative) };
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static Grid Layout(FrameworkElement child, double width = 360)
    {
        var grid = new Grid { Width = width, Height = 400 };
        grid.Children.Add(child); grid.Measure(new Size(width, 400)); grid.Arrange(new Rect(0, 0, width, 400)); grid.UpdateLayout(); Drain();
        return grid;
    }
    public sealed class State : INotifyPropertyChanged
    {
        private bool _expanded;
        public bool Expanded { get => _expanded; set { _expanded = value; PropertyChanged?.Invoke(this, new(nameof(Expanded))); } }
        private object? _tagListViewModel;
        public object? TagListViewModel { get => _tagListViewModel; set { _tagListViewModel = value; PropertyChanged?.Invoke(this, new(nameof(TagListViewModel))); } }
        public bool IsAddToolTipShow => true;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [TestMethod]
    public void TagManagementReplacesConnectionsAndRemovesOnlyFluentListSpacing() => Sta(() =>
    {
        var state = new State();
        var panel = new Grid { Style = (Style)Resources()["ServerConnectionsPanel"], DataContext = state };
        foreach (var managing in new[] { false, true, false, true })
        {
            state.TagListViewModel = managing ? new object() : null; Drain();
            Assert.AreEqual(managing ? Visibility.Collapsed : Visibility.Visible, panel.Visibility);
        }
        var body = new Grid { Style = (Style)Resources()["TagManagementBody"] };
        var spacer = new RowDefinition { Style = (Style)Resources()["TagManagementLegacySpacer"] };
        body.RowDefinitions.Add(spacer); Layout(body);
        foreach (var fluentList in new[] { false, true, false, true })
        {
            FluentWorkspace.SetIsPreview(body, fluentList); Drain(); body.UpdateLayout();
            Assert.AreEqual(new Thickness(0, fluentList ? 0 : 21, 0, 0), body.Margin);
            Assert.AreEqual(fluentList ? 0.0 : 20.0, spacer.Height.Value);
        }
    });

    [TestMethod]
    public void TagManagementRestoresPanelAndHidesEmptyConnectionAction() => Sta(() =>
    {
        var workspace = new FluentWorkspace();
        var presenter = (ContentControl)workspace.FindName("ServerPresenter");
        BindingOperations.ClearBinding(presenter, Stylet.Xaml.View.ModelProperty);
        var state = new State();
        workspace.DataContext = new { IsFluentPreview = true, IsShownList = true, ActiveServerViewModel = state };
        var page = new Border(); presenter.Content = page;
        FluentWorkspace.SetHasSourceGroups(page, true);
        var surface = (Border)workspace.FindName("ListSurface");
        var emptyAction = (Button)workspace.FindName("EmptyConnectionsAdd");
        foreach (var managing in new[] { false, true, false, true })
        {
            state.TagListViewModel = managing ? new object() : null; Drain();
            Assert.AreEqual(new Thickness(managing ? 1 : 0), surface.BorderThickness);
            Assert.AreEqual(new Thickness(managing ? 8 : 0), surface.Padding);
            Assert.AreEqual(managing ? Visibility.Collapsed : Visibility.Visible, emptyAction.Visibility);
        }
    });

    [TestMethod]
    public void ExpanderKeepsFullWidthHeaderAndTwoWayExpansionAtLargeFont() => Sta(() =>
    {
        var state = new State();
        var header = new TextBlock { Text = "Long source name", FontSize = 28 };
        var expander = new Expander { Header = header, Content = new Border { Height = 80 },
            Template = (ControlTemplate)Resources()["FluentSourceExpanderTemplate"], VerticalAlignment = VerticalAlignment.Top };
        expander.SetBinding(Expander.IsExpandedProperty, new Binding(nameof(State.Expanded)) { Source = state, Mode = BindingMode.TwoWay });
        var stage = Layout(expander);
        var toggle = (ToggleButton)expander.Template.FindName("HeaderSite", expander);
        Assert.IsTrue(toggle.ActualWidth > 300, "Do not inherit the legacy toggle switch's 36 DIP width.");
        Assert.IsTrue(toggle.ActualHeight >= 44);
        Assert.IsTrue(header.ActualWidth > 250);
        var body = (ContentPresenter)expander.Template.FindName("ExpandSite", expander);
        var collapsedHeight = expander.ActualHeight;
        var provider = (IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle);
        provider.Toggle(); Drain(); stage.UpdateLayout();
        Assert.IsTrue(state.Expanded); Assert.AreEqual(Visibility.Visible, body.Visibility);
        Assert.IsTrue(expander.ActualHeight > collapsedHeight);
        state.Expanded = false; Drain(); stage.UpdateLayout();
        Assert.AreEqual(false, toggle.IsChecked); Assert.AreEqual(Visibility.Collapsed, body.Visibility);
        Assert.AreEqual(collapsedHeight, expander.ActualHeight, 0.01);
        Assert.IsFalse(Validation.GetHasError(expander));
    });

    [TestMethod]
    public void SourceActionHasAccessibleSizeAndDoesNotToggleParent() => Sta(() =>
    {
        var action = new Button { Content = "+", Style = (Style)Resources()["FluentSourceAction"] };
        int calls = 0; action.Click += (_, _) => calls++;
        var expander = new Expander { Header = action, Template = (ControlTemplate)Resources()["FluentSourceExpanderTemplate"] };
        Layout(expander);
        ((IInvokeProvider)new ButtonAutomationPeer(action).GetPattern(PatternInterface.Invoke)).Invoke(); Drain();
        Assert.AreEqual(1, calls); Assert.IsFalse(expander.IsExpanded);
        Assert.IsTrue(action.ActualHeight >= 32); Assert.IsTrue(action.ActualWidth >= 32);
        action.IsEnabled = false; Assert.AreEqual(0.45, action.Opacity);
    });

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)]
    public void CardUsesScopedPaletteWithoutChangingValidation(bool dark, bool highContrast) => Sta(() =>
    {
        var expander = new Expander { Header = "Source", Template = (ControlTemplate)Resources()["FluentSourceExpanderTemplate"] };
        FluentAppearanceService.ApplyPalette(expander.Resources, dark, highContrast);
        expander.ApplyTemplate();
        var card = (Border)expander.Template.FindName("SourceCard", expander);
        Assert.AreEqual(new CornerRadius(8), card.CornerRadius);
        Assert.AreSame(expander.Resources["FluentSurface"], card.Background);
        Assert.AreSame(expander.Resources["FluentStroke"], card.BorderBrush);
        Assert.AreEqual(new Thickness(1), card.BorderThickness);
        Assert.IsNotNull(Validation.GetErrorTemplate(expander), "Never hide validation failures to mask a red line.");
    });

    [TestMethod]
    public void WorkspaceRestoresSingleSourceSurfaceOnGroupingAndViewChanges() => Sta(() =>
    {
        var workspace = new FluentWorkspace();
        workspace.DataContext = new { IsFluentPreview = true, IsShownList = true };
        var presenter = (ContentControl)workspace.FindName("ServerPresenter");
        BindingOperations.ClearBinding(presenter, Stylet.Xaml.View.ModelProperty);
        var sourcePage = new Border(); presenter.Content = sourcePage;
        var surface = (Border)workspace.FindName("ListSurface"); Drain();
        Assert.AreEqual(new Thickness(1), surface.BorderThickness);
        foreach (var grouped in new[] { true, false, true })
        {
            FluentWorkspace.SetHasSourceGroups(sourcePage, grouped); Drain();
            Assert.AreEqual(new Thickness(grouped ? 0 : 1), surface.BorderThickness);
            Assert.AreEqual(new Thickness(grouped ? 0 : 8), surface.Padding);
        }
        presenter.Content = new Border(); Drain(); // A tree or a single-source view must not inherit stale grouping.
        Assert.AreEqual(new Thickness(1), surface.BorderThickness);
        workspace.DataContext = new { IsFluentPreview = false, IsShownList = true }; Drain();
        Assert.AreEqual(new Thickness(0), surface.BorderThickness);
        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)surface.Background).Color);
    });

    private sealed class Group : CollectionViewGroup
    {
        public Group(params object[] rows) : base("synthetic") { foreach (var row in rows) ProtectedItems.Add(row); }
        public override bool IsBottomLevel => true;
    }
    [TestMethod]
    public void CollapsedFalsePersistsAndRestoresWithoutNullConversionError() => Sta(() =>
    {
        var previousPaths = AppPathHelper.Instance;
        var previousIoC = _1RM.IoC.GetByType;
        var settingsField = typeof(_1RM.Service.Locality.LocalityListViewService).GetField("_settings", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousSettings = settingsField.GetValue(null);
        var root = TestInit.CreateDirectory();
        try
        {
            AppPathHelper.Instance = new AppPathHelper(root, root);
            settingsField.SetValue(null, null);
            var locality = new _1RM.Service.Locality.LocalityService();
            _1RM.IoC.GetByType = (t, _) => t == typeof(_1RM.Service.Locality.LocalityService) ? locality : null;
            var source = new _1RM.Service.DataSource.Model.SqliteSource("synthetic") { DataSourceName = "synthetic" };
            var vm = new ProtocolBaseViewModel(new _1RM.Model.Protocol.Dummy { DataSource = source });
            var expander = new Expander { Header = "synthetic", Template = (ControlTemplate)Resources()["FluentSourceExpanderTemplate"] };
            // Non-nullable bool: False must not be a TargetNullValue sentinel, which maps back to null on collapse.
            expander.SetBinding(Expander.IsExpandedProperty, new Binding(nameof(vm.GroupedIsExpanded)) { Source = vm, Mode = BindingMode.TwoWay, FallbackValue = false });
            Layout(expander);
            var toggle = (ToggleButton)expander.Template.FindName("HeaderSite", expander);
            Assert.IsTrue(expander.IsExpanded);
            ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle(); Drain();
            Assert.IsFalse(vm.GroupedIsExpanded); Assert.IsFalse(Validation.GetHasError(expander));
            Assert.AreEqual(BindingStatus.Active, expander.GetBindingExpression(Expander.IsExpandedProperty)!.Status);
            Assert.IsTrue(System.IO.File.Exists(_1RM.Service.Locality.LocalityListViewService.JsonPath));
            settingsField.SetValue(null, null);
            Assert.IsFalse(_1RM.Service.Locality.LocalityListViewService.GroupedIsExpandedGet("synthetic"));
        }
        finally { _1RM.IoC.GetByType = previousIoC; AppPathHelper.Instance = previousPaths; settingsField.SetValue(null, previousSettings); }
    });
    [TestMethod]
    public void ExistingGroupCheckboxKeepsSelectAllAndPartialSelection() => Sta(() =>
    {
        var type = typeof(ServerListPageView);
        var listField = type.GetField("_lvServerCards", BindingFlags.Static | BindingFlags.NonPublic)!;
        var checkField = type.GetField("_checkBoxSelectedAll", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousList = listField.GetValue(null); var previousCheck = checkField.GetValue(null);
        try
        {
            listField.SetValue(null, new ListBox()); checkField.SetValue(null, new CheckBox());
            // Only exercise the selection flags; do not construct protocol models or access locality/user data.
            var a = (ProtocolBaseViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ProtocolBaseViewModel));
            var b = (ProtocolBaseViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ProtocolBaseViewModel));
            var group = new Group(a, b);
            var box = new CheckBox { Name = "HeaderCheckBox", DataContext = group };
            var expander = new Expander { Header = box, DataContext = group };
            NameScope.SetNameScope(expander, new NameScope()); expander.RegisterName(box.Name, box);
            foreach (var selected in new[] { true, false })
            {
                box.IsChecked = selected;
                ServerListPageView.ItemsCheckBox_OnClick_Static(box, new RoutedEventArgs(ButtonBase.ClickEvent, box));
                Assert.AreEqual(selected, a.IsSelected); Assert.AreEqual(selected, b.IsSelected);
            }
            a.IsSelected = true;
            type.GetMethod("RefreshCheckExpanderHeaderCheckBoxState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { expander });
            Assert.IsNull(box.IsChecked);
        }
        finally { listField.SetValue(null, previousList); checkField.SetValue(null, previousCheck); }
    });
}
