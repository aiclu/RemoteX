using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using _1RM.View;
using Dragablz;

namespace RemoteX.UiTesting;

// Uses the production window's entire content grid, tab style, prefix, suffix and window buttons.
// Only code-behind events/InterTabController are omitted: no real window, host, session or database.
public sealed class SessionHeaderFixture : IDisposable
{
    public sealed class StubCommand : ICommand
    {
        public int Calls { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Calls++;
    }
    public sealed class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool TabHeaderShowIconButton { get; set; } = true;
        public bool TabHeaderShowCloseButton { get; set; } = true;
        public bool TabHeaderShowReConnectButton { get; set; } = true;
        public bool TabWindowCloseButtonOnLeft { get; set; }
        public bool LauncherEnabled { get; set; } = true;
        public Settings GeneralSettingViewModel => this;
        public Settings LauncherSettingViewModel => this;
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    public sealed class SessionContent
    {
        public Settings SettingsPage { get; }
        public string ConnectionId { get; } = Guid.NewGuid().ToString();
        public bool CanFullScreen => true;
        public object[] MenuItems => Array.Empty<object>();
        public SessionContent(Settings settings) => SettingsPage = settings;
    }
    public sealed class Tab
    {
        public string DisplayName { get; set; } = "UAT 服务器";
        public string ColorHex { get; set; } = "#00000000";
        public ImageSource IconImg { get; } = new BitmapImage(new Uri("pack://application:,,,/RemoteX;component/Resources/Icons/000_OS/001_Ubuntu.png"));
        public SessionContent Content { get; }
        public StubCommand CmdReconnect { get; } = new();
        public Tab(Settings settings) => Content = new(settings);
    }
    public Settings SettingsPage { get; } = new();
    public ObservableCollection<Tab> Items { get; } = new();
    public Tab? SelectedItem { get; set; }
    public Visibility BtnCloseAllVisibility => Visibility.Visible;
    public ResizeMode WindowResizeMode => ResizeMode.CanResize;
    public StubCommand CmdInvokeLauncher { get; } = new();
    public StubCommand CmdClose { get; } = new();
    public StubCommand CmdCloseAll { get; } = new();
    public StubCommand CmdHostGoFullScreen { get; } = new();
    public StubCommand CmdGoMinimize { get; } = new();
    public StubCommand CmdGoMaximize { get; } = new();
    public StubCommand CmdGoMaximizeF11 { get; } = new();
    public StubCommand CmdCloseWindow { get; } = new();
    public Grid View { get; }
    public TabablzControl Tabs { get; }
    public Grid Header => (Grid)Tabs.Template.FindName("HeaderContainerGrid", Tabs);
    public Border ContentPanel => (Border)Tabs.Template.FindName("contentPanel", Tabs);
    public DragablzItemsControl Headers => (DragablzItemsControl)Tabs.Template.FindName("PART_HeaderItemsControl", Tabs);
    public StackPanel WindowButtons => (StackPanel)View.FindName("SessionWindowButtons");
    public Button Add => Descendants<Button>(Header).Single(x => x.Name == "SessionAdd");
    public Grid Logo => Descendants<Grid>(Header).Single(x => x.Name == "SessionLogo");
    public ScrollViewer Scroller => Descendants<ScrollViewer>(Headers).Single();

