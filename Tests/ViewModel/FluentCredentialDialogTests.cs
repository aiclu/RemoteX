using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using _1RM;
using _1RM.Controls;
using _1RM.View;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteX.UiTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class FluentCredentialDialogTests
{
    private static void Sta(Action action) => WpfTestHost.Run(() => {
        var previous = IoC.GetByType;
        try { IoC.GetByType = (_, _) => null; action(); }
        finally { IoC.GetByType = previous; }
    });
    private static Rect Bounds(FrameworkElement field, CredentialDialogFixture fixture) =>
        new(field.TranslatePoint(new Point(), fixture.View), field.RenderSize);
    private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;

    [TestMethod]
    public void LabelsAndInputsAlignAcrossWidthsAndFontSizes() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture();
        foreach (var width in new[] { 300.0, 560, 800 })
        foreach (var font in new[] { 13.0, 20 })
        {
            fixture.Prepare(width, font);
            var title = Bounds(fixture.Find<TextBlock>("DialogTitle"), fixture);
            var inputWidth = fixture.Find<TextBox>("TbName").ActualWidth;
            foreach (var (labelName, fieldName) in new[] {
                ("NameLabel", "TbName"), ("UserNameLabel", "TbUserName"), ("PasswordLabel", "TbPassword") })
            {
                var label = Bounds(fixture.Find<FrameworkElement>(labelName), fixture);
                var field = Bounds(fixture.Find<FrameworkElement>(fieldName), fixture);
                Assert.AreEqual(title.X, label.X, 1, labelName);
                Assert.AreEqual(label.X, field.X, 1, fieldName);
                Assert.AreEqual(inputWidth, field.Width, 1, fieldName);
                Assert.IsTrue(field.Top >= label.Bottom + 5, $"{fieldName}: label overlaps input");
                Assert.IsTrue(field.Height >= 36 && field.Right <= width - 1, fieldName);
            }
        }
    });

    [TestMethod]
    public void RequiredErrorsOccupyTheirOwnRowsAndClearWhenValid() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture();
        fixture.Data.Name = fixture.Data.UserName = fixture.Data.Password = "";
        fixture.Prepare();
        foreach (var (fieldName, errorsName) in new[] { ("TbName", "NameErrors"), ("TbUserName", "UserNameErrors"), ("TbPassword", "PasswordErrors") })
        {
            var field = fixture.Find<FrameworkElement>(fieldName);
            var errors = fixture.Find<ItemsControl>(errorsName);
            Assert.IsTrue(Validation.GetHasError(field), fieldName);
            Assert.AreEqual(1, errors.Items.Count, errorsName);
            Assert.AreEqual(Visibility.Visible, errors.Visibility);
            Assert.IsTrue(Bounds(errors, fixture).Top >= Bounds(field, fixture).Bottom + 3, errorsName);
            Assert.AreSame(fixture.View.FindResource("CredentialDialogErrorOutline"), Validation.GetErrorTemplate(field));
        }
        Assert.IsTrue(Bounds(fixture.Find<TextBlock>("UserNameLabel"), fixture).Top > Bounds(fixture.Find<ItemsControl>("NameErrors"), fixture).Bottom);
        Assert.IsFalse(fixture.Data.CanSave());
        var name = fixture.Find<TextBox>("TbName");
        name.SetCurrentValue(TextBox.TextProperty, "new-test-credential");
        fixture.Find<TextBox>("TbUserName").SetCurrentValue(TextBox.TextProperty, "demo");
        fixture.Find<PasswordInput>("TbPassword").SetCurrentValue(PasswordInput.PasswordProperty, "synthetic-password");
        fixture.Layout();
        Assert.AreEqual("new-test-credential", fixture.Data.Name);
        Assert.IsTrue(fixture.Data.CanSave());
        Assert.IsFalse(Validation.GetHasError(name));
        Assert.AreEqual(Visibility.Collapsed, fixture.Find<ItemsControl>("NameErrors").Visibility);
        name.SetCurrentValue(TextBox.TextProperty, "ALREADY-EXISTS"); fixture.Layout();
        Assert.IsTrue(Validation.GetHasError(name));
        Assert.IsFalse(fixture.Data.CmdSave.CanExecute(null));
    });

    [TestMethod]
    public void VaultHidesInheritanceHintsButAlternativeConnectionKeepsThem() => Sta(() =>
    {
        foreach (var host in new[] { false, true })
        {
            using var fixture = new CredentialDialogFixture(showHost: host); fixture.Prepare();
            Assert.AreEqual(host ? Visibility.Visible : Visibility.Collapsed, fixture.Find<Grid>("AddressRow").Visibility);
            foreach (var name in new[] { "TbUserName", "TbPassword", "TbPrivateKey" })
            {
                var field = fixture.Find<FrameworkElement>(name);
                if (host) Assert.IsFalse(string.IsNullOrEmpty(field.Tag as string), name);
                else Assert.IsNull(field.Tag, name);
            }
        }
    });

    [TestMethod]
    public void PasswordStaysMaskedAndPrivateKeyPickerDoesNotOverlayInput() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture(); fixture.Prepare();
        var password = fixture.Find<PasswordInput>("TbPassword");
        Assert.AreEqual(fixture.Data.Password, ((PasswordBox)password.FindName("CipherTextBox")).Password);
        Assert.AreNotEqual(Visibility.Visible, ((TextBox)password.FindName("PlainTextBox")).Visibility);
        fixture.Find<CheckBox>("PasswordUsePrivateKey").SetCurrentValue(CheckBox.IsCheckedProperty, true);
        fixture.Data.PrivateKeyPath = @"C:\synthetic\private-key.ppk"; fixture.Layout();
        Assert.IsTrue(fixture.Data.IsUsePrivateKey);
        Assert.AreEqual(Visibility.Collapsed, fixture.Find<Grid>("PasswordRow").Visibility);
        Assert.AreEqual(Visibility.Visible, fixture.Find<Grid>("PrivateKeyRow").Visibility);
        foreach (var width in new[] { 300.0, 560 })
        {
            fixture.Prepare(width, 20);
            var field = Bounds(fixture.Find<TextBox>("TbPrivateKey"), fixture);
            var picker = Bounds(fixture.Find<Button>("PrivateKeyPicker"), fixture);
            Assert.IsTrue(picker.Left >= field.Right + 7);
            Assert.IsTrue(picker.Right <= width - 1);
            Assert.AreEqual(field.Top, picker.Top, 1);
        }
        Assert.IsTrue(fixture.Data.CanSave());
    });

    [TestMethod]
    public void FooterRemainsVisibleWhileBodyScrollsInSmallWorkArea() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture(showHost: true);
        fixture.Prepare(300, 20, bodyMaxHeight: 260);
        var frame = fixture.Find<HeaderedContentControl>("CredentialDialogFrame");
        var scroll = CredentialDialogFixture.Descendants<ScrollViewer>(frame).First();
        var save = fixture.Find<Button>("SaveCredential");
        var cancel = fixture.Find<Button>("CancelCredential");
        Assert.IsTrue(scroll.ScrollableHeight > 0);
        Assert.IsTrue(Bounds(save, fixture).Top >= Bounds(scroll, fixture).Bottom);
        Assert.IsTrue(Bounds(cancel, fixture).Right <= fixture.View.ActualWidth - 1);
        var before = Bounds(save, fixture);
        scroll.ScrollToBottom(); fixture.Layout();
        Assert.IsTrue(scroll.VerticalOffset > 0);
        Assert.AreEqual(before, Bounds(save, fixture));
        Assert.AreSame(fixture.Data.CmdSave, save.Command);
        Assert.AreSame(fixture.Data.CmdCancel, cancel.Command);
    });

    [TestMethod]
    public void FluentPalettesUpdateShellFieldsAndErrorsWithoutChangingClassic() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture();
        fixture.Data.Name = "";
        foreach (var (dark, hc) in new[] { (false, false), (true, false), (false, true) })
        {
            fixture.Prepare(dark: dark, highContrast: hc);
            Assert.AreEqual(new CornerRadius(8), fixture.Shell.CornerRadius);
            Assert.AreEqual(new Thickness(1), fixture.Shell.BorderThickness);
            Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentStroke")), ColorOf(fixture.Shell.BorderBrush));
            Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentText")), ColorOf(fixture.Find<TextBox>("TbUserName").Foreground));
            var error = CredentialDialogFixture.Descendants<TextBlock>(fixture.Find<ItemsControl>("NameErrors")).Single();
            Assert.AreEqual(ColorOf((Brush)fixture.View.FindResource("FluentError")), ColorOf(error.Foreground));
        }
        fixture.Prepare(900, fluent: false);
        Assert.IsFalse(FluentPage.GetActive(fixture.View));
        Assert.AreEqual(3, fixture.Find<Grid>("NameRow").ColumnDefinitions.Count);
        Assert.AreEqual(2, fixture.Find<Grid>("NameRow").RowDefinitions.Count);
        Assert.AreEqual(100, fixture.Find<TextBlock>("NameLabel").Width);
        Assert.AreEqual(26, fixture.Find<TextBox>("TbName").Height);
        Assert.AreEqual(Visibility.Collapsed, fixture.Find<ItemsControl>("NameErrors").Visibility);
        Assert.AreNotSame(fixture.View.FindResource("CredentialDialogErrorOutline"), Validation.GetErrorTemplate(fixture.Find<TextBox>("TbName")));
        fixture.Prepare();
        Assert.AreEqual(0, fixture.Find<Grid>("NameRow").ColumnDefinitions.Count);
    });

    [TestMethod]
    public void FieldBindingsAndValidationRemainTheOriginalViewModelContract() => Sta(() =>
    {
        using var fixture = new CredentialDialogFixture(showHost: true); fixture.Prepare();
        foreach (var (name, property) in new[] { ("TbName", "Name"), ("TbAddress", "Address"), ("TbPort", "Port"), ("TbUserName", "UserName"), ("TbPrivateKey", "PrivateKeyPath") })
        {
            var binding = BindingOperations.GetBinding(fixture.Find<TextBox>(name), TextBox.TextProperty)!;
            Assert.AreEqual(property, binding.Path.Path);
            Assert.IsTrue(binding.ValidatesOnDataErrors && binding.ValidatesOnExceptions && binding.ValidatesOnNotifyDataErrors);
            Assert.AreEqual(UpdateSourceTrigger.PropertyChanged, binding.UpdateSourceTrigger);
        }
        var password = BindingOperations.GetBinding(fixture.Find<PasswordInput>("TbPassword"), PasswordInput.PasswordProperty)!;
        Assert.AreEqual("Password", password.Path.Path);
        Assert.IsTrue(password.ValidatesOnDataErrors);
        fixture.Find<TextBox>("TbPort").SetCurrentValue(TextBox.TextProperty, "99999"); fixture.Layout();
        Assert.IsTrue(Validation.GetHasError(fixture.Find<TextBox>("TbPort")));
        Assert.IsFalse(fixture.Data.CanSave());
    });
}
