using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using _1RM.Model;
using _1RM.View.ServerView;

namespace _1RM.View
{
    public partial class FluentConnectionRow : UserControl
    {
        private ContextMenu? _alternateMenu;

        public FluentConnectionRow()
        {
            InitializeComponent();
            // Recycled rows must never retain a popup/actions for the previous connection.
            DataContextChanged += (_, _) => CloseAlternateMenu();
            Unloaded += (_, _) => CloseAlternateMenu();
        }

        internal ContextMenu? CreateAlternateConnectionsMenu()
        {
            CloseAlternateMenu();
            if (DataContext is not ProtocolBaseViewModel vm) return null;
            var server = vm.Server;
            var actions = server.GetAlternateConnectionActions(nameof(MainWindowView));
            if (actions.Count == 0) return null;

            var menu = new ContextMenu
            {
                PlacementTarget = RowAlternatives,
                Placement = PlacementMode.Bottom,
                Style = (Style)FindResource("FluentWorkspaceMenu"),
                FontFamily = FontFamily,
                FontSize = FontSize,
                MaxHeight = Math.Min(400, SystemParameters.WorkArea.Height * 0.8),
                MaxWidth = Math.Min(400, SystemParameters.WorkArea.Width * 0.8)
            };
            // Popups are outside the page's visual tree; use its current local palette.
            foreach (var key in new[] { "FluentSurface", "FluentText", "FluentAccent", "FluentMuted", "FluentHover", "FluentStroke" })
                menu.Resources[key] = FindResource(key);
            foreach (var action in actions)
            {
                var item = new MenuItem
                {
                    Header = action.ActionName,
                    HeaderTemplate = (DataTemplate)Resources["AlternateConnectionLabel"],
                    ToolTip = action.ActionName
                };
                item.Click += (_, e) =>
                {
                    e.Handled = true;
                    // Close before a connection can hide the main window or recycle this row.
                    CloseAlternateMenu();
                    if (ReferenceEquals(DataContext, vm) && ReferenceEquals(vm.Server, server)) action.Run();
                };
                menu.Items.Add(item);
            }
            menu.Closed += (_, _) =>
            {
                menu.Items.Clear();
                menu.PlacementTarget = null;
                if (ReferenceEquals(_alternateMenu, menu)) _alternateMenu = null;
            };
            _alternateMenu = menu;
            return menu;
        }

        private void OpenAlternateConnections(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_alternateMenu?.IsOpen == true) { CloseAlternateMenu(); return; }
            if (CreateAlternateConnectionsMenu() is { } menu) menu.IsOpen = true;
        }

        private void ConnectionActionsKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != Key.Down || RowAlternatives.Visibility != Visibility.Visible) return;
            if (!RowAlternatives.IsKeyboardFocused && (Keyboard.Modifiers & ModifierKeys.Alt) == 0) return;
            OpenAlternateConnections(sender, e);
            _alternateMenu?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        private void CloseAlternateMenu()
        {
            var menu = _alternateMenu;
            _alternateMenu = null;
            if (menu == null) return;
            menu.IsOpen = false;
            menu.Items.Clear();
            menu.PlacementTarget = null;
        }

        private void SelectionClick(object sender, RoutedEventArgs e) =>
            ServerListPageView.ItemsCheckBox_OnClick_Static(sender, e);
    }
}
