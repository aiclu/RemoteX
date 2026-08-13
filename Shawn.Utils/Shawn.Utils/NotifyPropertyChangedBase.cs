using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shawn.Utils
{
    public class PropertyChangedEventArgsEx : PropertyChangedEventArgs
    {
        public PropertyChangedEventArgsEx(string? propertyName, object? arg) : base(propertyName)
        {
            Arg = arg;
        }
        public object? Arg { get; }
    }

    public interface INotifyPropertyChangedBase
    {
        public void SetNotifyPropertyChangedEnabled(bool isEnabled);
        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null, object? arg = null);
        public bool SetAndNotifyIfChangedWithArg<T>(ref T oldValue, T newValue, object arg, [CallerMemberName] string? propertyName = null);
    }


    public class NotifyPropertyChangedBase : INotifyPropertyChanged, INotifyPropertyChangedBase
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        #region INotifyPropertyChanged

        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }

        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null, object? arg = null)
        {
            if (NotifyPropertyChangedEnabled)
            {
                PropertyChanged?.Invoke(this, arg != null ? new PropertyChangedEventArgsEx(propertyName, arg) : new PropertyChangedEventArgs(propertyName));
            }
        }

        private bool SetAndNotifyIfChanged<T>(string? propertyName, ref T oldValue, T newValue, object? arg = null)
        {
            if (oldValue == null && newValue == null) return false;
            if (oldValue != null && oldValue.Equals(newValue)) return false;
            if (newValue != null && newValue.Equals(oldValue)) return false;
            oldValue = newValue;
            RaisePropertyChanged(propertyName, arg);
            return true;
        }

        protected virtual bool SetAndNotifyIfChanged<T>(ref T oldValue, T newValue, [CallerMemberName] string? propertyName = null)
        {
            return SetAndNotifyIfChanged(propertyName, ref oldValue, newValue);
        }



        public virtual bool SetAndNotifyIfChangedWithArg<T>(ref T oldValue, T newValue, object arg, [CallerMemberName] string? propertyName = null)
        {
            return SetAndNotifyIfChanged(propertyName, ref oldValue, newValue, arg);
        }

        #endregion INotifyPropertyChanged
    }
}