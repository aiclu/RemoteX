using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using _1RM;
using _1RM.Service;
using _1RM.View;

class Program
{
    public sealed class NoOpCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => throw new InvalidOperationException("The render fixture must not execute commands.");
    }
    public class DialogFixture
    {
        public string Title { get; set; } = "本地样例";
        public string DisplayName => "RemoteX · 本地样例";
        public string Text => string.Concat(Enumerable.Repeat("这是一段用于检查自动换行与滚动的模拟提示，不执行实际操作。", 24));
        public string ValidateMessage => "模拟校验提示：请检查输入。";
        public string Response { get; set; } = "demo";
        public string UserName { get; set; } = "demo-user";
        public string Password { get; set; } = "";
        public string PrivateKey { get; set; } = "";
        public bool CanUsePrivateKeyForConnect { get; set; } = true;
        public bool CanRememberInfo { get; set; } = true;
        public bool UsePrivateKeyForConnect { get; set; }
        public bool IsRememberInfo { get; set; }
        public MessageBoxImage Icon => MessageBoxImage.Information;
        public FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public TextAlignment TextAlignment => TextAlignment.Left;
        public object[] ButtonList => new object[] { new { Label = "确定", Value = MessageBoxResult.OK }, new { Label = "取消", Value = MessageBoxResult.Cancel } };
        public ICommand CmdSave { get; } = new NoOpCommand();
        public ICommand CmdCancel { get; } = new NoOpCommand();
        public ICommand CmdQuit { get; } = new NoOpCommand();
    }
    public class QuickFixture
    {
        public string Filter { get; set; } = "";
        public int SelectedIndex { get; set; } = 0;
        public object[] Protocols { get; } = new object[] { new { Protocol = "RDP" }, new { Protocol = "SSH" } };
        public object? SelectedProtocol { get; set; }
        public ObservableCollection<_1RM.View.Launcher.QuickConnectionItem> ConnectHistory { get; } = new ObservableCollection<_1RM.View.Launcher.QuickConnectionItem>();
    }
    public class CardFixture
    {
        public bool IsSelected { get; set; }
        public string DisplayName => "研发测试连接（模拟）";
        public object Server { get; } = new {
            DisplayName = "研发测试连接（模拟）", ProtocolDisplayName = "RDP", SubTitle = "demo.example.invalid:3389",
            ColorHex = "#0078D4", IconImg = new BitmapImage(new Uri("pack://application:,,,/RemoteX;component/Resources/Image/Logo/logo64.png"))
        };
        public object DataSource { get; } = new { Status = _1RM.Service.DataSource.DAO.EnumDatabaseStatus.OK };
    }
    public class TreeFixtureNode
    {
        public string Name { get; set; } = "模拟目录";
        public bool IsFolder { get; set; }
        public bool IsRootFolder { get; set; }
        public bool IsSelected { get; set; }
        public bool IsCheckboxSelected { get; set; }
        public bool IsExpanded { get; set; } = true;
        public CardFixture Server { get; } = new CardFixture();
        public ObservableCollection<TreeFixtureNode> Children { get; } = new();
    }
    public class SettingsFixture
    {
        public EnumMainWindowPage NavigationPage { get; set; } = EnumMainWindowPage.SettingsGeneral;
        public EnumMainWindowPage CurrentPage => NavigationPage;
        public object? SelectedViewModel { get; set; }
    }
    public class AppearanceFixture
    {
        public bool IsFluentPreview { get; set; } = true;
        public string FluentTheme { get; set; } = "System";
    }
    public class EditorFixture
    {
        public object[] ProtocolList { get; } = new[] { "RDP", "RemoteApp", "VNC", "SSH", "Telnet", "Serial", "SFTP", "FTP", "APP" }
            .Select(name => (object)new { ProtocolDisplayName = name, HelpUrl = name == "RemoteApp" || name == "APP" ? "https://example.invalid/help" : "" }).ToArray();
        public object? SelectedProtocol { get; set; }
        public bool IsBuckEdit => false;
        public EditorFixture() { SelectedProtocol = ProtocolList[0]; }
    }
    public class AboutFixture
    {
        public string CurrentVersion { get; set; } = "1.0.16";
        public string CurrentVersionDate { get; set; } = "Local preview";
        public string RepositoryUrl { get; set; } = "https://github.com/aiclu/RemoteX";
        public string NewVersion { get; set; } = "1.0.16（演示）";
        public bool IsUpdatePanelVisible { get; set; } = true;
        public string UpdateStatus { get; set; } = "测试更新状态：仅展示，不检查或下载更新。";
        public bool UpdateFailed { get; set; } = true;
        public double UpdateProgress { get; set; }
    }
    static readonly string Root = FindRoot();
    static readonly string AppDir = Path.Combine(Root, "Ui", "bin", "FluentDialogsTest");
    static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RemoteX.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Run this fixture from a RemoteX checkout.");
    }
    [STAThread] static void Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            var path = Path.Combine(AppDir, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        var app = new Application();
        // Exercise the same all-language loading path as startup, not just the rendered language.
        var languages = new LanguageService(app.Resources);
        if (languages.Resources.Count != LanguagesResources.Files.Length)
            throw new InvalidOperationException("Not all bundled languages loaded.");
        Console.WriteLine($"Startup language resources loaded: {languages.Resources.Count}");
        foreach (var resource in new[] {
            "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
            "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
            "RemoteX;component/Resources/Converter/Converter.xaml",
            "RemoteX;component/Resources/Theme/Markdown.xaml",
            "RemoteX;component/Resources/Icons/SVG.xaml",
            "RemoteX;component/Resources/Languages/zh-cn.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + resource) });
        var document = XDocument.Load(Root + @"\Ui\App.xaml");
        var dictionary = document.Descendants().First(x => x.Name.LocalName == "ResourceDictionary" && !x.HasAttributes);
        foreach (var ns in document.Root!.Attributes().Where(x => x.IsNamespaceDeclaration)) dictionary.SetAttributeValue(ns.Name, ns.Value);
        app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(dictionary.ToString()));
        var cfg = (Configuration)RuntimeHelpers.GetUninitializedObject(typeof(Configuration));
        cfg.Theme = new ThemeConfig();
        cfg.General = new GeneralConfig();
        cfg.Theme.Fluent.Enabled = true;
        var config = (ConfigurationService)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationService));
        typeof(ConfigurationService).GetField("_cfg", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(config, cfg);
        var type = typeof(FluentPage).Assembly.GetType("_1RM.Service.FluentAppearanceService")!;
        var appearance = Activator.CreateInstance(type, config)!;
        IoC.GetByType = (t, key) => t == type ? appearance : t == typeof(ConfigurationService) ? config : null;
        foreach (var dark in new[] { false, true })
        foreach (var width in new[] { 1000, 600 })
        {
            cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
            RenderAboutNavigation(dark, width < 720, type);
            RenderNavigationAlignment(dark, width < 720, type);
            RenderSessionHeader(width, dark, type);
            var cards = new WrapPanel();
            for (var i = 0; i < 8; i++) cards.Children.Add(new _1RM.Controls.ServerCardItem {
                DataContext = new CardFixture { IsSelected = i == 0 }, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Top });
            Render(cards, "Cards", width, dark, type, width == 600 ? 18 : 13);
            var tree = new _1RM.View.ServerView.Tree.ServerTreeView();
            var rootNode = new TreeFixtureNode { IsFolder = true, IsRootFolder = true, Name = "本地渲染数据源" };
            var folderNode = new TreeFixtureNode { IsFolder = true, Name = "研发环境" };
            for (var i = 0; i < 8; i++) folderNode.Children.Add(new TreeFixtureNode { IsSelected = i == 0, IsCheckboxSelected = i == 1 });
            rootNode.Children.Add(folderNode);
            tree.DataContext = new { RootNodes = new[] { rootNode } };
            Render(tree, "Tree", width, dark, type, width == 600 ? 18 : 13);
            typeof(SecondaryVerificationHelper).GetField("_isEnabled", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, false);
            var settings = new _1RM.View.Settings.SettingsPageView();
            var presenter = (ContentControl)settings.FindName("SettingsContent");
            BindingOperations.ClearBinding(presenter, Stylet.Xaml.View.ModelProperty);
            settings.DataContext = new SettingsFixture();
            presenter.Content = new _1RM.View.Settings.General.GeneralSettingView();
            Render(settings, "Settings", width, dark, type);
            if (width == 600) Render(settings, "Settings-LargeFont", width, dark, type, 18);
            var general = new _1RM.View.Settings.General.GeneralSettingView();
            general.InitializeComponent();
            Render(general, "General", width, dark, type);
            var editor = new _1RM.View.Editor.ServerEditorPageView();
            editor.InitializeComponent();
            editor.DataContext = new EditorFixture();
            Render(editor, "Editor", width, dark, type);
            if (width == 600) Render(editor, "Editor-LargeFont", width, dark, type, 18);
            var header = (FrameworkElement)editor.FindName("ProtocolHeader");
            var body = (FrameworkElement)editor.FindName("ScrollViewerMain");
            if (body.TranslatePoint(new Point(), editor).Y < header.TranslatePoint(new Point(0, header.ActualHeight), editor).Y)
                throw new InvalidOperationException("Protocol header overlaps the editor body.");
            RenderSearch(dark, width == 600, type);
            var about = new AboutPageView();
            about.InitializeComponent();
            about.DataContext = new AboutFixture();
            // Render the real content without displaying a native window or invoking updater code.
            var content = (FrameworkElement)about.Content;
            about.Content = null;
            content.DataContext = about.DataContext;
            Render(content, "About", 480, dark, type);
            foreach (var dialogName in new[] { "Utils.MessageBoxPageView", "Utils.InputBoxView",
                "Editor.PasswordPopupDialogView", "Editor.IconPopupDialogView", "Editor.DataSourceSelectorView",
                "Editor.Forms.AlternativeCredential.AlternativeCredentialEditView", "Editor.Forms.Argument.ArgumentEditView",
                "Settings.DataSource.SqliteSettingView", "Settings.DataSource.MysqlSettingView", "Settings.DataSource.PgsqlSettingView",
                "Host.ConnectionInfoView" })
            {
                var dialogType = typeof(FluentPage).Assembly.GetType("_1RM.View." + dialogName)!;
                var dialog = (FrameworkElement)Activator.CreateInstance(dialogType)!;
                dialogType.GetMethod("InitializeComponent")?.Invoke(dialog, null);
                FrameworkElement surface = dialog;
                if (dialog is Window window)
                {
                    surface = (FrameworkElement)window.Content;
                    window.Content = null;
                    surface.Resources.MergedDictionaries.Add(window.Resources);
                }
                surface.DataContext = new DialogFixture();
                if (dialogName == "Host.ConnectionInfoView") surface.DataContext = new {
                    WindowTitle = "连接信息 — 本地渲染样例", Summary = "模拟快照，不读取活动会话",
                    Sections = new[] { new _1RM.View.Host.ConnectionInfoSection("网络详细信息", new[] {
                        new _1RM.View.Host.ConnectionInfoRow("传输协议", "不可用"),
                        new _1RM.View.Host.ConnectionInfoRow("往返时间", "16 ms（模拟）"),
                        new _1RM.View.Host.ConnectionInfoRow("远程计算机", "demo-server.example.invalid") }) }
                };
                Render(surface, "Dialog-" + dialogName.Split('.').Last(), width == 1000 ? 560 : 360, dark, type, width == 600 ? 18 : 13);
            }
            var quick = new _1RM.View.Launcher.QuickConnectionView();
            var quickData = new QuickFixture(); quickData.SelectedProtocol = quickData.Protocols[0];
            quick.DataContext = quickData;
            Render(quick, "Launcher-Empty", width == 1000 ? 400 : 280, dark, type);
            for (var i = 0; i < 12; i++) quickData.ConnectHistory.Add(new _1RM.View.Launcher.QuickConnectionItem { Protocol = "RDP", Host = $"demo-server-{i}.example.invalid:3389" });
            Render(quick, "Launcher-Results", width == 1000 ? 400 : 280, dark, type, width == 600 ? 18 : 13);
            foreach (var name in new[] {
                "Settings.Theme.ThemeSettingView", "Settings.Launcher.LauncherSettingView",
                "Settings.DataSource.DataSourceView", "Settings.CredentialVault.CredentialVaultView",
                "Settings.ProtocolConfig.ProtocolRunnerSettingsPageView",
                "Settings.ProtocolConfig.ExternalRunnerSettings", "Settings.ProtocolConfig.ExternalSshRunnerSettings",
                "Editor.Forms.RdpFormView", "Editor.Forms.SshFormView", "Editor.Forms.VncFormView",
                "Editor.Forms.FtpFormView", "Editor.Forms.SftpFormView", "Editor.Forms.TelnetFormView",
                "Editor.Forms.SerialFormView", "Editor.Forms.RdpAppFormView", "Editor.Forms.LocalAppFormView",
                "Editor.Forms.Utils.HostView", "Editor.Forms.Utils.CredentialView" })
            {
                var viewType = typeof(FluentPage).Assembly.GetType("_1RM.View." + name)!;
                var subpage = (FrameworkElement)Activator.CreateInstance(viewType)!;
                viewType.GetMethod("InitializeComponent")?.Invoke(subpage, null);
                if (name.Contains("ThemeSetting")) subpage.DataContext = new { Appearance = new AppearanceFixture { FluentTheme = dark ? "Dark" : "Light" } };
                Render(subpage, name.Split('.').Last(), width, dark, type);
            }
        }
        ((IDisposable)appearance).Dispose();
    }
    static void RenderSearch(bool dark, bool compact, Type service)
    {
        var workspace = new FluentWorkspace();
        var search = (TextBox)workspace.FindName("Search");
        ((Panel)search.Parent).Children.Remove(search);
        search.ClearValue(TextBox.TextProperty);
        search.Margin = new Thickness(12);
        search.VerticalAlignment = VerticalAlignment.Center;
        FluentWorkspace.SetIsCompact(search, compact);
        Render(search, "Search-Empty", compact ? 280 : 640, dark, service, compact ? 20 : 13);
        search.Text = "研发测试 #RDP";
        Render(search, "Search-Typed", compact ? 280 : 640, dark, service, compact ? 20 : 13);
        search.IsEnabled = false;
        Render(search, "Search-Disabled", compact ? 280 : 640, dark, service, compact ? 20 : 13);
    }
    static void RenderNavigationAlignment(bool dark, bool compact, Type service)
    {
        var workspace = new FluentWorkspace();
        var samples = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        var labels = new[] { "SettingsLabel", "AboutLabel", "AppearanceLabel" }.Select(n => (TextBlock)workspace.FindName(n)).ToArray();
        foreach (var label in labels)
        {
            var button = (Button)((FrameworkElement)label.Parent).Parent;
            ((Panel)button.Parent).Children.Remove(button);
            button.DataContext = new { AboutViewModel = new { NewVersion = "1.0.99（模拟）" } };
            label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            samples.Children.Add(button);
        }
        Render(samples, compact ? "NavigationCompact" : "NavigationAligned", compact ? 56 : 176, dark, service, compact ? 13 : 18);
        if (!compact)
        {
            var left = labels[0].TranslatePoint(new Point(), samples).X;
            if (labels.Any(label => Math.Abs(label.TranslatePoint(new Point(), samples).X - left) > 0.1))
                throw new InvalidOperationException("Sidebar labels are not aligned.");
        }
    }
    static void RenderAboutNavigation(bool dark, bool compact, Type service)
    {
        var samples = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        foreach (var version in new[] { "", "1.0.99（模拟）" })
        {
            var workspace = new FluentWorkspace();
            var button = (Button)workspace.FindName("AboutNavigation");
            ((Panel)button.Parent).Children.Remove(button);
            button.DataContext = new { AboutViewModel = new { NewVersion = version } };
            ((TextBlock)workspace.FindName("AboutLabel")).Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            button.Margin = new Thickness(8); samples.Children.Add(button);
            button.Measure(new Size(200, 60));
            button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var badge = (Border)workspace.FindName("AboutUpdateBadge");
            if (badge.Visibility != (version.Length == 0 ? Visibility.Collapsed : Visibility.Visible))
                throw new InvalidOperationException($"About update indicator for '{version}' was {badge.Visibility}.");
        }
        Render(samples, compact ? "MenuAboutCompact" : "MenuAboutExpanded", compact ? 200 : 320, dark, service);
    }
    static void RenderSessionHeader(int width, bool dark, Type service)
    {
        // Extract the actual header style; never construct TabWindowView or a protocol host.
        var source = XDocument.Load(Path.Combine(Root, "Ui/View/Host/TabWindowView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var resources = new XElement(presentation + "ResourceDictionary");
        foreach (var attribute in source.Root!.Attributes().Where(a => a.IsNamespaceDeclaration))
            resources.SetAttributeValue(attribute.Name, attribute.Value.StartsWith("clr-namespace:_1RM") ? attribute.Value + ";assembly=RemoteX" : attribute.Value);
        resources.SetAttributeValue(XNamespace.Xmlns + "dragablz", "clr-namespace:Dragablz;assembly=Dragablz");
        resources.Add(new XElement(presentation + "ResourceDictionary.MergedDictionaries",
            new XElement(presentation + "ResourceDictionary", new XAttribute("Source", "/RemoteX;component/Resources/Theme/FluentSessions.xaml"))));
        resources.Add(new XElement(source.Descendants(presentation + "Style").First(e => (string?)e.Attribute(x + "Key") == "DragableTabHeaderItemStyle")));
        var resourceXaml = System.Text.RegularExpressions.Regex.Replace(resources.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
        var dictionary = (ResourceDictionary)XamlReader.Parse(resourceXaml);
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        stack.Resources.MergedDictionaries.Add(dictionary);
        var itemType = Assembly.Load("Dragablz").GetType("Dragablz.DragablzItem")!;
        for (var i = 0; i < 3; i++)
        {
            var item = (ContentControl)Activator.CreateInstance(itemType)!;
            item.Style = (Style)dictionary["DragableTabHeaderItemStyle"];
            item.Width = width == 600 ? 170 : 220;
            item.DataContext = new { DisplayName = i == 0 ? "研发服务器 · 模拟" : "SSH · demo.example.invalid", ColorHex = "#0078D4",
                Content = new { ConnectionId = "synthetic", SettingsPage = new { TabHeaderShowIconButton = false, TabHeaderShowReConnectButton = true, TabHeaderShowCloseButton = true } } };
            itemType.GetProperty("IsSelected")!.SetValue(item, i == 0);
            stack.Children.Add(item);
        }
        Render(stack, "SessionHeaders", width, dark, service, width == 600 ? 18 : 13);
        var buttonsXml = new XElement(source.Descendants(presentation + "StackPanel").First(e => (string?)e.Attribute(x + "Name") == "SessionWindowButtons"));
        foreach (var attribute in resources.Attributes().Where(a => a.IsNamespaceDeclaration)) buttonsXml.SetAttributeValue(attribute.Name, attribute.Value);
        var buttonsXaml = System.Text.RegularExpressions.Regex.Replace(buttonsXml.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
        FrameworkElement buttons;
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
        try { buttons = (FrameworkElement)XamlReader.Parse(buttonsXaml); }
        finally { Application.Current.Resources.MergedDictionaries.Remove(dictionary); }
        buttons.Resources.MergedDictionaries.Add(dictionary);
        Render(buttons, "SessionWindowButtons", width, dark, service, width == 600 ? 18 : 13);
    }
    static void Render(FrameworkElement view, string name, int width, bool dark, Type service, double fontSize = 13)
    {
        var height = name.StartsWith("Search-") ? 80 : name.StartsWith("Navigation") ? 140 : name.StartsWith("Session") || name.StartsWith("MenuAbout") ? 60 : name == "About" ? 460 : name.StartsWith("Dialog-") ? 500 : name == "Launcher-Empty" ? (int)Math.Ceiling(Math.Max(46, fontSize * 1.6 + 20)) : name == "Launcher-Results" ? (int)Math.Ceiling(Math.Max(46, fontSize * 1.6 + 20) + 8 * Math.Max(40, fontSize * 2.4 + 10)) : 680;
        view.Resources["FluentDialogBodyMaxHeight"] = (double)height - 144;
        view.Resources["FluentDialogFieldMaxWidth"] = (double)width - 64;
        FluentLauncher.SetSearchHeight(view, Math.Max(46, fontSize * 1.6 + 20));
        FluentLauncher.SetRowHeight(view, Math.Max(40, fontSize * 2.4 + 10));
        service.GetMethod("ApplyPalette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { view.Resources, dark, false });
        service.GetMethod("ApplyAliases", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { view.Resources });
        FluentPage.SetActive(view, true);
        FluentPage.SetNarrow(view, width < 720);
        view.Resources["GlobalFontSize"] = fontSize;
        view.Resources["GlobalFontSizeBody"] = fontSize;
        view.Resources["TextFontSize"] = fontSize;
        view.Resources["GlobalFontSizeTitle"] = fontSize + 5;
        view.Resources["GlobalFontSizeSubtitle"] = fontSize + 2;
        view.Resources["GlobalFontSizeSmall"] = fontSize - 1;
        view.Width = double.NaN; view.Height = double.NaN;
        var stage = new Grid { Width = width, Height = height, Background = (Brush)view.Resources["FluentCanvas"] };
        stage.Children.Add(view);
        view.SetValue(TextElement.FontFamilyProperty, new FontFamily("Microsoft YaHei"));
        view.SetValue(TextElement.FontSizeProperty, fontSize);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        stage.Measure(new Size(width,height)); stage.Arrange(new Rect(0,0,width,height)); stage.UpdateLayout();
        LoadChildren(view);
        stage.Measure(new Size(width,height)); stage.Arrange(new Rect(0,0,width,height)); stage.UpdateLayout();
        foreach(var scale in new[] {1.0,1.5,2.0})
        {
            var bitmap = new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
            bitmap.Render(stage);
            var directory = AppDir + @"\screenshots"; Directory.CreateDirectory(directory);
            using var file = File.Create(Path.Combine(directory,$"{name}-{(dark?"Dark":"Light")}-{width}-{scale*100:0}.png"));
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
        }
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        stage.Children.Remove(view);
        Console.WriteLine($"Rendered {name} {width} dark={dark}");
    }
    static void LoadChildren(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element) element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            LoadChildren(child);
        }
    }
}
