using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.View;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel
{
    [TestClass]
    public class FluentMenuTests
    {
        private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static XDocument ReadView(string path)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RemoteX.sln"))) directory = directory.Parent;
            return XDocument.Load(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "Ui/View", path));
        }
        private static void Sta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) throw failure;
        }
        [TestMethod]
        public void ActualMenuStyleCollapsesWithoutPlaceholderAndRestoresClassic() => Sta(() =>
        {
            var source = ReadView("MainWindowView.xaml");
            var element = source.Descendants(Presentation + "Grid").Single(e => (string?)e.Attribute(Xaml + "Name") == "MainMenuChrome");
            var styleXml = new XElement(element.Element(Presentation + "Grid.Style")!.Element(Presentation + "Style")!);
            var grid = new Grid { Width = 45, Height = 35, Style = (Style)XamlReader.Parse(styleXml.ToString()) };
            foreach (var enabled in new[] { false, true, false })
            {
                grid.DataContext = new { IsFluentPreview = enabled };
                grid.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                grid.Measure(new Size(100, 100));
                Assert.AreEqual(enabled ? Visibility.Collapsed : Visibility.Visible, grid.Visibility);
                Assert.AreEqual(enabled ? 0.0 : 45.0, grid.DesiredSize.Width);
            }
        });
        [TestMethod]
        public void ResetKeepsPopupBindingAndClearsToggleWithoutExecutingCommands() => Sta(() =>
        {
            var popup = new Popup();
            popup.SetBinding(Popup.IsOpenProperty, new Binding("Value") { Source = new { Value = false }, Mode = BindingMode.OneWay });
            var toggle = new CheckBox { IsChecked = true };
            MainWindowView.ResetClassicMenu(popup, toggle);
            Assert.IsFalse(popup.IsOpen); Assert.AreEqual(false, toggle.IsChecked);
            Assert.IsNotNull(BindingOperations.GetBindingExpression(popup, Popup.IsOpenProperty));
        });
        [TestMethod]
        public void FluentMenuReusesExitAndIdSortCommands()
        {
            var items = ReadView("FluentWorkspace.xaml").Descendants(Presentation + "MenuItem").ToArray();
            Assert.AreEqual(1, items.Count(e => (string?)e.Attribute("Command") == "{Binding CmdExit}"));
            var sort = items.Single(e => (string?)e.Attribute("CommandParameter") == "{x:Static locality:EnumServerOrderBy.IdAsc}");
            Assert.AreEqual("{Binding CmdReOrder}", (string?)sort.Attribute("Command"));
        }
        [TestMethod]
        public void WorkspaceHasNoHeadingAndAllMenusUseLocalStyle()
        {
            var source = ReadView("FluentWorkspace.xaml");
            Assert.IsFalse(source.Descendants(Presentation + "TextBlock").Any(e =>
                (string?)e.Attribute("Text") == "{DynamicResource FluentConnections}"));
            Assert.IsTrue(source.Descendants(Presentation + "ContextMenu").All(e =>
                (string?)e.Attribute("Style") == "{StaticResource FluentWorkspaceMenu}"));
        }

        [TestMethod]
        public void FluentMenuSelectionTracksActualViewAndSortDirection() => Sta(() =>
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            var palette = new ResourceDictionary
            {
                Source = new Uri("/RemoteX;component/Resources/Theme/FluentPreview.xaml", UriKind.Relative)
            };
            var source = ReadView("FluentWorkspace.xaml");
            foreach (var command in new[] { "{Binding CmdToggleListView}", "{Binding CmdReOrder}" })
            {
                var element = source.Descendants(Presentation + "MenuItem").First(e =>
                    (string?)e.Attribute("Command") == command &&
                    (command.Contains("Toggle") || ((string?)e.Attribute("CommandParameter"))?.Contains("NameAsc") == true));
                var styleElement = new XElement(element.Element(Presentation + "MenuItem.Style")!.Element(Presentation + "Style")!);
                // Resolve the real explicit base style in an isolated resource dictionary.
                var dictionary = new XElement(Presentation + "ResourceDictionary", styleElement);
                styleElement.SetAttributeValue(Xaml + "Key", "SelectionStyle");
                dictionary.AddFirst(new XElement(Presentation + "ResourceDictionary.MergedDictionaries",
                    new XElement(Presentation + "ResourceDictionary", new XAttribute("Source",
                        "/RemoteX;component/Resources/Theme/FluentPreview.xaml"))));
                var resources = (ResourceDictionary)ParseElement(dictionary, source);
                var item = new MenuItem { Header = "Example", Resources = resources, Style = (Style)resources["SelectionStyle"] };
                foreach (var descending in new[] { false, true })
                {
                    item.DataContext = new {
                        CurrentView = descending ? EnumServerViewStatus.Card : EnumServerViewStatus.List,
                        ServerOrderBy = descending ? _1RM.Service.Locality.EnumServerOrderBy.NameDesc : _1RM.Service.Locality.EnumServerOrderBy.NameAsc
                    };
                    item.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    item.Measure(new Size(240, 80)); item.Arrange(new Rect(0, 0, 240, 40)); item.ApplyTemplate();
                    Assert.AreEqual(command.Contains("ReOrder") || !descending, item.IsChecked);
                    Assert.AreEqual(command.Contains("ReOrder") ? (descending ? "↓" : "↑") : (descending ? null : "✓"), item.Tag);
                    Assert.IsNotNull(item.Template.FindName("Chrome", item));
                }
            }
        });

        private static object ParseElement(XElement element, XDocument document)
        {
            var copy = new XElement(element);
            foreach (var ns in document.Root!.Attributes().Where(a => a.IsNamespaceDeclaration)) copy.SetAttributeValue(ns.Name, ns.Value);
            var xml = System.Text.RegularExpressions.Regex.Replace(copy.ToString(), "(clr-namespace:_1RM[^\";]+)\"", "$1;assembly=RemoteX\"");
            return XamlReader.Parse(xml);
        }
        public sealed class SearchCaretFixture : System.ComponentModel.INotifyPropertyChanged
        {
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            private int _caret = 3;
            public int Caret { get => _caret; set { if (_caret == value) return; _caret = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Caret))); } }
        }
        [TestMethod]
        public void CompletionRequiresVisibleFocusedEditorAndBoundsItsSize()
        {
            Assert.IsTrue(_1RM.View.Editor.Forms.RdpCompletionPopup.CanOpen(true, true, 1));
            Assert.IsFalse(_1RM.View.Editor.Forms.RdpCompletionPopup.CanOpen(false, true, 1));
            Assert.IsFalse(_1RM.View.Editor.Forms.RdpCompletionPopup.CanOpen(true, false, 1));
            Assert.IsFalse(_1RM.View.Editor.Forms.RdpCompletionPopup.CanOpen(true, true, 0));
            Assert.AreEqual(new Size(420, 240), _1RM.View.Editor.Forms.RdpCompletionPopup.GetSize(1000, 800));
            var narrow = _1RM.View.Editor.Forms.RdpCompletionPopup.GetSize(300, 400);
            Assert.AreEqual(new Size(268, 160), narrow);
        }
        [TestMethod]
        public void HiddenSearchCannotResetSharedCaretOnTextSynchronization() => Sta(() =>
        {
            var model = new SearchCaretFixture();
            var hidden = new TextBox { Text = "abcdef", Visibility = Visibility.Collapsed };
            var property = Shawn.Utils.WpfResources.Theme.AttachProperty.TextBoxAttachProperty.CaretIndexProperty;
            hidden.SetBinding(property, new Binding("Caret") { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            hidden.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(3, hidden.CaretIndex);
            // WPF resets selection when the other search box's text is synchronized through the VM.
            hidden.Text = "abcdefg";
            hidden.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(3, model.Caret, "An inactive search box must not overwrite the active editor's caret.");
            model.Caret = 6;
            hidden.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(6, hidden.CaretIndex, "Programmatic positioning must still reach inactive controls.");
            Assert.IsNotNull(BindingOperations.GetBindingExpression(hidden, property));
        });
        [TestMethod]
        public void AllRegisteredLanguagesHaveUniqueKeysAndLoad() => Sta(() =>
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            foreach (var file in LanguagesResources.Files)
            {
                var document = ReadView("../Resources/Languages/" + file);
                var duplicates = document.Root!.Elements().Select(e => (string?)e.Attribute(Xaml + "Key"))
                    .Where(k => k != null).GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key);
                Assert.AreEqual("", string.Join(",", duplicates), file + " contains duplicate resource keys");
                var dictionary = new ResourceDictionary { Source = new Uri("/RemoteX;component/Resources/Languages/" + file, UriKind.Relative) };
                foreach (var key in dictionary.Keys) Assert.IsNotNull(dictionary[key], file + ": " + key);
                Assert.IsTrue(dictionary.Contains("language_name"), file);
                Assert.IsTrue(dictionary.Contains("FluentSearchConnections"), file);
            }
        });
        [DataTestMethod]
        [DataRow(13.0)]
        [DataRow(20.0)]
        public void SearchHintAndContentAreCenteredAndShortcutCollapses(double fontSize) => Sta(() =>
        {
            var source = ReadView("../Resources/Theme/FluentPreview.xaml");
            var resources = (ResourceDictionary)ParseElement(source.Root!, source);
            var search = new TextBox { Resources = resources, Style = (Style)resources["FluentSearch"], Tag = "搜索连接", FontSize = fontSize };
            search.Measure(new Size(400, 48)); search.Arrange(new Rect(0, 0, 400, 48)); search.ApplyTemplate(); search.UpdateLayout();
            var hint = (TextBlock)search.Template.FindName("Hint", search);
            var host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
            var shortcut = (TextBlock)search.Template.FindName("Shortcut", search);
            Assert.AreEqual(Visibility.Visible, hint.Visibility);
            Assert.AreEqual(VerticalAlignment.Center, host.VerticalAlignment);
            Assert.AreEqual(24.0, hint.TranslatePoint(new Point(0, hint.ActualHeight / 2), search).Y, 1.0);
            Assert.AreEqual(Visibility.Visible, shortcut.Visibility);
            FluentWorkspace.SetIsCompact(search, true);
            Assert.AreEqual(Visibility.Collapsed, shortcut.Visibility);
            search.Text = "研发 #RDP";
            Assert.AreEqual(Visibility.Collapsed, hint.Visibility);
            FluentWorkspace.SetIsCompact(search, false);
            Assert.AreEqual(Visibility.Visible, shortcut.Visibility);
            Assert.AreEqual("研发 #RDP", search.Text);
        });
        [TestMethod]
        public void ProtocolStyleUsesDynamicPaletteAndRestoresClassicTemplate() => Sta(() =>
        {
            var forms = ReadView("../Resources/Theme/FluentForms.xaml");
            var template = (ControlTemplate)ParseElement(forms.Root!.Elements().Single(e => (string?)e.Attribute(Xaml + "Key") == "FluentProtocolItemTemplate"), forms);
            var source = ReadView("Editor/ServerEditorPageView.xaml");
            var itemStyle = source.Descendants(Presentation + "ListBox.Resources").Single().Element(Presentation + "Style")!;
            var wrapper = new XElement(Presentation + "ResourceDictionary",
                new XElement(Presentation + "SolidColorBrush", new XAttribute(Xaml + "Key", "PrimaryTextBrush"), new XAttribute("Color", "Gray")),
                new XElement(Presentation + "SolidColorBrush", new XAttribute(Xaml + "Key", "AccentTextBrush"), new XAttribute("Color", "White")),
                new XElement(itemStyle));
            // Resolve the actual template through a resource placeholder replaced before creating the item.
            wrapper.AddFirst(new XElement(Presentation + "ControlTemplate", new XAttribute(Xaml + "Key", "FluentProtocolItemTemplate"), new XAttribute("TargetType", "ListBoxItem")));
            var resources = (ResourceDictionary)ParseElement(wrapper, source);
            var style = (Style)resources[typeof(ListBoxItem)];
            var fluentTrigger = (Trigger)style.Triggers[0];
            ((Setter)fluentTrigger.Setters.Cast<Setter>().Single(s => s.Property == Control.TemplateProperty)).Value = template;
            var item = new ListBoxItem { Resources = resources, Style = style, Content = "SSH" };
            item.Resources["FluentText"] = System.Windows.Media.Brushes.Black;
            var classic = item.Template;
            FluentPage.SetActive(item, true);
            Assert.AreSame(template, item.Template);
            Assert.AreSame(System.Windows.Media.Brushes.Black, item.Foreground);
            item.Resources["FluentText"] = System.Windows.Media.Brushes.White;
            Assert.AreSame(System.Windows.Media.Brushes.White, item.Foreground);
            item.Measure(new Size(200, 100)); item.Arrange(new Rect(0, 0, 200, 40)); item.ApplyTemplate();
            var mark = (Border)item.Template.FindName("SelectionMark", item);
            Assert.AreEqual(Visibility.Collapsed, mark.Visibility);
            item.IsSelected = true;
            Assert.AreEqual(Visibility.Visible, mark.Visibility);
            Assert.AreEqual(FontWeights.SemiBold, item.FontWeight);
            Assert.IsTrue(item.MinHeight >= 36);
            FluentPage.SetActive(item, false);
            Assert.AreSame(classic, item.Template);
        });
    }
}
