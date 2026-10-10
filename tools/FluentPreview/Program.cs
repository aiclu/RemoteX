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
    static readonly string AppDir = Environment.GetEnvironmentVariable("REMOTEX_PREVIEW_APP_DIR") ?? Path.Combine(Root, "Ui", "bin", "FluentDialogsTest");
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
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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
        if (Environment.GetCommandLineArgs().Contains("--credential-dialog"))
        {
            // Validation uses the same language resources as the fields, without a real vault.
            IoC.GetByType = (t, key) => t == type ? appearance : t == typeof(ConfigurationService) ? config
                : t == typeof(Shawn.Utils.Interface.ILanguageService) ? languages : null;
            languages.SetLanguage("zh-cn");
            foreach (var dark in new[] { false, true })
            foreach (var scenario in new[] { "Password", "Required", "PrivateKey", "Alternative", "Narrow", "LargeFont" })
            {
                using var fixture = new RemoteX.UiTesting.CredentialDialogFixture(showHost: scenario == "Alternative");
                if (scenario == "Required") fixture.Data.Name = fixture.Data.UserName = fixture.Data.Password = "";
                if (scenario == "PrivateKey") { fixture.Data.IsUsePrivateKey = true; fixture.Data.PrivateKeyPath = @"C:\synthetic\demo-key.ppk"; }
                var width = scenario == "Narrow" ? 300 : 560;
                fixture.Prepare(width, scenario == "LargeFont" || scenario == "Narrow" ? 20 : 13,
                    dark, bodyMaxHeight: scenario == "Narrow" ? 300 : 800);
                foreach (var scale in new[] { 1.0, 1.5, 2.0 })
                {
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(fixture.View.ActualWidth * scale), (int)Math.Ceiling(fixture.View.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(fixture.View);
                    var directory = Path.Combine(AppDir, "screenshots"); Directory.CreateDirectory(directory);
                    using var file = File.Create(Path.Combine(directory, $"CredentialDialog-{scenario}-{(dark ? "Dark" : "Light")}-{scale * 100:0}.png"));
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                }
                Console.WriteLine($"Rendered compiled credential dialog {scenario}, dark={dark}, {fixture.View.ActualWidth}x{fixture.View.ActualHeight}");
            }
            ((IDisposable)appearance).Dispose(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--credentials"))
        {
            foreach (var dark in new[] { false, true })
            foreach (var scenario in new[] { "Password", "Vault", "PrivateKey", "Narrow", "LargeFont", "LongLabels" })
            {
                using var fixture = new RemoteX.UiTesting.CredentialFixture();
                fixture.Data.ShowPrivateKeyInput = scenario == "PrivateKey" || scenario == "Narrow";
                fixture.Data.IsUsePrivateKey = fixture.Data.ShowPrivateKeyInput;
                if (scenario == "Vault") fixture.Data.SelectedCredential = fixture.Data.Credentials[0];
                if (scenario == "LongLabels") fixture.Data.StrBtnUseCredentialsVault = "Credentials vault with a long translated label";
                var width = scenario == "Narrow" || scenario == "LongLabels" ? 300 : 800;
                var font = scenario == "LargeFont" || scenario == "Narrow" ? 20 : 13;
                fixture.Prepare(width, font, dark);
                Render(fixture.View, "Credentials-" + scenario, width, dark, type, font,
                    heightOverride: (int)Math.Ceiling(fixture.View.DesiredSize.Height));
            }
            foreach (var dark in new[] { false, true })
            {
                var compiled = (UserControl)Application.LoadComponent(new Uri("/RemoteX;component/View/Editor/Forms/Utils/CredentialView.xaml", UriKind.Relative));
                compiled.DataContext = new RemoteX.UiTesting.CredentialFixture.Model();
                Render(compiled, "Credentials-Compiled", 800, dark, type, beforeCapture: () => {
                    var panel = (Border)compiled.FindName("CredentialPanel");
                    if (!double.IsNaN(panel.Width) || panel.BorderThickness != new Thickness(1))
                        throw new InvalidOperationException("Compiled credential panel did not apply Fluent styling.");
                }, heightOverride: 280);
            }
            ((IDisposable)appearance).Dispose(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--launcher"))
        {
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 60000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var scenario in new[] { "Results", "Actions", "Quick", "Note", "NoteEdit", "Filters", "Narrow", "Empty", "HiddenAccounts", "LargeFont", "SmallFont", "Highlight" })
            {
                using var fixture = new RemoteX.UiTesting.LauncherFixture(scenario == "Empty" ? 0 : 16);
                if (scenario == "Filters")
                {
                    fixture.Model.TagFilters.Add(_1RM.Model.TagFilter.Create("研发环境 · very-long-tag", _1RM.Model.TagFilter.FilterType.Included));
                    fixture.Model.TagFilters.Add(_1RM.Model.TagFilter.Create("排除生产", _1RM.Model.TagFilter.FilterType.Excluded));
                    fixture.Model.Filter = "#研发环境 #-生产";
                }
                fixture.Model.ShowCredentials = scenario != "HiddenAccounts";
                if (scenario == "Highlight") foreach (var row in fixture.Model.VmServerList)
                    row.LauncherMainTitleViewModel.HighLight(row.DisplayName.Select((_, i) => i < 2).ToList());
                fixture.Appearance(dark);
                fixture.Prepare(scenario == "LargeFont" ? 20 : scenario == "SmallFont" ? 10 : 13,
                    scenario == "Narrow" ? 320 : 1920, scenario == "Narrow" ? 400 : 1080,
                    scenario.ToLowerInvariant(), scenario.StartsWith("Note"));
                if (scenario == "NoteEdit")
                {
                    ((FrameworkElement)fixture.Note.FindName("MarkdownViewer")).Visibility = Visibility.Collapsed;
                    ((Grid)fixture.Note.FindName("GridEditor")).Visibility = Visibility.Visible;
                    ((TextBox)fixture.Note.FindName("TbMarkdown")).Text = "# 模拟编辑\n\n仅展示编辑样式，不保存备注。";
                    fixture.Layout();
                }
                foreach (var scale in new[] { 1.0, 1.5, 2.0 })
                {
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(fixture.View.ActualWidth * scale), (int)Math.Ceiling(fixture.View.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(fixture.View);
                    var directory = Path.Combine(AppDir, "screenshots"); Directory.CreateDirectory(directory);
                    using var file = File.Create(Path.Combine(directory, $"Launcher-{scenario}-{(dark ? "Dark" : "Light")}-{scale * 100:0}.png"));
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                }
                Console.WriteLine($"Rendered actual launcher {scenario}, dark={dark}, {fixture.View.ActualWidth}x{fixture.View.ActualHeight}");
            }
            // Also load compiled controls with their real namespace/ancestor bindings. The loose
            // fixture deliberately removes event hookups, so this catches BAML-only regressions.
            foreach (var dark in new[] { false, true })
            {
                using var fixture = new RemoteX.UiTesting.LauncherFixture();
                var results = new _1RM.View.Launcher.ServerSelectionsView();
                FluentPage.SetActive(results, true);
                ((ListBox)results.FindName("ListBoxSelections")).ItemTemplate = (DataTemplate)results.Resources[new DataTemplateKey(typeof(ProtocolBaseViewModel))];
                results.DataContext = fixture.Model;
                Render(results, "Launcher-CompiledResults", 400, dark, type, 13, () => {
                    var title = RemoteX.UiTesting.LauncherFixture.Descendants<_1RM.View.Launcher.LauncherHighlightedText>(results).First(x => x.Name == "ResultTitle");
                    if (title.FontSize != 13 || title.FontWeight != FontWeights.SemiBold) throw new InvalidOperationException("Compiled launcher title style did not apply.");
                    fixture.Model.ShowCredentials = false; fixture.Model.Refresh(); RemoteX.UiTesting.LauncherFixture.Drain();
                    foreach (var subtitle in RemoteX.UiTesting.LauncherFixture.Descendants<_1RM.View.Launcher.LauncherHighlightedText>(results).Where(x => x.Name == "ResultSubtitle"))
                        if (subtitle.Title != null || subtitle.ToolTip != null) throw new InvalidOperationException("Hidden account exposed by compiled bindings.");
                    fixture.Model.ShowCredentials = true; fixture.Model.Refresh();
                }, 414);
                var quick = new _1RM.View.Launcher.QuickConnectionView();
                FluentPage.SetActive(quick, true); quick.DataContext = fixture.QuickData;
                Render(quick, "Launcher-CompiledQuick", 400, dark, type, 13, heightOverride: 414);
            }
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--session-tabs"))
        {
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 30000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var width in new[] { 1000, 600 })
            {
                cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
                RenderSessionHeader(width, dark, type);
            }
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--home"))
        {
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 60000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var width in new[] { 1120, 800, 600, 300 })
            {
                cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
                var fixture = new RemoteX.UiTesting.HomeFixture();
                Render(fixture.View, "Home", width, dark, type, width == 600 ? 20 : 13, () =>
                {
                    fixture.Prepare(width, dark, type); fixture.CheckGeometry(width);
                    if (width == 1120) { fixture.CheckSelectionAndCommands(); fixture.CheckFullResize(width, dark, type); fixture.Prepare(width, dark, type); }
                }, 610);
                if (width != 800) continue;
                var states = new RemoteX.UiTesting.HomeFixture();
                states.ActiveServerViewModel.VmServerList[0].DataSource.IsWritable = false;
                states.ActiveServerViewModel.VmServerList[0].DataSource.DataSourceName = "只读数据源 · 很长的名称用于检查省略和提示";
                states.ActiveServerViewModel.VmServerList[5].DataSource.Status = _1RM.Service.DataSource.DAO.EnumDatabaseStatus.LostConnection;
                states.ActiveServerViewModel.VmServerList[5].DataSource.ReconnectInfo = "模拟错误：无法访问数据源，请检查网络连接。长文本自动换行。";
                states.ActiveServerViewModel.VmServerList.Add(new() { DataSource = new() { DataSourceName = "空数据源" }, IsVisible = false });
                Render(states.View, "Home-States", width, dark, type, 20, () => states.Prepare(width, dark, type), 610);
                var single = new RemoteX.UiTesting.HomeFixture(); single.ActiveServerViewModel.Collection.GroupDescriptions.Clear();
                Render(single.View, "Home-Ungrouped", width, dark, type, 13, () => single.Prepare(width, dark, type), 610);
                var collapsed = new RemoteX.UiTesting.HomeFixture();
                foreach (var row in collapsed.ActiveServerViewModel.VmServerList) row.GroupedIsExpanded = false;
                Render(collapsed.View, "Home-Collapsed", width, dark, type, 13, () => { collapsed.Prepare(width, dark, type); collapsed.CheckGeometry(width); }, 610);
                foreach (var longWidth in new[] { 1000, 300 })
                {
                    var longLabels = new RemoteX.UiTesting.HomeFixture(); longLabels.UseLongLabels();
                    Render(longLabels.View, "Home-LongLabels", longWidth, dark, type, 24, () => { longLabels.Prepare(longWidth, dark, type); longLabels.CheckGeometry(longWidth); }, 720);
                }
            }
            var interactions = new RemoteX.UiTesting.SourceGroupFixture(realRows: true);
            FluentPage.SetActive(interactions.Page, true); FluentWorkspace.SetIsPreview(interactions.Page, true);
            interactions.CheckInteractions();
            var large = new RemoteX.UiTesting.SourceGroupFixture(true, 600, realRows: true);
            FluentPage.SetActive(large.Page, true); FluentWorkspace.SetIsPreview(large.Page, true);
            large.CheckVirtualization();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--launcher-settings"))
        {
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 30000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var width in new[] { 1000, 600 })
            {
                cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
                var fixture = new RemoteX.UiTesting.LauncherSettingsFixture();
                var settings = new _1RM.View.Settings.SettingsPageView();
                var presenter = (ContentControl)settings.FindName("SettingsContent");
                BindingOperations.ClearBinding(presenter, Stylet.Xaml.View.ModelProperty);
                settings.DataContext = new SettingsFixture { NavigationPage = EnumMainWindowPage.SettingsLauncher };
                presenter.Content = fixture.Page;
                foreach (var bottom in new[] { false, true })
                    Render(settings, bottom ? "Settings-Launcher-Bottom" : "Settings-Launcher-Top", width, dark, type,
                        width == 600 ? 20 : 13, () => fixture.CheckScrolling(bottom), 480);
            }
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--tag-management"))
        {
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 30000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var width in new[] { 800, 420 })
            {
                cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
                var fixture = new RemoteX.UiTesting.SourceGroupFixture(true);
                FluentWorkspace.SetIsPreview(fixture.Page, true);
                fixture.ShowTagManagement();
                Render(fixture.Page, "TagManagement", width, dark, type);
                fixture.CheckTagManagementTransitions();
            }
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--source-groups"))
        {
            // Independent watchdog also bounds any accidental virtualization/layout loop.
            using var watchdog = new System.Threading.Timer(_ => Environment.Exit(2), null, 45000, System.Threading.Timeout.Infinite);
            foreach (var dark in new[] { false, true })
            foreach (var width in new[] { 800, 420 })
            {
                cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
                foreach (var expanded in new[] { false, true })
                {
                    var fixture = new RemoteX.UiTesting.SourceGroupFixture(expanded);
                    FluentWorkspace.SetIsPreview(fixture.Page, true);
                    Render(fixture.Page, expanded ? "Sources-Expanded" : "Sources-Collapsed", width, dark, type, width == 420 ? 20 : 13);
                    foreach (var expander in RemoteX.UiTesting.SourceGroupFixture.Descendants(fixture.Page).OfType<Expander>())
                        Console.WriteLine($"Source={expander.DataContext} expanded={expander.IsExpanded} validation={Validation.GetHasError(expander)} size={expander.ActualWidth}x{expander.ActualHeight}");
                }
                var states = new RemoteX.UiTesting.SourceGroupFixture();
                states.VmServerList[0].DataSource.IsWritable = false;
                states.VmServerList[0].DataSource.DataSourceName = "只读数据源 · 很长的名称用于检查截断与完整提示";
                states.VmServerList[3].DataSource.Status = _1RM.Service.DataSource.DAO.EnumDatabaseStatus.LostConnection;
                states.VmServerList[3].DataSource.ReconnectInfo = "模拟重连信息：暂时无法访问数据源，请检查网络连接。较长说明应当换行，不挤占其他操作按钮。";
                states.VmServerList.Add(new RemoteX.UiTesting.SourceGroupFixture.Row { DataSource = new() { DataSourceName = "空数据源（模拟占位）" }, IsVisible = false, GroupedIsExpanded = true });
                FluentWorkspace.SetIsPreview(states.Page, true);
                Render(states.Page, "Sources-States", width, dark, type, width == 420 ? 24 : 13);
                var cardGroups = new RemoteX.UiTesting.SourceGroupFixture(true, 4, true);
                Render(cardGroups.Page, "Sources-CardView", width, dark, type);
            }
            var interactions = new RemoteX.UiTesting.SourceGroupFixture();
            FluentPage.SetActive(interactions.Page, true); FluentWorkspace.SetIsPreview(interactions.Page, true);
            interactions.CheckInteractions();
            var large = new RemoteX.UiTesting.SourceGroupFixture(true, 600);
            FluentPage.SetActive(large.Page, true); FluentWorkspace.SetIsPreview(large.Page, true);
            large.CheckVirtualization();
            var largeCards = new RemoteX.UiTesting.SourceGroupFixture(true, 600, true);
            FluentPage.SetActive(largeCards.Page, true);
            largeCards.CheckVirtualization();
            return;
        }
        foreach (var dark in new[] { false, true })
        foreach (var width in new[] { 1000, 600 })
        {
            cfg.Theme.Fluent.Theme = dark ? "Dark" : "Light";
            RenderAboutNavigation(dark, width < 720, type);
            RenderNavigationAlignment(dark, width < 720, type);
            RenderSidebarTags(dark, width < 720 ? 140 : 176, type);
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
    static void RenderSidebarTags(bool dark, int width, Type service)
    {
        var workspace = new FluentWorkspace();
        var tags = (ListBox)workspace.FindName("Tags");
        ((Panel)tags.Parent).Children.Remove(tags);
        tags.Visibility = Visibility.Visible;
        tags.Resources.MergedDictionaries.Add(workspace.Resources);
        var source = new ObservableCollection<_1RM.Model.Tag> {
            new("ordinary", false, 0), new("pinned", true, 1), new("很长的固定标签名称·模拟数据", true, 2)
        };
        for (var i = 0; i < 400; i++) source.Add(new _1RM.Model.Tag("tag-" + i.ToString("D3"), false, i + 3));
        var type = typeof(FluentWorkspace).Assembly.GetType("_1RM.View.FluentTagView")!;
        using var sorted = (IDisposable)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { source }, null)!;
        tags.ItemsSource = (System.Collections.IEnumerable)type.GetProperty("View", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(sorted)!;
        // Keep a stale ListBox selection while the actual filter activates another row.
        tags.SelectedItem = source[0];
        tags.DataContext = new { ActiveServerViewModel = new { SelectedTabName = source[1].Name } };
        Render(tags, "SidebarTags", width, dark, service, width == 140 ? 18 : 13);
        if (tags.ItemContainerGenerator.ContainerFromIndex(300) != null)
            throw new InvalidOperationException("Off-screen sidebar tags must remain virtualized.");
        tags.ItemsSource = null;
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
        using var fixture = new RemoteX.UiTesting.SessionHeaderFixture(width < 800 ? 12 : 3, longTitle: true);
        Render(fixture.View, "SessionHeaders", width, dark, service, width == 600 ? 20 : 13, () => {
            fixture.SetFluent(true);
            fixture.Layout(width, 60);
            fixture.CheckActionBounds();
        });
    }
    static void Render(FrameworkElement view, string name, int width, bool dark, Type service, double fontSize = 13,
        Action? beforeCapture = null, int? heightOverride = null)
    {
        var height = name.StartsWith("Search-") ? 80 : name.StartsWith("Navigation") ? 140 : name.StartsWith("Session") || name.StartsWith("MenuAbout") ? 60 : name == "About" ? 460 : name.StartsWith("Dialog-") ? 500 : name == "Launcher-Empty" ? (int)Math.Ceiling(Math.Max(46, fontSize * 1.6 + 20)) : name == "Launcher-Results" ? (int)Math.Ceiling(Math.Max(46, fontSize * 1.6 + 20) + 8 * Math.Max(40, fontSize * 2.4 + 10)) : 680;
        height = heightOverride ?? height;
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
        beforeCapture?.Invoke();
        stage.UpdateLayout();
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
