using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.View;
using _1RM.View.Launcher;

namespace RemoteX.UiTesting;

// Production window, result/action/quick templates and note chrome, with inert data.
// Code-behind event hookups and Stylet activation are omitted: no hosts, DB, hotkeys or persistence.
public sealed class LauncherFixture : IDisposable
{
    public sealed class Row
    {
        public RDP Server { get; }
        public string DisplayName => Server.DisplayName;
        public string SubTitle => Server.SubTitle;
        public string TagString { get; } = "#研发 #personal";
        public string DataSourceNameForLauncher { get; }
        public ServerTitleViewModel LauncherMainTitleViewModel { get; }
        public ServerTitleViewModel LauncherSubTitleViewModel { get; }
        public Row(int index, string icon)
        {
            Server = new RDP { Id = "MOCK-" + index, DisplayName = index == 1 ? "开发环境 · 非常长的连接名称用于省略检查 Development" : new[] { "UAT 服务器", "研发服务器", "Linux 网关", "测试数据库" }[index % 4],
                Address = "demo-" + index + ".example.invalid", UserName = "demo-user", IconBase64 = icon, ColorHex = index % 3 == 1 ? "#D97706" : "#00000000", Note = index == 0 ? "# 部署备注\n\n- 仅使用模拟数据\n- 不连接或保存\n\n**检查信息层级与滚动**" : "" };
            DataSourceNameForLauncher = index % 2 == 0 ? "Local" : "Team";
            LauncherMainTitleViewModel = new(DisplayName); LauncherMainTitleViewModel.UnHighLightAll();
            LauncherSubTitleViewModel = new(SubTitle); LauncherSubTitleViewModel.UnHighLightAll();
        }
    }
    public sealed class SelectionModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<Row> VmServerList { get; } = new();
        public ObservableCollection<TagFilter> TagFilters { get; } = new();
        public ObservableCollection<ProtocolAction> Actions { get; } = new();
        public bool ShowCredentials { get; set; } = true;
        public bool IsShowNoteFieldEnabled => true;
        public string Filter { get; set; } = "";
        public int SelectedIndex { get; set; }
        public int SelectedActionIndex { get; set; }
        public Row? SelectedItem => VmServerList.ElementAtOrDefault(SelectedIndex);
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    public sealed class QuickModel
    {
        public string Filter { get; set; } = "";
        public int SelectedIndex { get; set; }
        public object[] Protocols { get; } = new object[] { new { Protocol = "RDP" }, new { Protocol = "SSH" } };
        public object? SelectedProtocol { get; set; }
        public ObservableCollection<QuickConnectionItem> ConnectHistory { get; } = new();
    }
    public Grid View { get; }
    public UserControl Results { get; }
    public UserControl Quick { get; }
    public UserControl Note { get; }
    public SelectionModel Model { get; } = new();
    public QuickModel QuickData { get; } = new();
    public ListBox ResultList => (ListBox)Results.FindName("ListBoxSelections");
    public ListBox ActionList => (ListBox)Results.FindName("ListBoxActions");
    public ListBox QuickList => (ListBox)Quick.FindName("ListBoxHistory");
    public TextBox Search => (TextBox)Results.FindName("TbKeyWord");
    public Border FilterStrip => (Border)Results.FindName("FluentFilterStrip");
    public Grid Actions => (Grid)Results.FindName("GridActionsList");
    private readonly ContentControl _resultHost;
    private readonly ContentControl _quickHost;
    private readonly Border _noteHost;
    private readonly Grid _content;
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Stylet = "https://github.com/canton7/Stylet";
    private static string Root => SessionHeaderFixture.FindRoot();

