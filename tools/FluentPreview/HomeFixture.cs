using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using _1RM.Model;
using _1RM.Service.Locality;
using _1RM.View;

namespace RemoteX.UiTesting;

// The complete compiled home surface, including actual connection rows. All commands/data are synthetic.
public sealed class HomeFixture
{
    public SourceGroupFixture ActiveServerViewModel { get; } = new(true, 5, realRows: true);
    public bool IsFluentPreview => true;
    public bool IsShownList => true;
    public string FluentTheme => "System";
    public EnumServerViewStatus CurrentView => EnumServerViewStatus.List;
    public EnumServerOrderBy ServerOrderBy => EnumServerOrderBy.NameAsc;
    public string MainFilterString { get; set; } = "";
    public bool MainFilterIsFocused { get; set; }
    public int MainFilterCaretIndex { get; set; }
    public object AboutViewModel { get; } = new { NewVersion = "" };
    public SourceGroupFixture.Command CmdGoSysOptionsPage { get; } = new();
    public SourceGroupFixture.Command CmdGoAboutPage { get; } = new();
    public SourceGroupFixture.Command CmdFluentTheme { get; } = new();
    public SourceGroupFixture.Command CmdClassicAppearance { get; } = new();
    public SourceGroupFixture.Command CmdToggleListView { get; } = new();
    public SourceGroupFixture.Command CmdToggleCardView { get; } = new();
    public SourceGroupFixture.Command CmdToggleTreeView { get; } = new();
    public SourceGroupFixture.Command CmdReOrder { get; } = new();
    public SourceGroupFixture.Command CmdExit { get; } = new();
    public ObservableCollection<Tag> Tags { get; } = new() { new("prime", true, 0), new("hp", false, 1), new("personal", false, 2), new("bpm1.0", false, 3), new("lanyun · 长标签名称", false, 4) };
    public FluentWorkspace View { get; } = new();

