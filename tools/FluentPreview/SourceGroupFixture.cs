using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using _1RM.Service.DataSource.DAO;
using _1RM.View;
using _1RM.View.ServerView;
using Shawn.Utils;

namespace RemoteX.UiTesting;

// Real compiled view/templates, synthetic data and commands only. No application bootstrap or database.
public sealed class SourceGroupFixture : NotifyPropertyChangedBase
{
    public sealed class Source : NotifyPropertyChangedBase
    {
        private string _name = "Local";
        public string DataSourceName { get => _name; set => SetAndNotifyIfChanged(ref _name, value); }
        private bool _writable = true;
        public bool IsWritable { get => _writable; set => SetAndNotifyIfChanged(ref _writable, value); }
        private EnumDatabaseStatus _status = EnumDatabaseStatus.OK;
        public EnumDatabaseStatus Status { get => _status; set { SetAndNotifyIfChanged(ref _status, value); RaisePropertyChanged(nameof(StatusInfo)); } }
        public string StatusInfo => Status == EnumDatabaseStatus.OK ? (IsWritable ? "可读写（模拟）" : "只读（模拟）") : "数据源暂时无法访问（模拟）";
        public string ReconnectInfo { get; set; } = "";
        public override string ToString() => DataSourceName;
    }
    public sealed class Row : NotifyPropertyChangedBase
    {
        private bool _expanded;
        public bool GroupedIsExpanded { get => _expanded; set => SetAndNotifyIfChanged(ref _expanded, value); }
        public Source DataSource { get; set; } = null!;
        public bool IsVisible { get; set; } = true;
        public object Server => new { Id = IsVisible ? "synthetic" : "0" };
        public string DisplayName { get; set; } = "测试连接";
    }
    public sealed class Command : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public int Calls { get; private set; }
        public object? Parameter { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Calls++; Parameter = parameter; }
    }
    public ObservableCollection<Row> VmServerList { get; } = new();
    public Command CmdRefreshDataSource { get; } = new();
    public Command CmdAdd { get; } = new();
    public EnumServerViewStatus CurrentViewInListPage { get; set; } = EnumServerViewStatus.List;
    public bool IsTagFiltersShown => false;
    public bool IsAddToolTipShow => false;
    public object? SelectedServerViewModel { get; set; }
    private object? _tagListViewModel;
    public object? TagListViewModel { get => _tagListViewModel; set => SetAndNotifyIfChanged(ref _tagListViewModel, value); }
    public ServerListPageView Page { get; }
    public ListBox List { get; }
    public CollectionViewSource Collection { get; }

    public sealed class TagManagementModel
    {
        public object GlobalData { get; set; } = null!;
        public string FilterString { get; set; } = "";
        public bool FilterIsFocused { get; set; }
    }

    public TagsPanelView ShowTagManagement()
    {
        var presenter = (ContentControl)Page.FindName("TagManagementPresenter");
        var tags = new ObservableCollection<_1RM.Model.Tag> { new("bpm1.0", false, 0), new("hp", false, 1), new("lanyun", true, 2), new("personal", false, 3), new("prime", false, 4) };
        var model = new TagManagementModel { GlobalData = new { TagList = tags } };
        var view = new TagsPanelView { DataContext = model };
        presenter.Content = view; TagListViewModel = model;
        return view;
    }

    public void CheckTagManagementTransitions()
    {
        var view = ShowTagManagement();
        var model = TagListViewModel;
        var panel = (Grid)Page.FindName("ConnectionsPanel");
        var presenter = (ContentControl)Page.FindName("TagManagementPresenter");
        var stage = new Grid { Width = 760, Height = 480 }; stage.Children.Add(Page);
        var source = VmServerList[0];
        source.GroupedIsExpanded = false;
        for (var i = 0; i < 12; i++)
        {
            var managing = i % 2 == 0;
            TagListViewModel = managing ? model : null;
            Drain(); stage.Measure(new Size(760, 480)); stage.Arrange(new Rect(0, 0, 760, 480)); stage.UpdateLayout(); Drain();
            Require(panel.Visibility == (managing ? Visibility.Collapsed : Visibility.Visible), "Tag management must not leave the connection content behind it.");
            Require(presenter.Visibility == (managing ? Visibility.Visible : Visibility.Collapsed), "Only one content region should be visible.");
            Require(!source.GroupedIsExpanded, "Returning from tag management must preserve source expansion state.");
            if (managing)
            {
                var body = (Grid)view.FindName("TagManagementBody");
                Require(body.Margin.Top == 0 && body.RowDefinitions[0].ActualHeight == 0, "Fluent list has no legacy tab-strip space.");
            }
        }
        stage.Children.Clear();
        Console.WriteLine("PASS: repeated tag management / connection switching, no legacy gap, expansion preserved.");
    }

    public SourceGroupFixture(bool expanded = false, int rowsPerSource = 3, bool cards = false)
    {
        CurrentViewInListPage = cards ? EnumServerViewStatus.Card : EnumServerViewStatus.List;
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        // The preview runner supplies the same shared resources as the application, without bootstrapping it.
        var resources = new ResourceDictionary();
        foreach (var path in new[] {
            "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
            "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
            "RemoteX;component/Resources/Converter/Converter.xaml",
            "RemoteX;component/Resources/Theme/Markdown.xaml",
            "RemoteX;component/Resources/Icons/SVG.xaml",
            "RemoteX;component/Resources/Theme/FluentPreview.xaml" })
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + path) });
        Page = new ServerListPageView();
        // Even a null model invokes Stylet's ViewManager; isolated fixtures provide content directly.
        BindingOperations.ClearBinding((ContentControl)Page.FindName("TagManagementPresenter"), Stylet.Xaml.View.ModelProperty);
        Page.Resources.MergedDictionaries.Add(resources);
        Page.DataContext = this;
        List = (ListBox)Page.FindName("LvServerCards");
        Collection = (CollectionViewSource)Page.Resources["ServerListItemSource"];
        Collection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Row.DataSource)));
        for (var s = 0; s < 2; s++)
        {
            var source = new Source { DataSourceName = s == 0 ? "Local" : "home_pgsql" };
            for (var r = 0; r < rowsPerSource; r++)
                VmServerList.Add(new Row { DataSource = source, GroupedIsExpanded = expanded, DisplayName = $"测试连接 {s + 1}-{r + 1}" });
        }
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.DisplayName)));
        text.SetValue(FrameworkElement.MinHeightProperty, 40.0);
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(16, 0, 8, 0));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetResourceReference(TextBlock.ForegroundProperty, "FluentText");
        if (cards)
        {
            // Connection card internals are outside this change; keep their occupied size in the grouping fixture.
            var card = new FrameworkElementFactory(typeof(Border));
            card.SetValue(FrameworkElement.WidthProperty, 200.0); card.SetValue(FrameworkElement.HeightProperty, 204.0);
            card.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 12));
            card.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            card.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
            card.SetValue(Border.BorderThicknessProperty, new Thickness(1)); card.AppendChild(text);
            List.ItemTemplate = new DataTemplate { VisualTree = card };
        }
        else List.ItemTemplate = new DataTemplate { VisualTree = text };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    public void CheckInteractions()
    {
        var stage = new Grid { Width = 760, Height = 480 };
        stage.Children.Add(Page);
        void Layout() { stage.Measure(new Size(stage.Width, stage.Height)); stage.Arrange(new Rect(0, 0, stage.Width, stage.Height)); stage.UpdateLayout(); Drain(); }
        Layout();
        var expanders = Descendants(Page).OfType<Expander>().ToArray();
        Require(expanders.Length == 2, "Two actual group containers should be realized.");
        var first = expanders[0];
        first.SetCurrentValue(Expander.IsExpandedProperty, false);
        expanders[1].SetCurrentValue(Expander.IsExpandedProperty, false); Layout();
        var toggle = (ToggleButton)first.Template.FindName("HeaderSite", first);
        Require(toggle.ActualWidth > 700, "Group header must stretch, not inherit the legacy switch width.");
        var provider = (IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle);
        for (var i = 0; i < 10; i++)
        {
            provider.Toggle(); Layout();
            Require(first.IsExpanded == (i % 2 == 0), "Header activation must toggle exactly once.");
            var item = ((CollectionViewGroup)first.DataContext).Items[0] as Row;
            var expression = first.GetBindingExpression(Expander.IsExpandedProperty);
            Console.WriteLine($"Toggle {i}: group={((CollectionViewGroup)first.DataContext).Name}, expanded={first.IsExpanded}, source={item?.GroupedIsExpanded}, binding={expression?.Status}, resolved={expression?.ResolvedSourcePropertyName}");
            if (expression?.ValidationError is { } error) Console.WriteLine($"VALIDATION: {error.ErrorContent}\n{error.Exception}");
            Require(item?.GroupedIsExpanded == first.IsExpanded, "Two-way expansion binding must remain active.");
            Require(!expanders[1].IsExpanded, "Expanding one source must not alter the other.");
        }
        foreach (var name in new[] { "ButtonDbStatus", "SourceAdd" })
        {
            var button = Descendants(first).OfType<Button>().Single(b => b.Name == name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke(); Drain();
            Require(!first.IsExpanded, "Source action must not toggle expansion.");
        }
        Require(CmdAdd.Calls == 1 && CmdRefreshDataSource.Calls == 1, "Existing command bindings must invoke the correct command once.");
        Require(ReferenceEquals(CmdAdd.Parameter, VmServerList[0].DataSource), "Add must target the chosen source.");
        Require(first.FindName("HeaderCheckBox") is CheckBox, "Existing group selection lookup must find the real header checkbox.");
        var handle = Descendants(first).OfType<Grid>().Single(g => g.Cursor == Cursors.SizeNS);
        var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent };
        handle.RaiseEvent(down);
        Require(down.Handled && !first.IsExpanded, "Drag handle must retain its handler and suppress header activation.");
        VmServerList[0].DataSource.DataSourceName = "Renamed source"; Layout();
        Require(Descendants(first).OfType<TextBlock>().Any(t => t.Name == "SourceName" && t.Text == "Renamed source"), "Renaming must update the header.");
        VmServerList[0].DataSource.IsWritable = false; Layout();
        var add = Descendants(first).OfType<Button>().Single(b => b.Name == "SourceAdd");
        Require(!add.IsEnabled, "Read-only source must not allow adding.");
        VmServerList[0].DataSource.Status = EnumDatabaseStatus.LostConnection; Layout();
        Require(add.Visibility == Visibility.Collapsed, "Disconnected source must hide Add.");
        Require(Descendants(first).OfType<StackPanel>().Single(p => p.Name == "SourceError").Visibility == Visibility.Visible, "Failure information should be visible.");
        Require(!Descendants(Page).OfType<FrameworkElement>().Any(Validation.GetHasError), "Unexpected WPF validation error/red decoration.");
        Console.WriteLine("PASS: real grouped template expansion, independent source actions, selection name scope, rename, read-only and error state; no validation errors.");
        Collection.GroupDescriptions.Clear(); Layout();
        Require(!FluentWorkspace.GetHasSourceGroups(Page), "Removing grouping must restore the outer surface.");
        Collection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Row.DataSource))); Layout();
        Require(FluentWorkspace.GetHasSourceGroups(Page), "Regrouping must restore source cards.");
        FluentPage.SetActive(Page, false); Layout();
        Require(Descendants(Page).OfType<Expander>().All(e => e.Template != Page.FindResource("FluentSourceExpanderTemplate")), "Classic template must be preserved.");
        var classic = Descendants(Page).OfType<Expander>().First();
        var classicToggle = (ToggleButton)classic.Template.FindName("HeaderSite", classic);
        var classicProvider = (IToggleProvider)new ToggleButtonAutomationPeer(classicToggle).GetPattern(PatternInterface.Toggle);
        classicProvider.Toggle(); classicProvider.Toggle(); Layout();
        Require(!Validation.GetHasError(classic), "Shared null-conversion fix must also preserve classic expansion.");
        // Simulate filtering, source removal and a new empty group without touching a real data source.
        var saved = VmServerList.ToArray();
        VmServerList.Clear(); Layout();
        Require(!Descendants(Page).OfType<Expander>().Any(), "Empty filtered collection must not leave stale group visuals.");
        foreach (var row in saved.Where(r => r.DataSource == saved[0].DataSource)) VmServerList.Add(row);
        Layout(); Require(Descendants(Page).OfType<Expander>().Count() == 1, "Removed sources must disappear.");
        VmServerList.Add(new Row { DataSource = new Source { DataSourceName = "Empty source" }, IsVisible = false });
        Layout(); Require(Descendants(Page).OfType<Expander>().Count() == 2, "Empty source placeholder must keep its header.");
        stage.Children.Clear();
    }

    public void CheckVirtualization()
    {
        var stage = new Grid { Width = 760, Height = 480 }; stage.Children.Add(Page);
        void Layout() { stage.Measure(new Size(760, 480)); stage.Arrange(new Rect(0, 0, 760, 480)); stage.UpdateLayout(); Drain(); }
        Layout();
        var scroll = Descendants(List).OfType<ScrollViewer>().First();
        foreach (var offset in new[] { 0.0, 10000.0, 40000.0, 0.0 })
        {
            scroll.ScrollToVerticalOffset(offset); Layout();
            var count = Descendants(List).OfType<ListBoxItem>().Count();
            Console.WriteLine($"Grouped virtualization: offset={offset}, realized={count}, total={VmServerList.Count}");
            Require(count < 200, "Source card wrapper must not materialize the entire connection list.");
        }
        // The wrap panel must not use the whole ListBox width and clip the last column when resized.
        if (CurrentViewInListPage == EnumServerViewStatus.Card)
        {
            stage.Width = 420;
            stage.Measure(new Size(420, 480)); stage.Arrange(new Rect(0, 0, 420, 480)); stage.UpdateLayout(); Drain();
            foreach (var expander in Descendants(List).OfType<Expander>())
            foreach (var item in Descendants(expander).OfType<ListBoxItem>())
                Require(item.TranslatePoint(new Point(item.ActualWidth, 0), expander).X <= expander.ActualWidth + 0.5,
                    "Connection cards must fit inside the source card at narrow widths.");
        }
        stage.Children.Clear();
    }

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
