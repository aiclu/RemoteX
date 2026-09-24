using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

// Synthetic data only. No bootstrapper, database, protocol host or user configuration.
internal static class Program
{
    public sealed class Node
    {
        public string Name { get; set; } = "";
        public List<Node> Children { get; } = new();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        var resources = new ResourceDictionary {
            Source = new Uri("/RemoteX;component/Resources/Theme/FluentServerViews.xaml", UriKind.Relative)
        };
        var style = new Style(typeof(TreeViewItem));
        if (!args.Contains("--stock")) style.Setters.Add(new Setter(Control.TemplateProperty, resources["FluentTreeItemTemplate"]));
        style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 2)));
        var template = new HierarchicalDataTemplate(typeof(Node)) { ItemsSource = new Binding("Children"), ItemContainerStyle = style };
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding("Name")); template.VisualTree = text;
        var roots = Enumerable.Range(0, 4).Select(i => new Node { Name = "Synthetic source " + i }).ToList();
        foreach (var root in roots)
            for (var i = 0; i < 300; i++) root.Children.Add(new Node { Name = "Synthetic connection " + i });
        var tree = new TreeView { ItemsSource = roots, ItemTemplate = template, ItemContainerStyle = style, UseLayoutRounding = true };
        tree.Style = (Style)resources["FluentTreeLayout"];
        _1RM.View.FluentPage.SetActive(tree, true);
        // Explicit baseline switch to reproduce the old failure; the watchdog bounds it.
        if (args.Contains("--virtualized")) VirtualizingPanel.SetIsVirtualizing(tree, true);
        var window = new Window { Content = tree, Width = 800, Height = 600, ShowActivated = false,
            ShowInTaskbar = false, Left = -2000, Top = -2000, Title = "RemoteX synthetic layout probe" };
        var layoutPasses = 0;
        tree.LayoutUpdated += (_, _) => layoutPasses++;
        var stage = 0;
        Exception? failure = null;
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            try
            {
                Console.WriteLine($"stage={stage} layoutPasses={layoutPasses} containers={Descendants(tree).OfType<TreeViewItem>().Count()}");
                if (layoutPasses > 100) throw new Exception("Layout failed to settle before idle.");
                layoutPasses = 0;
                var scroll = Descendants(tree).OfType<ScrollViewer>().First();
                switch (stage++)
                {
                    case 0: scroll.ScrollToVerticalOffset(100); break;
                    case 1: scroll.ScrollToEnd(); break;
                    case 2: scroll.ScrollToHome(); tree.FontSize = 24; window.Width = 480; break;
                    case 3: ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = false; break;
                    case 4: ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true; break;
                    case 5: window.Content = new TextBlock { Text = "Other view" }; break;
                    case 6: window.Content = tree; break;
                    default: timer.Stop(); window.Close(); break;
                }
            }
            catch (Exception ex) { failure = ex; timer.Stop(); window.Close(); }
        };
        // Independent watchdog also catches starvation of the UI dispatcher.
        using var watchdog = new System.Threading.Timer(_ => { Console.Error.WriteLine("UI heartbeat timeout"); Environment.Exit(2); }, null, 15000, System.Threading.Timeout.Infinite);
        window.Loaded += (_, _) => timer.Start();
        app.Run(window);
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine("PASS: load, scroll, resize, large font, expansion and view reattachment.");
        return 0;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