    public HomeFixture()
    {
        var presenter = (ContentControl)View.FindName("ServerPresenter");
        BindingOperations.ClearBinding(presenter, Stylet.Xaml.View.ModelProperty);
        View.DataContext = this; presenter.Content = ActiveServerViewModel.Page;
        var icon = new BitmapImage(new Uri("pack://application:,,,/RemoteX;component/Resources/Image/Logo/logo64.png"));
        var names = new[] { "研发服务器", "移动端测试服务器", "UAT 环境", "开发服务器 · 长名称用于检查省略和提示", "运维终端" };
        for (var i = 0; i < ActiveServerViewModel.VmServerList.Count; i++)
        {
            var row = ActiveServerViewModel.VmServerList[i]; row.GroupedIsExpanded = i >= 5;
            row.DisplayName = names[i % 5]; row.Protocol = i % 5 == 4 ? "SSH" : "RDP";
            row.Address = $"demo-{i + 1}.example.invalid:{(row.Protocol == "SSH" ? 22 : 3389)} (demo-user)"; row.Icon = icon;
        }
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private T Named<T>(string name) where T : FrameworkElement => (T)View.FindName(name);
    private Rect Bounds(FrameworkElement element) => element.TransformToAncestor(View).TransformBounds(new Rect(element.RenderSize));
    private void Equal(double first, double second, string message) => Require(Math.Abs(first - second) <= 1, $"{message}: {first:0.##} vs {second:0.##} DIP");

    public void Prepare(int width, bool dark, Type service)
    {
        // The normal Loaded handler deliberately requires a real MainWindowViewModel. This fixture supplies
        // only its binding contract; use the same presentation-only layout method without bootstrapping services.
        typeof(FluentWorkspace).GetMethod("ApplyHomeLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(View, new object[] { true, (double)width });
        Named<ListBox>("Tags").ItemsSource = Tags;
        FluentWorkspace.SetIsPreview(ActiveServerViewModel.Page, true);
        service.GetMethod("ApplyPalette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { ActiveServerViewModel.Page.Resources, dark, false });
        service.GetMethod("ApplyAliases", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { ActiveServerViewModel.Page.Resources });
        foreach (var key in new[] { "GlobalFontSize", "GlobalFontSizeBody", "TextFontSize", "GlobalFontSizeTitle", "GlobalFontSizeSubtitle", "GlobalFontSizeSmall" })
            ActiveServerViewModel.Page.Resources[key] = View.Resources[key];
        View.UpdateLayout(); Drain(); View.UpdateLayout();
    }

    public void CheckGeometry(int width)
    {
        var toolbar = Named<Panel>("Toolbar"); var search = Named<TextBox>("Search"); var surface = Named<Border>("ListSurface");
        Equal(Bounds(toolbar).Left, Bounds(surface).Left, "Toolbar / surface left");
        Equal(Bounds(toolbar).Right, Bounds(surface).Right, "Toolbar / surface right");
        Equal(Bounds(search).Top, Bounds(Named<Button>("AllConnectionsNavigation")).Top, "Sidebar / search top");
        Equal(Bounds(surface).Right, width - 16, "Content right inset");
        var toolbarBounds = Bounds(toolbar);
        foreach (FrameworkElement child in toolbar.Children)
        {
            var box = Bounds(child);
            Require(box.Left >= toolbarBounds.Left - 1 && box.Right <= toolbarBounds.Right + 1, "Toolbar action must stay within the content edges.");
            Require(child.ActualHeight >= 36, "Toolbar minimum height.");
        }
        foreach (var expander in SourceGroupFixture.Descendants(ActiveServerViewModel.Page).OfType<Expander>())
        {
            var card = (Border)expander.Template.FindName("SourceCard", expander);
            Equal(Bounds(card).Left, Bounds(toolbar).Left, "Card / toolbar left");
            Equal(Bounds(card).Right, Bounds(toolbar).Right, "Card / toolbar right");
            var elements = SourceGroupFixture.Descendants(expander).OfType<FrameworkElement>().ToArray();
            var row = elements.OfType<FluentConnectionRow>().FirstOrDefault();
            if (row == null || !expander.IsExpanded) continue;
            var check = elements.Single(e => e.Name == "HeaderCheckBox"); var name = elements.Single(e => e.Name == "SourceName");
            Equal(Bounds(check).Left, Bounds((FrameworkElement)row.FindName("RowSelection")).Left, "Header / row checkbox");
            Equal(Bounds(name).Left, Bounds((FrameworkElement)row.FindName("RowName")).Left, "Header / row name");
            Equal(Bounds(elements.Single(e => e.Name == "SourceAdd")).Right, Bounds((FrameworkElement)row.FindName("RowConnect")).Right, "Header / row actions");
            Require(row.ActualHeight >= 44, "Connection row minimum height.");
            if (width == 1000)
            {
                var button = (Button)row.FindName("RowConnect");
                var label = SourceGroupFixture.Descendants(button).OfType<TextBlock>().First();
                var formatted = new FormattedText(label.Text, System.Globalization.CultureInfo.CurrentCulture, label.FlowDirection,
                    new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                Require(label.ActualWidth >= formatted.Width, "Wide layout must not ellipsize the localized connection action.");
            }
        }
        Require(!SourceGroupFixture.Descendants(View).OfType<FrameworkElement>().Any(Validation.GetHasError), "No validation adorners in the simulated home.");
        Console.WriteLine($"PASS: complete home alignment <= 1 DIP at {width}, real rows={SourceGroupFixture.Descendants(View).OfType<FluentConnectionRow>().Count()}.");
    }

    public void CheckSelectionAndCommands()
    {
        var all = Named<Button>("AllConnectionsNavigation");
        foreach (var tag in new[] { "prime", "hp", "", "personal" })
        {
            ActiveServerViewModel.SelectedTabName = tag; Drain();
            Require(all.FontWeight == (tag.Length == 0 ? FontWeights.SemiBold : FontWeights.Normal), "All connections must not compete with the active tag.");
            var buttons = SourceGroupFixture.Descendants(Named<ListBox>("Tags")).OfType<Button>().Where(b => b.DataContext is Tag).ToArray();
            foreach (var button in buttons)
                Require(button.FontWeight == (((Tag)button.DataContext).Name == tag ? FontWeights.SemiBold : FontWeights.Normal), "No stale tag highlight.");
        }
        ActiveServerViewModel.SelectedTabName = "prime";
        var search = Named<TextBox>("Search"); search.Text = "#prime 中文"; search.CaretIndex = 7;
        for (var i = 0; i < 20; i++)
        {
            var toolbar = Named<Panel>("Toolbar"); var size = new Size(i % 2 == 0 ? 212 : 800, 300);
            toolbar.Measure(size); toolbar.Arrange(new Rect(new Point(), toolbar.DesiredSize));
            Require(ReferenceEquals(search, Named<TextBox>("Search")) && search.CaretIndex == 7, "Reflow must preserve the search instance/caret.");
        }
        search.Text = ""; View.InvalidateMeasure(); View.UpdateLayout();
        foreach (var button in SourceGroupFixture.Descendants(View).OfType<Button>().Where(b => b.ContextMenu != null && b.Name is "ViewMenuButton" or "SortMenuButton" or "MoreMenuButton"))
        {
            button.ContextMenu!.DataContext = this;
            foreach (var menu in button.ContextMenu.Items.OfType<MenuItem>()) menu.ApplyTemplate();
        }
        Drain();
        var viewMenu = Named<Button>("ViewMenuButton").ContextMenu!;
        Require(((MenuItem)viewMenu.Items[0]).IsChecked && !((MenuItem)viewMenu.Items[1]).IsChecked, "View menu marker must match list view.");
        var sortMenu = Named<Button>("SortMenuButton").ContextMenu!;
        Require(((MenuItem)sortMenu.Items[1]).IsChecked && Equals(((MenuItem)sortMenu.Items[1]).Tag, "↑"), "Sort marker must retain its direction.");
        Require(ReferenceEquals(Named<Button>("AddConnectionButton").Command, ActiveServerViewModel.CmdAdd), "Add command binding.");
        var connect = SourceGroupFixture.Descendants(View).OfType<Button>().First(b => b.Name == "RowConnect");
        Require(connect.Command is SourceGroupFixture.Command, "Real row must bind its connection command.");
        connect.Command.Execute(connect.CommandParameter); // synthetic command, never opens a connection
        Require(((SourceGroupFixture.Command)connect.Command).Calls == 1, "Synthetic connect invoked once.");
        Console.WriteLine("PASS: sidebar selection, repeated toolbar reflow/caret, menu markers and synthetic command bindings.");
    }

    public void UseLongLabels()
    {
        foreach (var resources in new[] { View.Resources, ActiveServerViewModel.Page.Resources })
        {
            resources["ListViewMode"] = "Connection view";
            resources["FluentSort"] = "Sort connections";
            resources["FluentMore"] = "More operations";
            resources["Add"] = "Add connection";
            resources["Connect"] = "Connect";
        }
    }

    public void CheckFullResize(int originalWidth, bool dark, Type service)
    {
        var stage = (Grid)View.Parent;
        var search = Named<TextBox>("Search"); search.Text = "#prime 中文"; search.CaretIndex = 7;
        foreach (var width in Enumerable.Repeat(new[] { 1120, 300, 600, 800 }, 3).SelectMany(x => x).Append(originalWidth))
        {
            stage.Width = width; stage.Measure(new Size(width, stage.Height)); stage.Arrange(new Rect(0, 0, width, stage.Height)); stage.UpdateLayout();
            Prepare(width, dark, service); CheckGeometry(width);
            Require(ReferenceEquals(search, Named<TextBox>("Search")) && search.CaretIndex == 7 && search.Text == "#prime 中文", "Full-page resize must preserve search state.");
        }
        search.Text = "";
        Console.WriteLine("PASS: repeated full-home resize without layout loops or search-state loss (IME/hardware focus still requires interactive verification).");
    }
}
