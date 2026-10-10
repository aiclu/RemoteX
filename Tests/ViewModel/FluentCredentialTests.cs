using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using _1RM;
using _1RM.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteX.UiTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class FluentCredentialTests
{
    private static void Sta(Action action)
    {
        WpfTestHost.Run(() => {
            var previous = IoC.GetByType;
            var resources = new ResourceDictionary();
            foreach (var path in new[] {
                "Shawn.Utils.WpfResources;component/Converter/Converter.xaml",
                "Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
                "Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml"
            }) resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + path) });
            Application.Current.Resources.MergedDictionaries.Add(resources);
            try { IoC.GetByType = (_, _) => null; action(); }
            finally { IoC.GetByType = previous; Application.Current.Resources.MergedDictionaries.Remove(resources); }
        });
    }
    private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;
    [TestMethod]
    public void CredentialPanelUsesAdaptiveNeutralBorderInAllFluentPalettes() => Sta(() =>
    {
        using var fixture = new CredentialFixture();
        foreach (var (dark, hc) in new[] { (false, false), (true, false), (false, true) })
        {
            fixture.Prepare(dark: dark, highContrast: hc);
            Assert.IsTrue(double.IsNaN(fixture.Panel.Width));
            Assert.AreEqual(new Thickness(1), fixture.Panel.BorderThickness);
            Assert.AreEqual(new CornerRadius(8), fixture.Panel.CornerRadius);
            Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentStroke")), ColorOf(fixture.Panel.BorderBrush));
            Assert.AreEqual(800, fixture.Panel.ActualWidth, 0.1);
            Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentText")), ColorOf(fixture.Manual.Foreground));
        }
    });
    [TestMethod]
    public void CredentialModeCommandsKeepOneShortSelectionIndicator() => Sta(() =>
    {
        using var fixture = new CredentialFixture(); fixture.Prepare();
        Assert.AreSame(fixture.Data.CmdUseManuallyCredential, fixture.Manual.Command);
        Assert.AreSame(fixture.Data.CmdUseCredentialsVault, fixture.Vault.Command);
        Assert.AreEqual(new Thickness(0, 0, 0, 2), fixture.Manual.BorderThickness);
        Assert.AreEqual(new Thickness(), fixture.Vault.BorderThickness);
        var mark = (Border)fixture.Manual.Template.FindName("SelectionMark", fixture.Manual);
        Assert.AreEqual(24, mark.ActualWidth, 0.1); Assert.AreEqual(2, mark.ActualHeight, 0.1);
        fixture.Vault.Command.Execute(null); fixture.Layout();
        Assert.AreEqual(new Thickness(), fixture.Manual.BorderThickness);
        Assert.AreEqual(new Thickness(0, 0, 0, 2), fixture.Vault.BorderThickness);
        Assert.AreEqual(Visibility.Visible, fixture.Find<Grid>("VaultCredentialRow").Visibility);
        Assert.IsFalse(fixture.Find<StackPanel>("ManualCredentialFields").IsEnabled);
        fixture.Manual.Command.Execute(null); fixture.Layout();
        Assert.IsTrue(fixture.Find<StackPanel>("ManualCredentialFields").IsEnabled);
        Assert.AreEqual(Visibility.Collapsed, fixture.Find<Grid>("VaultCredentialRow").Visibility);
        Assert.AreEqual(1, fixture.Data.CmdUseCredentialsVault.Calls);
        Assert.AreEqual(1, fixture.Data.CmdUseManuallyCredential.Calls);
    });
    [TestMethod]
    public void PasswordPrivateKeyAndNarrowFieldsStayInsidePanel() => Sta(() =>
    {
        using var fixture = new CredentialFixture(); fixture.Data.ShowPrivateKeyInput = true;
        foreach (var width in new[] { 300.0, 480, 800 })
        foreach (var font in new[] { 13.0, 20 })
        foreach (var key in new[] { false, true })
        {
            fixture.Data.IsUsePrivateKey = key; fixture.Prepare(width, font);
            Assert.AreEqual(key ? Visibility.Collapsed : Visibility.Visible, fixture.Find<Grid>("CredentialPasswordRow").Visibility);
            Assert.AreEqual(key ? Visibility.Visible : Visibility.Collapsed, fixture.Find<Grid>("CredentialPrivateKeyRow").Visibility);
            foreach (var name in new[] { "CredentialUserName", key ? "TbPrivateKey" : "CredentialPasswordInput", "AskForPassword", "CbUsePrivateKey" })
            {
                var field = fixture.Find<FrameworkElement>(name);
                var point = field.TranslatePoint(new Point(), fixture.Panel);
                Assert.IsTrue(point.X >= 1 && point.X + field.ActualWidth <= fixture.Panel.ActualWidth - 1,
                    $"{name} overflows at {width} DIP / font {font}: x={point.X} width={field.ActualWidth} panel={fixture.Panel.ActualWidth}");
            }
        }
    });
    [TestMethod]
    public void ClassicDimensionsAndTemplateRestoreAfterFluentMode() => Sta(() =>
    {
        using var fixture = new CredentialFixture(); fixture.Prepare(fluent: false);
        var classic = fixture.Manual.Template;
        Assert.AreEqual(700, fixture.Panel.Width, 0.1);
        Assert.AreEqual(new Thickness(2), fixture.Panel.BorderThickness);
        fixture.Prepare(300); fixture.Prepare(fluent: false);
        Assert.AreSame(classic, fixture.Manual.Template);
        Assert.AreEqual(700, fixture.Panel.Width, 0.1);
        Assert.AreEqual(new Thickness(20, 0, 20, 0), fixture.Panel.Margin);
        Assert.AreEqual(32, fixture.Find<PasswordInput>("CredentialPasswordInput").Height, 0.1);
    });
    [TestMethod]
    public void VaultSelectionAndUnavailableSourceKeepTheirBindingsInNarrowLayout() => Sta(() =>
    {
        using var fixture = new CredentialFixture();
        fixture.Data.StrBtnUseCredentialsVault = "Credentials vault with a deliberately long translated label";
        fixture.Data.SelectedCredential = fixture.Data.Credentials[0]; fixture.Prepare(300, 20);
        var selector = fixture.Find<ComboBox>("VaultCredentialSelector");
        selector.SelectedItem = fixture.Data.Credentials[1]; fixture.Layout();
        Assert.AreSame(selector.SelectedItem, fixture.Data.SelectedCredential);
        var scroller = CredentialFixture.Descendants<ScrollViewer>(fixture.View).First(x => x.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
        Assert.IsTrue(scroller.ScrollableWidth > 0, "Long mode names should scroll instead of widening the form.");
        var point = selector.TranslatePoint(new Point(), fixture.Panel);
        Assert.IsTrue(point.X + selector.ActualWidth < fixture.Panel.ActualWidth);
        var add = fixture.Find<Button>("AddVaultCredential");
        Assert.AreSame(fixture.Data.CmdAdd, add.Command);
        fixture.Data.New.DataSource = null; fixture.Data.Refresh(); fixture.Layout();
        Assert.IsFalse(selector.IsEnabled); Assert.IsFalse(add.IsEnabled);
        Assert.AreEqual(0, fixture.Data.CmdAdd.Calls, "Rendering must never insert credentials.");
    });
    [TestMethod]
    public void FieldBindingsValidationAndPasswordMaskRemainIntact() => Sta(() =>
    {
        using var fixture = new CredentialFixture(); fixture.Prepare();
        var user = fixture.Find<TextBox>("CredentialUserName");
        var password = fixture.Find<PasswordInput>("CredentialPasswordInput");
        Assert.AreEqual("New.Password", BindingOperations.GetBinding(password, PasswordInput.PasswordProperty)!.Path.Path);
        Assert.AreEqual("New.PrivateKey", BindingOperations.GetBinding(fixture.Find<TextBox>("TbPrivateKey"), TextBox.TextProperty)!.Path.Path);
        Assert.AreEqual("New.AskPasswordWhenConnect", BindingOperations.GetBinding(fixture.Find<CheckBox>("AskForPassword"), System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.Path.Path);
        Assert.AreEqual(Visibility.Collapsed, ((TextBox)password.FindName("PlainTextBox")).Visibility);
        Assert.AreEqual(fixture.Data.New.Password, ((PasswordBox)password.FindName("CipherTextBox")).Password);
        user.Text = ""; user.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); fixture.Layout();
        Assert.IsTrue(Validation.GetHasError(user), "The card must not suppress input validation.");
        user.Text = "demo-user"; user.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); fixture.Layout();
        Assert.IsFalse(Validation.GetHasError(user));
    });
    [TestMethod]
    public void ModeButtonsHaveIndependentHoverAndKeyboardFocusStates() => Sta(() =>
    {
        using var fixture = new CredentialFixture(); fixture.Prepare();
        void State(string name, bool value)
        {
            var key = (DependencyPropertyKey)typeof(UIElement).GetField(name + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            fixture.Vault.SetValue(key, value); fixture.Layout();
        }
        State("IsMouseOver", true);
        var chrome = (Border)fixture.Vault.Template.FindName("Chrome", fixture.Vault);
        Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentHover")), ColorOf(chrome.Background));
        State("IsKeyboardFocused", true);
        Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentAccent")), ColorOf(chrome.BorderBrush));
        Assert.AreEqual(new Thickness(), fixture.Vault.BorderThickness, "Keyboard focus must not become a selection.");
        fixture.Vault.IsEnabled = false;
        Assert.IsTrue(fixture.Vault.Opacity < 1);
    });
}
