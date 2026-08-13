using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Shawn.Utils.Wpf;

namespace Shawn.Utils.WpfResources.Theme.Styles
{
    public abstract class WindowBase : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (NotifyPropertyChangedEnabled)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetAndNotifyIfChanged<T>(string propertyName, ref T oldValue, T newValue)
        {
            if (oldValue == null && newValue == null) return false;
            if (oldValue != null && oldValue.Equals(newValue)) return false;
            if (newValue != null && newValue.Equals(oldValue)) return false;
            oldValue = newValue;
            RaisePropertyChanged(propertyName);
            return true;
        }

        protected virtual bool SetAndNotifyIfChanged<T>(ref T oldValue, T newValue, [CallerMemberName] string? propertyName = null)
        {
            return SetAndNotifyIfChanged(propertyName!, ref oldValue, newValue);
        }

        #endregion INotifyPropertyChanged

        #region DragMove

        protected bool _isDragging = false;
        protected bool _doubleClickEnd = false;
        protected Point _mousePosition = new Point(-1, -1);
        public virtual void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _doubleClickEnd = false;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.ClickCount == 2)
                {
                    _doubleClickEnd = true;
                    _isDragging = false;
                    this.WindowState = (this.WindowState == WindowState.Normal)
                        ? WindowState.Maximized
                        : WindowState.Normal;
                    _mousePosition = new Point(-1, -1);
                }
                else
                {
                    _mousePosition = e.GetPosition(this);
                }
            }
        }

        public Action? OnDragEnd = null;

        public virtual void WinTitleBar_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed
                || _doubleClickEnd)
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    _mousePosition = new Point(-1, -1);
                    OnDragEnd?.Invoke();
                    SimpleLogHelper.Debug("OnDragEnd?.Invoke();");
                }
            }
            else
            {
                var position = e.GetPosition(this);
                if (!_isDragging
                    && _mousePosition.X > 0
                    && (Math.Abs(position.X - _mousePosition.X) > 2 || Math.Abs(position.Y - _mousePosition.Y) > 2))
                {
                    _isDragging = true;
                }


                if (_isDragging)
                {
                    if (this.WindowState == WindowState.Maximized)
                    {
                        var p = ScreenInfoEx.GetMouseVirtualPosition();
                        var top = p.Y;
                        var left = p.X;
                        this.Top = top - 15;
                        this.Left = left - this.Width / 2;
                        this.WindowState = WindowState.Normal;
                        this.Top = top - 15;
                        this.Left = left - this.Width / 2;
                    }

                    try
                    {
                        this.DragMove();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        #endregion DragMove

        public bool IsClosing { get; private set; } = false;
        public bool IsClosed { get; private set; } = false;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (e.Cancel != false) return;
            IsClosing = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsClosing = true;
            IsClosed = true;
        }

        public virtual void WinTitleBar_OnCloseButtonDown(object s, RoutedEventArgs e)
        {
            this.Close();
        }
        public virtual void WinTitleBar_OnMaximizeButtonDown(object s, RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }
        public virtual void WinTitleBar_OnMinimizeButtonDown(object s, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        protected WindowBase()
        {
#if DEBUG
            SimpleLogHelper.Debug($"{this.GetType().Name} init");
#endif
        }

        public new void Hide()
        {
            if (IsClosing == false)
                base.Hide();
        }

        public new void Show()
        {
            if (IsClosing == false)
                base.Show();
        }

        public new void Activate()
        {
            if (IsClosing == false)
                base.Activate();
        }


        /// <summary>
        /// Ensures that a click anywhere on the window brings it to the foreground.
        /// Precision trackpad taps may not trigger the standard WM_MOUSEACTIVATE path,
        /// so we explicitly activate the window on the first PreviewMouseDown if it is
        /// not already the active window.
        /// </summary>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (!IsActive)
            {
                Activate();
            }
        }
    }
}