    public LauncherFixture(int count = 12)
    {
        var iconResource = Application.GetResourceStream(new Uri("pack://application:,,,/RemoteX;component/Resources/Icons/000_OS/001_Ubuntu.png"));
        using var memory = new MemoryStream(); using (var stream = iconResource.Stream) stream.CopyTo(memory);
        var icon = Convert.ToBase64String(memory.ToArray());
        for (var i = 0; i < count; i++) Model.VmServerList.Add(new Row(i, icon));
        foreach (var action in new[] { "连接", "使用备选连接", "编辑连接", "复制地址", "查看备注", "在新窗口中打开" })
            Model.Actions.Add(new ProtocolAction(action, () => throw new InvalidOperationException("No real actions in a render fixture.")));
        for (var i = 0; i < count; i++) QuickData.ConnectHistory.Add(new QuickConnectionItem { Host = "demo-" + i + ".example.invalid:3389", Protocol = i % 2 == 0 ? "RDP" : "SSH" });
        QuickData.SelectedProtocol = QuickData.Protocols[0];
        Results = Page("Ui/View/Launcher/ServerSelectionsView.xaml"); Results.DataContext = Model;
        // Feed the production implicit result template inert rows of the same binding shape.
        ResultList.ItemTemplate = (DataTemplate)Results.Resources[new DataTemplateKey(typeof(ProtocolBaseViewModel))];
        Quick = Page("Ui/View/Launcher/QuickConnectionView.xaml"); Quick.DataContext = QuickData;
        Note = Page("Ui/Controls/NoteDisplay/NoteDisplayAndEditor.xaml");
        var markdown = (FrameworkElement)Note.FindName("MarkdownViewer");
        markdown.GetType().GetProperty("Markdown")!.SetValue(markdown, Model.SelectedItem?.Server.Note ?? "# 模拟备注");
        ((Grid)Note.FindName("GridEditor")).Visibility = Visibility.Collapsed;
        ((Button)Note.FindName("ButtonClose")).IsEnabled = true;
        FluentLauncher.SetIsNotePane(Note, true);

        var source = XDocument.Load(Path.Combine(Root, "Ui/View/LauncherWindowView.xaml"));
        var grid = new XElement(source.Root!.Element(P + "Grid")!);
        // Use the real shell, not a hand-built imitation. Bindings that activate pages are replaced below.
        var dictionary = Resources();
        grid.AddFirst(new XElement(P + "Grid.Resources", dictionary));
        Namespaces(source.Root, grid);
        var noteElement = grid.Descendants().Single(e => e.Name.LocalName == "NoteDisplayAndEditor");
        noteElement.ReplaceWith(new XElement(P + "ContentControl", new XAttribute(X + "Name", "NotePreview")));
        Clean(grid);
        View = (Grid)XamlReader.Parse(Normalize(grid));
        _resultHost = (ContentControl)View.FindName("ServerSelectionsView");
        _content = (Grid)_resultHost.Parent;
        _quickHost = _content.Children.OfType<ContentControl>().Single(c => c != _resultHost);
        _noteHost = (Border)View.FindName("NoteField");
        _resultHost.Content = Results; _quickHost.Content = Quick;
        ((ContentControl)View.FindName("NotePreview")).Content = Note;
        _quickHost.Visibility = Visibility.Collapsed; _noteHost.Visibility = Visibility.Collapsed;
    }
    private static XElement Resources()
    {
        var dictionary = new XElement(P + "ResourceDictionary",
            new XElement(P + "ResourceDictionary.MergedDictionaries", new[] {
                "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
                "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
                "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
                "RemoteX;component/Resources/Converter/Converter.xaml",
                "RemoteX;component/Resources/Theme/Markdown.xaml",
                "RemoteX;component/Resources/Icons/SVG.xaml",
                "RemoteX;component/Resources/Languages/zh-cn.xaml"
            }.Select(path => new XElement(P + "ResourceDictionary", new XAttribute("Source", "pack://application:,,,/" + path)))));
        foreach (var path in new[] { "Ui/View/Launcher/FluentLauncher.xaml", "Ui/View/Launcher/LauncherDictionary.xaml" })
        {
            var root = XDocument.Load(Path.Combine(Root, path)).Root!;
            foreach (var element in root.Elements().Where(e => e.Name.LocalName != "ResourceDictionary.MergedDictionaries"))
            {
                var copy = new XElement(element); Namespaces(root, copy); dictionary.Add(copy);
            }
        }
        return dictionary;
    }
    private static UserControl Page(string path)
    {
        var root = XDocument.Load(Path.Combine(Root, path)).Root!;
        var ownResources = root.Element(P + "UserControl.Resources")!;
        var dictionary = Resources();
        foreach (var resource in ownResources.Element(P + "ResourceDictionary")!.Elements().Where(e => e.Name.LocalName != "ResourceDictionary.MergedDictionaries"))
            dictionary.Add(new XElement(resource));
        ownResources.ReplaceNodes(dictionary);
        // Use the actual title XAML as the content template in place of Stylet view activation.
        var title = XDocument.Load(Path.Combine(Root, "Ui/View/Launcher/ServerTitleView.xaml")).Root!;
        var titleTemplate = new XElement(P + "DataTemplate", new XAttribute(X + "Key", "FixtureTitle"), new XElement(title.Element(P + "Grid")!));
        Namespaces(title, titleTemplate); dictionary.Element(P + "ResourceDictionary.MergedDictionaries")!.AddAfterSelf(titleTemplate);
        foreach (var attr in root.DescendantsAndSelf().Attributes(Stylet + "View.Model").ToArray())
        {
            attr.Parent!.SetAttributeValue("Content", attr.Value); attr.Parent.SetAttributeValue("ContentTemplate", "{StaticResource FixtureTitle}"); attr.Remove();
        }
        foreach (var setter in root.Descendants(P + "Setter").Where(e => (string?)e.Attribute("Property") == "s:View.Model"))
        {
            setter.SetAttributeValue("Property", "Content");
            setter.AddAfterSelf(new XElement(P + "Setter", new XAttribute("Property", "ContentTemplate"), new XAttribute("Value", "{StaticResource FixtureTitle}")));
        }
        foreach (var attribute in root.DescendantsAndSelf().Attributes().Where(a => a.Value.Contains("AncestorType=local:ServerSelectionsView")))
            attribute.Value = attribute.Value.Replace("AncestorType=local:ServerSelectionsView", "AncestorType=UserControl");
        Clean(root);
        return (UserControl)XamlReader.Parse(Normalize(root));
    }
    private static void Namespaces(XElement source, XElement target)
    {
        foreach (var ns in source.Attributes().Where(a => a.IsNamespaceDeclaration)) target.SetAttributeValue(ns.Name, ns.Value);
    }
    private static void Clean(XElement root)
    {
        foreach (var attribute in root.DescendantsAndSelf().Attributes().Where(a => a.Name == X + "Class" || a.Name.NamespaceName.Contains("expression/blend") || a.Name.Namespace == Stylet
            || new[] { "PreviewKeyDown", "PreviewMouseDown", "PreviewMouseMove", "MouseDown", "Click", "MouseEnter", "MouseLeave", "MouseWheel", "PreviewMouseRightButtonDown", "PreviewMouseLeftButtonDown", "PreviewMouseLeftButtonUp", "PreviewMouseDoubleClick", "Executed" }.Contains(a.Name.LocalName)).ToArray()) attribute.Remove();
    }
    private static string Normalize(XElement root) => System.Text.RegularExpressions.Regex.Replace(root.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
    public void Appearance(bool dark = false, bool highContrast = false)
    {
        var type = typeof(FluentPage).Assembly.GetType("_1RM.Service.FluentAppearanceService")!;
        foreach (var element in new FrameworkElement[] { View, Results, Quick, Note })
        {
            type.GetMethod("ApplyPalette", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { element.Resources, dark, highContrast });
            type.GetMethod("ApplyAliases", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { element.Resources });
        }
    }
    public void Prepare(double font = 13, double workWidth = 1920, double workHeight = 1080, string mode = "results", bool note = false, bool fluent = true)
    {
        FluentPage.SetActive(View, fluent);
        FluentLauncher.SetQuickMode(Quick, true);
        var metricsType = typeof(FluentLauncher).Assembly.GetType("_1RM.Service.FluentLauncherMetrics")!;
        var family = new FontFamily("Microsoft YaHei");
        var metrics = Activator.CreateInstance(metricsType, fluent, font, workWidth, workHeight, family.LineSpacing)!;
        double Metric(string name) => (double)metricsType.GetProperty(name)!.GetValue(metrics)!;
        double Height(int count, bool action, bool filter) => (double)metricsType.GetMethod("HeightFor")!.Invoke(metrics, new object[] { count, action, filter })!;
        FluentLauncher.SetRowHeight(View, Metric("RowHeight")); FluentLauncher.SetSearchHeight(View, Metric("SearchHeight"));
        FluentLauncher.SetActionHeight(View, Metric("ActionHeight")); FluentLauncher.SetFilterHeight(View, Metric("FilterHeight"));
        FluentLauncher.SetCompact(View, Metric("Width") < 380 || font > 16);
        foreach (var element in new FrameworkElement[] { View, Results, Quick, Note })
        {
            foreach (var pair in new[] { ("GlobalFontSizeBody", font), ("GlobalFontSizeSmall", Math.Max(8, font - 2)), ("GlobalFontSizeTitle", Math.Min(20, font + 6)) }) element.Resources[pair.Item1] = pair.Item2;
            element.Resources["GlobalFontFamily"] = family;
        }
        View.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, family);
        var quick = mode == "quick"; var actions = mode == "actions";
        _resultHost.Visibility = quick ? Visibility.Collapsed : Visibility.Visible;
        _quickHost.Visibility = quick ? Visibility.Visible : Visibility.Collapsed;
        Actions.Visibility = actions ? Visibility.Visible : Visibility.Hidden;
        _noteHost.Visibility = note ? Visibility.Visible : Visibility.Collapsed;
        var height = Height(quick ? QuickData.ConnectHistory.Count : actions ? Model.Actions.Count : Model.VmServerList.Count, actions, !quick && Model.TagFilters.Count > 0);
        _content.Width = Metric("Width"); _content.Height = height;
        _content.Clip = new RectangleGeometry(new Rect(0, 0, _content.Width, height), 8, 8);
        Note.Width = Math.Max(0, Math.Min(320, workWidth - Metric("Width") - 60)); Note.Height = Height(8, false, false);
        Model.Refresh();
        Layout();
    }
    public void Layout()
    {
        View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        View.Arrange(new Rect(new Point(), View.DesiredSize)); View.UpdateLayout(); Drain(); View.UpdateLayout();
    }
    public static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject => SessionHeaderFixture.Descendants<T>(root);
    public ListBoxItem Result(int index) => (ListBoxItem)ResultList.ItemContainerGenerator.ContainerFromIndex(index);
    public void Dispose()
    {
        foreach (var child in Descendants<FrameworkElement>(View).Reverse().ToArray()) child.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        View.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
}
