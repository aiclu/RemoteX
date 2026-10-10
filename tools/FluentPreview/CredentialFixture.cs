using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using _1RM.View;

namespace RemoteX.UiTesting;

// Actual credential form and password control; only the model/commands are synthetic.
// No IoC, data sources, credentials, file picker or connection commands are activated.
public sealed class CredentialFixture : IDisposable
{
    public sealed class Command(Action action) : ICommand
    {
        public int Calls { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Calls++; action(); }
    }
    public sealed class Fields : IDataErrorInfo
    {
        public object? DataSource { get; set; } = new object();
        public string UserName { get; set; } = "demo-user";
        public string Password { get; set; } = "synthetic-password";
        public string PrivateKey { get; set; } = "C:\\synthetic\\demo-key.ppk";
        public bool AskPasswordWhenConnect { get; set; }
        public string Error => "";
        public string this[string name] => name == nameof(UserName) && string.IsNullOrEmpty(UserName) ? "模拟必填校验" : "";
    }
    public sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public Fields New { get; } = new();
        public string[] CredentialNames { get; } = { "研发环境（模拟凭据）", "只用于界面验证" };
        public object[] Credentials { get; }
        private object? _selectedCredential;
        public object? SelectedCredential
        {
            get => _selectedCredential;
            set { _selectedCredential = value; Refresh(); }
        }
        public string SelectedCredentialName => SelectedCredential?.GetType().GetProperty("Name")?.GetValue(SelectedCredential) as string ?? "";
        public string StrBtnUseCredentialsVault { get; set; } = "凭据库";
        public bool ShowPrivateKeyInput { get; set; }
        private bool _isUsePrivateKey;
        public bool IsUsePrivateKey { get => _isUsePrivateKey; set { _isUsePrivateKey = value; Refresh(); } }
        public Command CmdUseManuallyCredential { get; }
        public Command CmdUseCredentialsVault { get; }
        public Command CmdAdd { get; } = new(() => { });
        public Model()
        {
            Credentials = CredentialNames.Select(name => (object)new { Name = name }).ToArray();
            CmdUseManuallyCredential = new(() => SelectedCredential = null);
            CmdUseCredentialsVault = new(() => SelectedCredential = Credentials[0]);
        }
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    public UserControl View { get; }
    public Model Data { get; } = new();
    public Border Panel => Find<Border>("CredentialPanel");
    public Button Manual => Find<Button>("ManualCredentialMode");
    public Button Vault => Find<Button>("VaultCredentialMode");
    private readonly ResourceDictionary _palette = new();
    private double _width = 800;
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    public CredentialFixture()
    {
        var rootPath = SessionHeaderFixture.FindRoot();
        var root = XDocument.Load(Path.Combine(rootPath, "Ui/View/Editor/Forms/Utils/CredentialView.xaml")).Root!;
        var dictionary = root.Element(P + "UserControl.Resources")!.Element(P + "ResourceDictionary")!;
        var merged = dictionary.Element(P + "ResourceDictionary.MergedDictionaries")!;
        merged.ReplaceNodes(new[] {
            "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
            "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml",
            "RemoteX;component/Resources/Converter/Converter.xaml",
            "RemoteX;component/Resources/Icons/SVG.xaml",
            "RemoteX;component/Resources/Languages/zh-cn.xaml"
        }.Select(path => new XElement(P + "ResourceDictionary", new XAttribute("Source", "pack://application:,,,/" + path))));
        var app = XDocument.Load(Path.Combine(rootPath, "Ui/App.xaml")).Root!;
        var appDictionary = app.Descendants(P + "ResourceDictionary").First(e => !e.HasAttributes);
        var form = XDocument.Load(Path.Combine(rootPath, "Ui/Resources/Theme/FluentForms.xaml")).Root!;
        var resources = new List<XElement>();
        foreach (var (source, elements) in new[] { (app, appDictionary.Elements()), (form, form.Elements()) })
        foreach (var element in elements)
        {
            var copy = new XElement(element);
            foreach (var ns in source.Attributes().Where(a => a.IsNamespaceDeclaration)) copy.SetAttributeValue(ns.Name, ns.Value);
            resources.Add(copy);
        }
        merged.AddAfterSelf(resources);
        foreach (var attr in root.DescendantsAndSelf().Attributes().Where(a => a.Name == X + "Class" || a.Name.NamespaceName.Contains("expression/blend") || a.Name.LocalName == "Click").ToArray()) attr.Remove();
        var xaml = System.Text.RegularExpressions.Regex.Replace(root.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
        View = (UserControl)XamlReader.Parse(xaml);
        View.DataContext = Data;
        View.Resources.MergedDictionaries.Add(_palette);
    }
    public T Find<T>(string name) where T : FrameworkElement => (T)View.FindName(name);
    public void Prepare(double width = 800, double font = 13, bool dark = false, bool fluent = true, bool highContrast = false)
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
        View.Resources["GlobalFontSize"] = View.Resources["GlobalFontSizeBody"] = font;
        View.Resources["GlobalFontSizeTitle"] = font + 5;
        View.Resources["GlobalFontFamily"] = new FontFamily("Microsoft YaHei");
        View.FontFamily = (FontFamily)View.Resources["GlobalFontFamily"]; View.FontSize = font;
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
    }
}
