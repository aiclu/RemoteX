using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using _1RM;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Service.RustSsh;
using _1RM.View;
using _1RM.View.ErrorReport;
using _1RM.View.Host.ProtocolHosts;
using _1RM.View.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteX.UiTesting;
using Shawn.Utils.Interface;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class TerminalFailureUiTests
{
    private sealed class ResourceLanguageService : ILanguageService
    {
        public void AddXamlLanguageResources(string code, string fullName) { }
        public string Translate(string key) => Application.Current.TryFindResource(key) as string ?? key;
        public string Translate(Enum key) => Translate(key.ToString());
        public string Translate(string key, params object[] values) => string.Format(Translate(key), values);
    }

    private static void Sta(Action action) => WpfTestHost.Run(() =>
    {
        var previous = IoC.GetByType;
        var resources = new ResourceDictionary();
        var configuration = new ConfigurationService(new KeywordMatchService(), new _1RM.Service.Configuration());
        var settings = new SettingsPageViewModel(null!, null!);
        IoC.GetByType = (type, _) => type == typeof(SettingsPageViewModel) ? settings
            : type == typeof(ConfigurationService) ? configuration
            : type == typeof(ILanguageService) ? new ResourceLanguageService() : null;
        try
        {
            foreach (var path in new[] {
                "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
                "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
                "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
                "RemoteX;component/Resources/Converter/Converter.xaml",
                "RemoteX;component/Resources/Icons/SVG.xaml",
                "RemoteX;component/Resources/Languages/zh-cn.xaml" })
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + path) });
            Application.Current.Resources.MergedDictionaries.Add(resources);
            action();
        }
        finally
        {
            Application.Current.Resources.MergedDictionaries.Remove(resources);
            IoC.GetByType = previous;
        }
    });

    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static T Find<T>(FrameworkElement view, string name) where T : FrameworkElement => (T)view.FindName(name);
    private static void BeginSimulatedAttempt(RustTerminalHost host, int attempt)
    {
        Field(typeof(RustTerminalHost), "_connectRequested").SetValue(host, true);
        Field(typeof(RustTerminalHost), "_connectionAttempt").SetValue(host, attempt);
    }
    private static void AssertDisposed(SshRustBridge bridge) => Assert.IsTrue((bool)Field(typeof(SshRustBridge), "_disposed").GetValue(bridge)!);

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FailedTerminalCreationStaysInTabReleasesConnectionAndAllowsRetry(bool missingDll) => Sta(() =>
    {
        var attempts = 0;
        var host = new RustTerminalHost(new SSH(), () =>
        {
            attempts++;
            throw missingDll ? new XamlParseException("constructor", new DllNotFoundException()) : new InvalidOperationException();
        });
        var closed = 0;
        host.OnClosed = _ => closed++;
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                BeginSimulatedAttempt(host, attempt);
                using var bridge = new SshRustBridge(); // No Connect call or real credentials.
                host.CompleteConnectionAttempt(attempt, bridge, Encoding.UTF8, null);
                AssertDisposed(bridge);
                Assert.AreEqual(ProtocolHostStatus.Disconnected, host.Status);
                Assert.IsFalse(host.HasConnected);
                Assert.IsFalse((bool)Field(typeof(RustTerminalHost), "_connectRequested").GetValue(host)!);
                Assert.IsNull(Field(typeof(RustTerminalHost), "_bridge").GetValue(host));
                Assert.IsNull(Field(typeof(RustTerminalHost), "_connection").GetValue(host));
                Assert.AreEqual(0, Find<Grid>(host, "TerminalHost").Children.Count);
                Assert.AreEqual(Visibility.Collapsed, Find<Control>(host, "GridLoading").Visibility);
                Assert.AreEqual(Visibility.Visible, Find<Grid>(host, "GridMessageBox").Visibility);
                Assert.AreEqual(Visibility.Visible, Find<Button>(host, "BtnReconn").Visibility);
                Assert.AreEqual(IoC.Translate(missingDll ? TerminalInitialization.MissingDependency : TerminalInitialization.Failed),
                    Find<TextBlock>(host, "TbMessage").Text);
                Assert.AreEqual(0, closed, "An initialization failure must not automatically close the tab.");
            }
            Assert.AreEqual(2, attempts);
            Find<Button>(host, "BtnCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, closed);
        }
        finally { host.Close(); }
    });

    [TestMethod]
    public void LateConnectionCompletionAfterCloseOrReconnectIsDiscarded() => Sta(() =>
    {
        var host = new RustTerminalHost(new SSH(), () => throw new AssertFailedException("Stale result created a terminal"));
        try
        {
            BeginSimulatedAttempt(host, 1);
            host.Close();
            using var first = new SshRustBridge();
            host.CompleteConnectionAttempt(1, first, Encoding.UTF8, null);
            AssertDisposed(first);
            BeginSimulatedAttempt(host, 3);
            using var second = new SshRustBridge();
            host.CompleteConnectionAttempt(1, second, Encoding.UTF8, null);
            AssertDisposed(second);
            Assert.AreEqual(Visibility.Collapsed, Find<Grid>(host, "GridMessageBox").Visibility);
        }
        finally { host.Close(); }
    });

    [TestMethod]
    public void ConnectLoaderFailureUsesRestartAdviceButAuthenticationErrorKeepsItsMessage() => Sta(() =>
    {
        var host = new RustTerminalHost(new SSH(), () => throw new AssertFailedException("Failed connect created a terminal"));
        try
        {
            BeginSimulatedAttempt(host, 1);
            using var missing = new SshRustBridge();
            host.CompleteConnectionAttempt(1, missing, Encoding.UTF8, null, "missing native library", new DllNotFoundException());
            AssertDisposed(missing);
            Assert.AreEqual(IoC.Translate(TerminalInitialization.MissingDependency), Find<TextBlock>(host, "TbMessage").Text);
            BeginSimulatedAttempt(host, 2);
            using var denied = new SshRustBridge();
            host.CompleteConnectionAttempt(2, denied, Encoding.UTF8, null, "Authentication failed");
            AssertDisposed(denied);
            Assert.AreEqual("Authentication failed", Find<TextBlock>(host, "TbMessage").Text);
        }
        finally { host.Close(); }
    });

    [TestMethod]
    public void RealTerminalCanInitializeAndCloseWithoutConnectingToAServer() => Sta(() =>
    {
        var host = new RustTerminalHost(new SSH());
        using var bridge = new SshRustBridge();
        try
        {
            BeginSimulatedAttempt(host, 1);
            host.CompleteConnectionAttempt(1, bridge, Encoding.UTF8, null);
            Assert.AreEqual(ProtocolHostStatus.Connected, host.Status);
            Assert.AreEqual(1, Find<Grid>(host, "TerminalHost").Children.Count);
            Assert.AreEqual(Visibility.Collapsed, Find<Grid>(host, "GridMessageBox").Visibility);
            host.Close();
            AssertDisposed(bridge);
            Assert.AreEqual(ProtocolHostStatus.Disconnected, host.Status);
        }
        finally { host.Close(); }
    });

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FeedbackDialogOnlyOffersCurrentGithubAndCanClose(bool dark) => Sta(() =>
    {
        // This constructor only creates the UI: it does not read logs or send telemetry.
        var window = new ErrorReportWindow();
        try
        {
            FluentAppearanceService.ApplyPalette(window.Resources, dark, false);
            FluentAppearanceService.ApplyAliases(window.Resources);
            FluentPage.SetActive(window, true);
            var button = Find<Button>(window, "GitHubFeedbackButton");
            Assert.AreEqual("https://github.com/aiclu/RemoteX/issues", button.ToolTip);
            Assert.AreEqual("github.com/aiclu/RemoteX", Find<TextBlock>(window, "GitHubFeedbackAddress").Text);
            Assert.IsNull(typeof(ErrorReportWindow).GetMethod("ButtonSendByEmail_OnClick", BindingFlags.Instance | BindingFlags.NonPublic));
            var root = (Grid)window.Content;
            root.Measure(new Size(696, 496)); root.Arrange(new Rect(0, 0, 696, 496)); root.UpdateLayout();
            Assert.IsTrue(button.ActualWidth >= 220);
            var address = Find<TextBlock>(window, "GitHubFeedbackAddress");
            Assert.IsTrue(address.ActualWidth >= address.DesiredSize.Width - 1);
            var previewDirectory = Path.Combine(SessionHeaderFixture.FindRoot(), "artifacts/terminal-failure-tests/previews");
            Directory.CreateDirectory(previewDirectory);
            var bitmap = new RenderTargetBitmap(696, 496, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(previewDirectory, dark ? "feedback-dark.png" : "feedback-light.png"))) encoder.Save(stream);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            Find<Button>(window, "BtnClose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(closed);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void AllLanguagesIncludeDependencyAdviceAndNoObsoleteContactText() => Sta(() =>
    {
        foreach (var file in LanguagesResources.Files)
        {
            var dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/RemoteX;component/Resources/Languages/" + file) };
            var csv = File.ReadAllLines(Path.Combine(SessionHeaderFixture.FindRoot(), "Ui/Resources/Languages/glossary", Path.ChangeExtension(file, ".csv")));
            foreach (var key in new[] { TerminalInitialization.MissingDependency, TerminalInitialization.InvalidDependency,
                TerminalInitialization.Failed, "error_report_how_help_us_improve" })
            {
                var value = dictionary[key] as string;
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), file + ": " + key);
                Assert.AreEqual(key + ";" + value, csv.Single(line => line.StartsWith(key + ";", StringComparison.Ordinal)));
                Assert.IsFalse(value.Contains("QQ群", StringComparison.Ordinal) || value.Contains("168542318", StringComparison.Ordinal));
            }
            Assert.IsFalse(dictionary.Contains("error_report_send_by_email"));
        }
    });
}
