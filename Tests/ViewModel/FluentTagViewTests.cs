using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using _1RM.Model;
using _1RM.View;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel;

[TestClass]
public class FluentTagViewTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static string[] Names(FluentTagView view) => view.View.Cast<Tag>().Select(t => t.Name).ToArray();
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    public sealed class FilterFixture : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _selectedTabName = "";
        public string SelectedTabName
        {
            get => _selectedTabName;
            set { _selectedTabName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTabName))); }
        }
    }
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            if (FindChild<T>(child) is T nested) return nested;
        }
        return null;
    }

    [TestMethod]
    public void SidebarHighlightFollowsFilterNotStaleListSelection() => Sta(() =>
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        var workspace = new FluentWorkspace();
        var tags = (ListBox)workspace.FindName("Tags");
        ((Panel)tags.Parent).Children.Remove(tags);
        tags.Resources.MergedDictionaries.Add(workspace.Resources);
        tags.Visibility = Visibility.Visible;
        var model = new FilterFixture();
        tags.DataContext = new { ActiveServerViewModel = model };
        var source = new ObservableCollection<Tag> { new("hp", false, 0), new("personal", false, 1) };
        using var view = new FluentTagView(source);
        tags.ItemsSource = view.View;
        tags.ApplyTemplate(); Drain();
        var stage = new Grid { Width = 176, Height = 300 };
        stage.Children.Add(tags);
        stage.Measure(new Size(176, 300)); stage.Arrange(new Rect(0, 0, 176, 300)); stage.UpdateLayout(); Drain();
        var oldContainer = (ListBoxItem)tags.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.IsNotNull(oldContainer, $"First tag container should be realized. items={tags.Items.Count}, visible={tags.Visibility}, size={tags.ActualWidth}x{tags.ActualHeight}, children={VisualTreeHelper.GetChildrenCount(tags)}, status={tags.ItemContainerGenerator.Status}");
        var hp = FindChild<Button>(oldContainer)!;
        var personal = FindChild<Button>((ListBoxItem)tags.ItemContainerGenerator.ContainerFromIndex(1))!;
        Assert.IsNotNull(hp); Assert.IsNotNull(personal);
        Assert.AreEqual(false, tags.IsSynchronizedWithCurrentItem);
        // Reproduce the independent, previously stale selection/current-item state.
        tags.SelectedItem = source[0]; view.View.MoveCurrentTo(source[0]);
        foreach (var selected in new[] { "hp", "personal", "", _1RM.View.ServerView.ServerPageViewModelBase.TAB_NONE_SELECTED, "hp" })
        {
            model.SelectedTabName = selected; Drain();
            foreach (var pair in new[] { (hp, "hp"), (personal, "personal") })
            {
                Assert.AreEqual(pair.Item2 == selected ? FontWeights.SemiBold : FontWeights.Normal, pair.Item1.FontWeight);
                var color = ((SolidColorBrush)pair.Item1.Background).Color;
                Assert.AreEqual(pair.Item2 == selected ? ((SolidColorBrush)tags.FindResource("FluentSelection")).Color : Colors.Transparent, color);
                Assert.IsNotNull(pair.Item1.FocusVisualStyle, "Keyboard focus feedback must remain available.");
            }
            Assert.IsInstanceOfType(VisualTreeHelper.GetChild(oldContainer, 0), typeof(ContentPresenter),
                "Container must not paint a second selection background.");
        }
        tags.ItemsSource = null;
    });

    [TestMethod]
    public void ActualSidebarTemplateShowsPinAndToggleMenuWithoutExecutingCommands() => Sta(() =>
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        var workspace = new FluentWorkspace();
        var tags = (ListBox)workspace.FindName("Tags");
        Assert.IsTrue(VirtualizingPanel.GetIsVirtualizing(tags));
        var button = (Button)tags.ItemTemplate.LoadContent();
        button.Resources.MergedDictionaries.Add(workspace.Resources);
        button.Resources["Pin"] = "固定"; button.Resources["Unpin"] = "取消固定";
        var menu = button.ContextMenu;
        menu.PlacementTarget = button;
        menu.Resources.MergedDictionaries.Add(button.Resources);
        var pinItem = (MenuItem)menu.Items[1];
        var pin = (TextBlock)((Grid)button.Content).Children[0];
        foreach (var isPinned in new[] { false, true, false })
        {
            button.DataContext = new { Name = "A very long synthetic tag name", IsPinned = isPinned };
            Drain(); button.Measure(new Size(152, 60));
            Assert.AreEqual(isPinned ? Visibility.Visible : Visibility.Hidden, pin.Visibility);
            Assert.AreEqual(isPinned ? "取消固定" : "固定", pinItem.Header);
            Assert.AreEqual("A very long synthetic tag name", button.ToolTip);
        }
        Assert.IsNotNull(pinItem.GetBindingExpression(MenuItem.CommandProperty));
        Assert.IsNotNull(pinItem.GetBindingExpression(MenuItem.CommandParameterProperty));
        Assert.AreEqual(1, button.InputBindings.Count, "Alt-click exclusion must remain available.");
    });

    [TestMethod]
    public void PinnedSortIsIndependentOfSourceAndDefaultView() => Sta(() =>
    {
        var source = new ObservableCollection<Tag> { new("ordinary", false, 0), new("b", true, 3), new("a", true, 3), new("first", true, 1) };
        var original = source.ToArray();
        var classic = System.Windows.Data.CollectionViewSource.GetDefaultView(source);
        using var view = new FluentTagView(source);
        CollectionAssert.AreEqual(new[] { "first", "a", "b", "ordinary" }, Names(view));
        CollectionAssert.AreEqual(original, source.ToArray());
        CollectionAssert.AreEqual(original, classic.Cast<Tag>().ToArray());
    });

    [TestMethod]
    public void LiveViewTracksPinOrderRenameAddRemoveAndReset() => Sta(() =>
    {
        var a = new Tag("a", false, 0); var b = new Tag("b", true, 1);
        var source = new ObservableCollection<Tag> { a, b };
        using var view = new FluentTagView(source);
        CollectionAssert.AreEqual(new[] { "b", "a" }, Names(view));
        // Simulate the model notification without invoking persistence or global IoC.
        typeof(Tag).GetField("_isPinned", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(b, false);
        b.RaisePropertyChanged(nameof(Tag.IsPinned)); Drain();
        CollectionAssert.AreEqual(new[] { "a", "b" }, Names(view));
        b.CustomOrder = -1; Drain();
        CollectionAssert.AreEqual(new[] { "b", "a" }, Names(view));
        b.CustomOrder = 0; a.Name = "z"; Drain();
        CollectionAssert.AreEqual(new[] { "b", "z" }, Names(view));
        source.Add(new Tag("pinned", true, 99));
        CollectionAssert.AreEqual(new[] { "pinned", "b", "z" }, Names(view));
        source.Remove(b); source.Clear();
        Assert.AreEqual(0, view.View.Count);
    });

    private sealed class TrackedTags : ObservableCollection<Tag>
    {
        public int Subscriptions { get; private set; }
        public override event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { Subscriptions++; base.CollectionChanged += value; }
            remove { Subscriptions--; base.CollectionChanged -= value; }
        }
    }

    [TestMethod]
    public void ReplacementViewsDetachSourceSubscriptions() => Sta(() =>
    {
        var source = new TrackedTags { new("tag", false, 0) };
        for (var i = 0; i < 20; i++)
        {
            var view = new FluentTagView(source);
            Assert.IsTrue(source.Subscriptions > 0);
            view.Dispose();
            Assert.AreEqual(0, source.Subscriptions);
            Assert.AreEqual(false, view.View.IsLiveSorting);
        }
    });

    [TestMethod]
    public void ExistingSerializedPinStateRestoresSidebarOrder() => Sta(() =>
    {
        var source = new ObservableCollection<Tag> { new("normal", false, 0), new("saved", true, 4) };
        var restored = JsonConvert.DeserializeObject<ObservableCollection<Tag>>(JsonConvert.SerializeObject(source))!;
        using var view = new FluentTagView(restored);
        CollectionAssert.AreEqual(new[] { "saved", "normal" }, Names(view));
        Assert.IsTrue(restored[1].IsPinned); Assert.AreEqual(4, restored[1].CustomOrder);
    });
}
