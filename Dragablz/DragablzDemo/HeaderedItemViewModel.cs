using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DragablzDemo
{
    // Sample-only model: the RemoteX fork no longer exposes this upstream helper.
    public class HeaderedItemViewModel : INotifyPropertyChanged
    {
        private object _header;
        private object _content;
        private bool _isSelected;
        public object Header { get => _header; set { _header = value; Changed(); } }
        public object Content { get => _content; set { _content = value; Changed(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; Changed(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
