using System.Windows;
using System.Windows.Controls;
using _1RM.View.ServerView;

namespace _1RM.View
{
    public partial class FluentConnectionRow : UserControl
    {
        public FluentConnectionRow() => InitializeComponent();
        private void SelectionClick(object sender, RoutedEventArgs e) =>
            ServerListPageView.ItemsCheckBox_OnClick_Static(sender, e);
    }
}
