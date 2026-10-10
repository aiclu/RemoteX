using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using _1RM;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.View;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass, DoNotParallelize]
public class FluentAlternateConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousIoC = IoC.GetByType;
            var previousConnect = GlobalEventHelper.OnRequestServerConnect;
            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                TestInit.Init();
                _1RM.Utils.UnSafeStringEncipher.Init("RemoteX.UnitTests.NonProduction");
                // No SessionControlService is constructed and no real connection can be opened.
                GlobalEventHelper.OnRequestServerConnect = null;
                action();
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                IoC.GetByType = previousIoC;
                GlobalEventHelper.OnRequestServerConnect = previousConnect;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static RDP Server() => new()
    {
        DisplayName = "Demo connection", Address = "primary.example.invalid", UserName = "demo",
        AlternateCredentials = new ObservableCollection<Credential>
        {
            new() { Name = "Office", Address = "office.example.invalid", Password = "not-for-display" },
            new() { Name = "Home", Address = "home.example.invalid", PrivateKeyPath = "not-for-display.key" }
        }
    };

    private static FluentConnectionRow Row(ProtocolBaseViewModel vm, bool dark = false)
    {
        var row = new FluentConnectionRow { FontSize = 14 };
        row.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/RemoteX;component/Resources/Theme/FluentPreview.xaml", UriKind.Relative)
        });
        row.Resources["Connect"] = "连接";
        row.Resources["with alternative"] = "使用备选凭证";
        FluentAppearanceService.ApplyPalette(row.Resources, dark, false);
        FluentAppearanceService.ApplyAliases(row.Resources);
        row.DataContext = vm;
        Layout(row, 700);
        return row;
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Layout(FluentConnectionRow row, double width)
    {
        row.Measure(new Size(width, 800));
        row.Arrange(new Rect(0, 0, width, row.DesiredSize.Height));
        row.UpdateLayout(); Drain();
    }

    [TestMethod]
    public void ArrowTracksCountCollectionReplacementAndRecycledServer() => Sta(() =>
    {
        var server = Server();
        var vm = new ProtocolBaseViewModel(server);
        var row = Row(vm);
        var arrow = (Button)row.FindName("RowAlternatives");
        Assert.AreEqual(Visibility.Visible, arrow.Visibility);
        Assert.IsNotNull(arrow.FocusVisualStyle);
        Assert.IsTrue(arrow.Focusable);

        server.AlternateCredentials.Clear(); Drain();
        Assert.AreEqual(Visibility.Hidden, arrow.Visibility);
        Assert.IsNull(row.CreateAlternateConnectionsMenu());
        server.AlternateCredentials = new ObservableCollection<Credential> { new() { Name = "New alternate" } };
        Drain();
        Assert.AreEqual(Visibility.Visible, arrow.Visibility);
        var oldMenu = row.CreateAlternateConnectionsMenu()!;
        row.DataContext = new ProtocolBaseViewModel(new Serial()); Drain();
        Assert.AreEqual(Visibility.Hidden, arrow.Visibility);
        Assert.AreEqual(0, oldMenu.Items.Count);
        Assert.IsNull(oldMenu.PlacementTarget);
        Assert.IsNull(row.CreateAlternateConnectionsMenu());

        row.DataContext = vm;
        vm.Server = new RDP(); Drain();
        Assert.AreEqual(Visibility.Hidden, arrow.Visibility);
        vm.Server = server; Drain();
        Assert.AreEqual(Visibility.Visible, arrow.Visibility);
    });

    [TestMethod]
    public void DefaultAndAlternateActionsUseExistingConnectionEventOnce() => Sta(() =>
    {
        var server = Server();
        var row = Row(new ProtocolBaseViewModel(server));
        var calls = 0;
        string? chosen = null;
        GlobalEventHelper.OnRequestServerConnect = (in ProtocolBase actual, in string view, in string tab, in string runner, in string credential) =>
        {
            Assert.AreSame(server, actual);
            Assert.AreEqual(nameof(MainWindowView), view);
            Assert.AreEqual("", tab); Assert.AreEqual("", runner);
            calls++; chosen = credential;
        };
        var connect = (Button)row.FindName("RowConnect");
        connect.Command.Execute(null);
        Assert.AreEqual(1, calls); Assert.AreEqual("", chosen);

        var menu = row.CreateAlternateConnectionsMenu()!;
        Assert.AreEqual(2, menu.Items.Count);
        var labels = string.Join(" ", menu.Items.Cast<MenuItem>().Select(x => x.Header));
        StringAssert.Contains(labels, "Office"); StringAssert.Contains(labels, "Home");
        Assert.IsFalse(labels.Contains("not-for-display"));
        Assert.IsFalse(labels.Contains("example.invalid"));
        ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.AreEqual(2, calls); Assert.AreEqual("Home", chosen);
        Assert.IsFalse(menu.IsOpen); Assert.AreEqual(0, menu.Items.Count);
        Assert.AreEqual("primary.example.invalid", server.Address);
        Assert.AreEqual("demo", server.UserName);
    });

    [TestMethod]
    public void MenusRebuildAndStaleItemsCannotConnectAfterServerReplacement() => Sta(() =>
    {
        var server = Server();
        var vm = new ProtocolBaseViewModel(server);
        var row = Row(vm);
        var calls = 0;
        GlobalEventHelper.OnRequestServerConnect = (in ProtocolBase actual, in string view, in string tab, in string runner, in string credential) => calls++;
        var menu = row.CreateAlternateConnectionsMenu()!;
        var staleItem = (MenuItem)menu.Items[0];
        vm.Server = Server();
        staleItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.AreEqual(0, calls);

        vm.Server = server;
        server.AlternateCredentials[0].Name = "Renamed";
        server.AlternateCredentials.RemoveAt(1);
        menu = row.CreateAlternateConnectionsMenu()!;
        Assert.AreEqual(1, menu.Items.Count);
        StringAssert.Contains((string)((MenuItem)menu.Items[0]).Header, "Renamed");
        row.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.AreEqual(0, menu.Items.Count);
        Assert.IsNull(menu.PlacementTarget);
    });

    [TestMethod]
    public void MenuUsesLocalPaletteAndKeepsManyAlternatesScrollable() => Sta(() =>
    {
        foreach (var dark in new[] { false, true })
        {
            var server = Server();
            for (var i = 0; i < 60; i++) server.AlternateCredentials.Add(new Credential { Name = $"Alternate {i}" });
            var row = Row(new ProtocolBaseViewModel(server), dark);
            var menu = row.CreateAlternateConnectionsMenu()!;
            Assert.AreEqual(62, menu.Items.Count);
            foreach (var key in new[] { "FluentSurface", "FluentText", "FluentAccent", "FluentHover" })
                Assert.AreSame(row.FindResource(key), menu.Resources[key]);
            Assert.IsTrue(menu.MaxHeight <= 400);
            Assert.IsTrue(menu.MaxWidth <= 400);
            menu.ApplyTemplate();
            Assert.IsNotNull(menu.Template);
            row.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    });

    [TestMethod]
    public void SplitActionsStayWithinNarrowRowsAndKeepNamesAligned() => Sta(() =>
    {
        var withAlternates = Row(new ProtocolBaseViewModel(Server()));
        var withoutAlternates = Row(new ProtocolBaseViewModel(new RDP { DisplayName = "Default" }));
        foreach (var fontSize in new[] { 14.0, 24.0 })
        foreach (var width in new[] { 700.0, 186.0 })
        foreach (var row in new[] { withAlternates, withoutAlternates })
        {
            row.FontSize = fontSize;
            row.Resources["Connect"] = "Connect to server";
            Layout(row, width);
            var action = (Grid)row.FindName("RowConnectionActions");
            var arrow = (Button)row.FindName("RowAlternatives");
            var button = (Button)row.FindName("RowConnect");
            Assert.IsTrue(action.TranslatePoint(new Point(action.ActualWidth, 0), row).X <= width + 0.01);
            Assert.IsTrue(button.ActualWidth >= 40);
            Assert.AreEqual(24.0, arrow.ActualWidth, 0.01);
            Assert.AreEqual(withAlternates.FindName("RowName") is FrameworkElement name
                ? name.TranslatePoint(new Point(), withAlternates).X : -1,
                ((FrameworkElement)row.FindName("RowName")).TranslatePoint(new Point(), row).X, 0.01);
        }
    });

    [TestMethod]
    public void RenderRealRowAndMenuWithLightAndDarkPalettes() => Sta(() =>
    {
        foreach (var dark in new[] { false, true })
        {
            var row = Row(new ProtocolBaseViewModel(Server()), dark);
            row.Margin = new Thickness(8);
            row.Background = (Brush)row.FindResource("FluentSurface");
            var menu = row.CreateAlternateConnectionsMenu()!;
            // Isolated rendering of the actual template, without showing a desktop popup.
            menu.Visibility = Visibility.Visible;
            menu.Width = 360;
            var stage = new Grid { Background = (Brush)row.FindResource("FluentCanvas") };
            stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            stage.RowDefinitions.Add(new RowDefinition());
            stage.Children.Add(row);
            stage.Measure(new Size(720, 220)); stage.Arrange(new Rect(0, 0, 720, 220)); stage.UpdateLayout(); Drain();
            // ContextMenu cannot have a visual parent: render it separately at its popup position.
            menu.Measure(new Size(360, 150));
            menu.Arrange(new Rect(new Point(), menu.DesiredSize)); menu.UpdateLayout(); Drain();
            Assert.IsTrue(menu.ActualHeight > 32, "The real menu must render, not an empty placeholder.");
            var popupVisual = new DrawingVisual();
            using (var drawing = popupVisual.RenderOpen())
                drawing.DrawRectangle(new VisualBrush(menu), null, new Rect(352, row.ActualHeight + 16, menu.ActualWidth, menu.ActualHeight));
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            {
                var bitmap = new RenderTargetBitmap((int)(720 * scale), (int)(220 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(stage);
                bitmap.Render(popupVisual);
                var path = Path.Combine(TestContext.TestResultsDirectory!, $"AlternateConnections-{(dark ? "Dark" : "Light")}-{scale * 100:0}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var file = File.Create(path))
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                }
                TestContext.AddResultFile(path);
            }
            row.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    });
}
