using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using _1RM.View;
using _1RM.View.Editor.Forms.AlternativeCredential;

namespace RemoteX.UiTesting;

// Compiled production window, detached for off-screen layout. Never shown, connected to a
// vault, or saved. The real view model only holds synthetic values and validation rules.
public sealed class CredentialDialogFixture : IDisposable
{
    private readonly Window _window;
    private readonly ResourceDictionary _resources = new();
    private readonly ResourceDictionary _palette = new();
    private double _width = 560;
    public AdornerDecorator View { get; }
    public Border Shell { get; }
    public AlternativeCredentialEditViewModel Data { get; }

    public CredentialDialogFixture(bool showHost = false, bool showUser = true,
        bool showPassword = true, bool showKey = true)
    {
        _1RM.Utils.UnSafeStringEncipher.Init("RemoteX.UnitTests.NonProduction");
        foreach (var path in new[] {
            "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
            "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
            "RemoteX;component/Resources/Converter/Converter.xaml",
            "RemoteX;component/Resources/Icons/SVG.xaml",
            "RemoteX;component/Resources/Languages/zh-cn.xaml"
        }) _resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + path) });
        Application.Current.Resources.MergedDictionaries.Add(_resources);
        try
        {
            var document = XDocument.Load(Path.Combine(SessionHeaderFixture.FindRoot(), "Ui/App.xaml"));
            var dictionary = document.Descendants().First(e => e.Name.LocalName == "ResourceDictionary" && !e.HasAttributes);
            foreach (var ns in document.Root!.Attributes().Where(a => a.IsNamespaceDeclaration)) dictionary.SetAttributeValue(ns.Name, ns.Value);
            _resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(dictionary.ToString()));
            _window = (Window)Application.LoadComponent(new Uri(
                "/RemoteX;component/View/Editor/Forms/AlternativeCredential/AlternativeCredentialEditView.xaml", UriKind.Relative));
            Shell = (Border)_window.Content;
            _window.Content = null;
            NameScope.SetNameScope(Shell, NameScope.GetNameScope(_window));
            View = new AdornerDecorator { Child = Shell };
            View.Resources.MergedDictionaries.Add(_window.Resources);
            View.Resources.MergedDictionaries.Add(_palette);
            Data = new AlternativeCredentialEditViewModel(new List<string> { "already-exists" },
                showHost: showHost, showUsername: showUser, showPassword: showPassword, showPrivateKey: showKey,
                title: showHost ? "添加备选连接" : "添加凭据") {
                RequireUserName = !showHost, RequirePassword = !showHost, RequirePrivateKey = !showHost,
                Name = "研发环境", UserName = "demo-user", Password = "synthetic-ui-password"
            };
            View.DataContext = Data;
        }
        catch { Application.Current.Resources.MergedDictionaries.Remove(_resources); throw; }
    }

    public T Find<T>(string name) where T : FrameworkElement => (T)_window.FindName(name);
    public void Prepare(double width = 560, double font = 13, bool dark = false, bool fluent = true,
        bool highContrast = false, double bodyMaxHeight = 800)
    {
        _width = width;
        _palette.Clear();
        if (fluent)
        {
            var appearance = typeof(FluentPage).Assembly.GetType("_1RM.Service.FluentAppearanceService")!;
            appearance.GetMethod("ApplyPalette", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { _palette, dark, highContrast });
            appearance.GetMethod("ApplyAliases", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { _palette });
        }
        FluentPage.SetActive(View, fluent); FluentPage.SetNarrow(View, fluent && width < 720);
        View.Resources["FluentDialogBodyMaxHeight"] = bodyMaxHeight;
        View.Resources["GlobalFontSize"] = View.Resources["GlobalFontSizeBody"] = font;
        View.Resources["GlobalFontSizeTitle"] = font + 5;
        View.Resources["GlobalFontSizeSubtitle"] = font + 2;
        View.Resources["GlobalFontFamily"] = new FontFamily("Microsoft YaHei");
        Layout();
        foreach (var element in Descendants<FrameworkElement>(View).ToArray()) element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Layout();
    }
    public void Layout()
    {
        View.Measure(new Size(_width, double.PositiveInfinity));
        View.Arrange(new Rect(0, 0, _width, View.DesiredSize.Height)); View.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); View.UpdateLayout();
    }
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject => SessionHeaderFixture.Descendants<T>(root);
    public void Dispose()
    {
        foreach (var element in Descendants<FrameworkElement>(View).Reverse().ToArray()) element.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        _window.Close();
        Application.Current.Resources.MergedDictionaries.Remove(_resources);
    }
}
