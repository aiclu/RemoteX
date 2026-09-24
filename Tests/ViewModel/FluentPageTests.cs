using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM;
using _1RM.Service;
using _1RM.View;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel
{
    [TestClass]
    public class FluentPageTests
    {
        private static readonly Type AppearanceType = typeof(FluentPage).Assembly.GetType("_1RM.Service.FluentAppearanceService")!;
        private static void Sta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); thread.Join();
            if (failure != null) throw failure;
        }
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PaletteUsesDistinctSolidSurfaces(bool dark) => Sta(() =>
        {
            var resources = new ResourceDictionary();
            AppearanceType.GetMethod("ApplyPalette", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { resources, dark, false });
            Assert.AreNotEqual(((SolidColorBrush)resources["FluentText"]).Color, ((SolidColorBrush)resources["FluentSurface"]).Color);
            Assert.AreNotEqual(((SolidColorBrush)resources["FluentCanvas"]).Color, ((SolidColorBrush)resources["FluentSurface"]).Color);
            AppearanceType.GetMethod("ApplyAliases", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { resources });
            Assert.AreSame(resources["FluentSurface"], resources["BackgroundBrush"]);
            Assert.AreEqual(((SolidColorBrush)resources["FluentText"]).Color, resources["BackgroundTextColor"]);
        });
        [TestMethod]
        public void HighContrastUsesSystemColors() => Sta(() =>
        {
            var resources = new ResourceDictionary();
            AppearanceType.GetMethod("ApplyPalette", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { resources, true, true });
            Assert.AreSame(SystemColors.WindowBrush, resources["FluentSurface"]);
            Assert.AreSame(SystemColors.HighlightBrush, resources["FluentAccent"]);
        });
        [TestMethod]
        public void NarrowRowRestoresOriginalColumnsAndPositions() => Sta(() =>
        {
            var grid = new Grid();
            var labelColumn = new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" };
            var inputColumn = new ColumnDefinition { Width = new GridLength(300), MinWidth = 200 };
            grid.ColumnDefinitions.Add(labelColumn); grid.ColumnDefinitions.Add(inputColumn);
            var label = new TextBlock { Text = "Address" };
            var input = new TextBox { Text = "example.invalid" }; Grid.SetColumn(input, 1);
            grid.Children.Add(label); grid.Children.Add(input);
            FluentFieldLayout.SetEnabled(grid, true);
            grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.AreEqual(0, grid.ColumnDefinitions.Count);
            Assert.AreEqual(1, Grid.GetRow(input));
            Assert.AreEqual("example.invalid", input.Text);
            FluentFieldLayout.SetEnabled(grid, false);
            Assert.AreSame(inputColumn, grid.ColumnDefinitions[1]);
            Assert.AreEqual(1, Grid.GetColumn(input));
            Assert.AreEqual(0, Grid.GetRow(input));
            Assert.AreEqual("Label", labelColumn.SharedSizeGroup);
        });
        [TestMethod]
        public void NarrowStackRestoresClassicOrientation() => Sta(() =>
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            FluentFieldLayout.SetEnabled(stack, true);
            stack.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.AreEqual(Orientation.Vertical, stack.Orientation);
            FluentFieldLayout.SetEnabled(stack, false);
            Assert.AreEqual(Orientation.Horizontal, stack.Orientation);
        });
        private static ResourceDictionary ServerViewResources() => new ResourceDictionary {
            Source = new Uri("/RemoteX;component/Resources/Theme/FluentServerViews.xaml", UriKind.Relative)
        };
        [TestMethod]
        public void EarlyErrorUiWorksWithoutAppearanceService() => Sta(() =>
        {
            var previous = IoC.GetByType;
            IoC.GetByType = (type, key) => null;
            try
            {
                var surface = new Border();
                FluentPage.SetScope(surface, true);
                surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.IsFalse(FluentPage.GetActive(surface));
                FluentPage.SetScope(surface, false);
            }
            finally { IoC.GetByType = previous; }
        });
        [TestMethod]
        public void CardSizingRestoresClassicDimensions() => Sta(() =>
        {
            var resources = ServerViewResources();
            var card = new Grid { Style = (Style)resources["FluentCardBody"] };
            Assert.AreEqual(144.0, card.Width); Assert.AreEqual(144.0, card.Height);
            FluentPage.SetActive(card, true);
            Assert.AreEqual(184.0, card.Width); Assert.IsTrue(double.IsNaN(card.Height)); Assert.IsNull(card.Clip);
            FluentPage.SetActive(card, false);
            Assert.AreEqual(144.0, card.Width); Assert.AreEqual(144.0, card.Height); Assert.IsNotNull(card.Clip);
        });
        [TestMethod]
        public void CardCellPreservesClassicSpacing() => Sta(() =>
        {
            var cell = new Grid { Style = (Style)ServerViewResources()["FluentCardCell"] };
            FluentPage.SetActive(cell, true);
            Assert.AreEqual(200.0, cell.Width); Assert.IsTrue(double.IsNaN(cell.Height));
            FluentPage.SetActive(cell, false);
            Assert.AreEqual(165.0, cell.Width); Assert.AreEqual(164.0, cell.Height); Assert.AreEqual(new Thickness(), cell.Margin);
        });
        [TestMethod]
        public void TreeTemplatePreservesExpansionAndSelectionIndicators() => Sta(() =>
        {
            var item = new TreeViewItem { Header = "Synthetic", Template = (ControlTemplate)ServerViewResources()["FluentTreeItemTemplate"] };
            item.Items.Add(new TreeViewItem { Header = "Child" }); item.ApplyTemplate();
            var host = (ItemsPresenter)item.Template.FindName("ItemsHost", item);
            var mark = (Border)item.Template.FindName("SelectedMark", item);
            var expander = (System.Windows.Controls.Primitives.ToggleButton)item.Template.FindName("Expander", item);
            Assert.AreEqual(Visibility.Collapsed, host.Visibility);
            item.IsExpanded = true; item.IsSelected = true;
            Assert.AreEqual(Visibility.Visible, host.Visibility); Assert.AreEqual(Visibility.Visible, mark.Visibility);
            Assert.AreEqual(true, expander.IsChecked);
            item.IsSelected = false; Assert.AreEqual(Visibility.Collapsed, mark.Visibility);
        });
        [TestMethod]
        public void MultirowDialogRestoresRowsSpansAndBindings() => Sta(() =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var row = new RowDefinition { Height = GridLength.Auto };
            grid.RowDefinitions.Add(row); grid.RowDefinitions.Add(new RowDefinition());
            var input = new TextBox(); Grid.SetRow(input, 1); Grid.SetColumn(input, 1); Grid.SetColumnSpan(input, 2);
            input.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("Value") { Source = new { Value = "synthetic" }, Mode = System.Windows.Data.BindingMode.OneWay });
            grid.Children.Add(new TextBlock()); grid.Children.Add(input);
            FluentFieldLayout.SetFlattenRows(grid, true); FluentFieldLayout.SetEnabled(grid, true);
            grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.AreEqual(0, grid.ColumnDefinitions.Count);
            Assert.AreEqual("synthetic", input.Text);
            FluentFieldLayout.SetEnabled(grid, false);
            Assert.AreSame(row, grid.RowDefinitions[0]);
            Assert.AreEqual(1, Grid.GetRow(input)); Assert.AreEqual(1, Grid.GetColumn(input)); Assert.AreEqual(2, Grid.GetColumnSpan(input));
            Assert.IsNotNull(input.GetBindingExpression(TextBox.TextProperty));
        });
        [TestMethod]
        public void ScopeSynchronizesAndUnsubscribesWithoutChangingClassicResources() => Sta(() =>
        {
            var app = new Application();
#pragma warning disable SYSLIB0050
            var cfg = (_1RM.Service.Configuration)FormatterServices.GetUninitializedObject(typeof(_1RM.Service.Configuration));
            cfg.Theme = new ThemeConfig(); cfg.Theme.Fluent.Enabled = true;
            var config = (ConfigurationService)FormatterServices.GetUninitializedObject(typeof(ConfigurationService));
#pragma warning restore SYSLIB0050
            typeof(ConfigurationService).GetField("_cfg", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(config, cfg);
            var service = Activator.CreateInstance(AppearanceType, config)!;
            var previous = IoC.GetByType;
            IoC.GetByType = (type, key) => type == AppearanceType ? service : null;
            try
            {
                var classic = new ResourceDictionary { ["BackgroundBrush"] = Brushes.Purple };
                var first = new Border(); first.Resources.MergedDictionaries.Add(classic);
                var second = new Border();
                FluentPage.SetScope(first, true); FluentPage.SetScope(second, true);
                first.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                second.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                cfg.Theme.Fluent.Theme = "Dark";
                AppearanceType.GetMethod("NotifyChanged")!.Invoke(service, null);
                Assert.AreEqual(((SolidColorBrush)first.FindResource("BackgroundBrush")).Color,
                    ((SolidColorBrush)second.FindResource("BackgroundBrush")).Color);
                cfg.Theme.Fluent.Enabled = false;
                AppearanceType.GetMethod("NotifyChanged")!.Invoke(service, null);
                Assert.IsFalse(FluentPage.GetActive(first));
                Assert.AreSame(Brushes.Purple, first.FindResource("BackgroundBrush"));
                Assert.AreSame(Brushes.Purple, classic["BackgroundBrush"]);
                first.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                second.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.IsNull(AppearanceType.GetField("_changed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service));
                FluentPage.SetScope(first, false); FluentPage.SetScope(second, false);
                // Session windows expose only new palette tokens, never legacy aliases
                // or inherited form-style activation to the protocol content.
                cfg.Theme.Fluent.Enabled = true;
                var session = new Window();
                session.Resources["PrimaryMidBrush"] = Brushes.Purple;
                var remoteSurface = new Border(); session.Content = remoteSurface;
                _1RM.View.Host.FluentSessionAppearance.SetEnabled(session, true);
                session.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.IsTrue(_1RM.View.Host.FluentSessionAppearance.GetActive(session));
                Assert.IsFalse(FluentPage.GetActive(remoteSurface));
                Assert.AreSame(Brushes.Purple, remoteSurface.FindResource("PrimaryMidBrush"));
                Assert.IsNotNull(session.TryFindResource("FluentCanvas"));
                cfg.Theme.Fluent.Enabled = false;
                AppearanceType.GetMethod("NotifyChanged")!.Invoke(service, null);
                Assert.IsFalse(_1RM.View.Host.FluentSessionAppearance.GetActive(session));
                Assert.IsNull(session.TryFindResource("FluentCanvas"));
                session.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.IsNull(AppearanceType.GetField("_changed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service));
                _1RM.View.Host.FluentSessionAppearance.SetEnabled(session, false);
                session.Close();
            }
            finally { ((IDisposable)service).Dispose(); IoC.GetByType = previous; app.Shutdown(); }
        });
    }
}