    public static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "RemoteX.sln"))) folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("Run inside the RemoteX checkout.");
    }
    public SessionHeaderFixture(int count = 3, bool longTitle = false)
    {
        var source = XDocument.Load(Path.Combine(FindRoot(), "Ui/View/Host/TabWindowView.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = new XElement(source.Root!.Element(p + "Window.Resources")!.Element(p + "ResourceDictionary")!);
        var merged = resources.Element(p + "ResourceDictionary.MergedDictionaries")!;
        // Inline the identical local dictionary so loose XAML can resolve BasedOn against the
        // same base resources without constructing a process-wide Application on the test thread.
        merged.RemoveNodes();
        var sessionResources = XDocument.Load(Path.Combine(FindRoot(), "Ui/Resources/Theme/FluentSessions.xaml"));
        merged.AddAfterSelf(sessionResources.Root!.Elements().Select(e => new XElement(e)));
        merged.AddFirst(new[] {
            "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
            "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
            "RemoteX;component/Resources/Languages/zh-cn.xaml"
        }.Select(path => new XElement(p + "ResourceDictionary", new XAttribute("Source", "pack://application:,,,/" + path))));
        var grid = new XElement(source.Root.Element(p + "Grid")!);
        grid.AddFirst(new XElement(p + "Grid.Resources", resources));
        foreach (var attribute in source.Root.Attributes().Where(a => a.IsNamespaceDeclaration))
            grid.SetAttributeValue(attribute.Name, attribute.Value.StartsWith("clr-namespace:_1RM") ? attribute.Value + ";assembly=RemoteX" : attribute.Value);
        grid.SetAttributeValue(XNamespace.Xmlns + "dragablz", "clr-namespace:Dragablz;assembly=Dragablz");
        foreach (var element in grid.Descendants().Where(e => e.Name.NamespaceName == "http://dragablz.net/winfx/xaml/dragablz"))
            element.Name = XName.Get(element.Name.LocalName, "clr-namespace:Dragablz;assembly=Dragablz");
        foreach (var element in grid.Descendants().Where(e => e.Name.NamespaceName.StartsWith("clr-namespace:_1RM") && !e.Name.NamespaceName.Contains(';')))
            element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=RemoteX");
        foreach (var controller in grid.Descendants().Where(x => x.Name.LocalName == "TabablzControl.InterTabController").ToArray()) controller.Remove();
        foreach (var handler in grid.DescendantsAndSelf().Attributes().Where(a => new[] {
            "MouseDown", "PreviewMouseMove", "SelectionChanged", "PreviewMouseLeftButtonDown"
        }.Contains(a.Name.LocalName)).ToArray()) handler.Remove();
        var xaml = System.Text.RegularExpressions.Regex.Replace(grid.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
        View = (Grid)XamlReader.Parse(xaml);
        Tabs = (TabablzControl)View.FindName("TabablzControl");
        // Keep the actual content-presenter geometry without constructing a remote protocol control.
        Tabs.ContentTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='Transparent'/></DataTemplate>");
        for (var i = 0; i < count; i++) Items.Add(new Tab(SettingsPage) {
            DisplayName = i == 0 ? "UAT 服务器" : i == 1 ? "研发 Linux" : longTitle ? "长标题 · Development-Server-with-a-very-long-name-用于省略验证" : $"会话 {i + 1}",
            ColorHex = i == 1 ? "#D97706" : "#00000000"
        });
        SelectedItem = Items[0];
        View.DataContext = this;
    }
    public void SetFluent(bool enabled)
    {
        // Just like production: scope the header, not the remote-content presenter.
        FluentPage.SetActive(View, false);
        FluentPage.SetActive(Header, enabled);
        FluentPage.SetActive(WindowButtons, enabled);
    }
    public void Layout(double width = 1080, double height = 120)
    {
        View.Measure(new Size(width, height)); View.Arrange(new Rect(0, 0, width, height)); View.UpdateLayout();
        Drain();
        View.UpdateLayout();
    }
    public DragablzItem Item(int index) => (DragablzItem)Headers.ItemContainerGenerator.ContainerFromIndex(index);
    public T Part<T>(int index, string name) where T : FrameworkElement => (T)Item(index).Template.FindName(name, Item(index));
    public static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    public void CheckActionBounds()
    {
        if (Add.Visibility != Visibility.Visible) return;
        var addLeft = Add.TranslatePoint(new Point(), View).X;
        var addRight = Add.TranslatePoint(new Point(Add.ActualWidth, 0), View).X;
        var windowLeft = WindowButtons.TranslatePoint(new Point(), View).X;
        var windowRight = windowLeft + WindowButtons.ActualWidth;
        if (SettingsPage.TabWindowCloseButtonOnLeft ? addLeft < windowRight : addRight > windowLeft)
            throw new InvalidOperationException($"Launcher overlaps window buttons: {addLeft}..{addRight}, chrome {windowLeft}..{windowRight}.");
    }
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var next in Descendants<T>(child)) yield return next;
        }
    }
    public void Dispose()
    {
        foreach (var child in Descendants<FrameworkElement>(View).Reverse().ToArray())
            child.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        View.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
}
