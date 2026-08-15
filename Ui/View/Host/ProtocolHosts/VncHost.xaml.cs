using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.RustVnc;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    /// <summary>
    /// VNC host rendered from the Rust RFB core. The framebuffer is decoded in
    /// Rust (<c>ssh_rust.dll</c>) and blitted onto a <see cref="WriteableBitmap"/>.
    /// Keyboard and pointer input are forwarded as RFB events. Only compiled into
    /// net9 builds.
    /// </summary>
    public sealed partial class VncHost : HostBase
    {
        private readonly VNC _vnc;
        private long _handle;
        private WriteableBitmap? _bitmap;
        private CancellationTokenSource? _cts;
        private bool _invokeOnClosedWhenDisconnected = true;
        private byte _mouseButtons;

        public static VncHost Create(VNC protocolServer)
        {
            VncHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new VncHost(protocolServer);
            });
            return view!;
        }

        private VncHost(VNC vnc) : base(vnc, false)
        {
            InitializeComponent();
            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;
            _vnc = vnc;

            VncImage.MouseMove += VncImage_OnMouseMove;
            VncImage.MouseDown += VncImage_OnMouseDown;
            VncImage.MouseUp += VncImage_OnMouseUp;
            VncImage.MouseWheel += VncImage_OnMouseWheel;
            VncImage.PreviewKeyDown += VncImage_OnPreviewKeyDown;
            VncImage.PreviewKeyUp += VncImage_OnPreviewKeyUp;
            VncImage.PreviewMouseLeftButtonDown += (_, _) => { VncImage.Focus(); };

            MenuItems.Add(new System.Windows.Controls.Separator());
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Ctrl + Alt + Del",
                Command = new RelayCommand(o => SendSpecialKeySeq(0xFFE3, 0xFFE9, 0xFFFF), o => Status == ProtocolHostStatus.Connected)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Ctrl + Esc",
                Command = new RelayCommand(o => SendSpecialKeySeq(0xFFE3, 0xFF1B), o => Status == ProtocolHostStatus.Connected)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Alt + F4",
                Command = new RelayCommand(o => SendSpecialKeySeq(0xFFE9, 0xFF08), o => Status == ProtocolHostStatus.Connected)
            });
            {
                var tb = new System.Windows.Controls.TextBlock();
                tb.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand(o => ReConn())
                });
            }
            {
                var tb = new System.Windows.Controls.TextBlock();
                tb.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand(o => Close())
                });
            }
        }

        #region Base Interface

        public override void Conn()
        {
            Status = ProtocolHostStatus.Connecting;
            GridLoading.Visibility = Visibility.Visible;
            GridMessageBox.Visibility = Visibility.Collapsed;

            var host = _vnc.Address;
            var port = (ushort)(_vnc.GetPort() > 0 ? _vnc.GetPort() : 5900);
            var password = UnSafeStringEncipher.DecryptOrReturnOriginalString(_vnc.Password);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() =>
            {
                var errBuf = new byte[1024];
                var rc = VncRustNative.sr_vnc_connect(host, port, string.IsNullOrEmpty(password) ? null : password, 15, out var handle, errBuf, errBuf.Length);
                if (rc != VncRustNative.SR_OK)
                {
                    var msg = Encoding.UTF8.GetString(errBuf).TrimEnd('\0');
                    Execute.OnUIThread(() => ShowError(string.IsNullOrEmpty(msg) ? "VNC connect failed" : msg));
                    return;
                }
                Execute.OnUIThread(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        VncRustNative.sr_vnc_disconnect(handle);
                        return;
                    }
                    _handle = handle;
                    RenderLoop(token);
                });
            }, token);
        }

        public override void ReConn()
        {
            _invokeOnClosedWhenDisconnected = false;
            Close();
            _invokeOnClosedWhenDisconnected = true;
            Conn();
        }

        public override void Close()
        {
            // Close() may be invoked from a background thread (e.g. the session
            // cleanup timer in SessionControlService), while Status and the
            // VncImage dependency property are bound to the UI thread. Delegate
            // the UI-touching part back to the dispatcher.
            Execute.OnUIThread(() =>
            {
                Status = ProtocolHostStatus.Disconnected;
                VncImage.Source = null;
                _bitmap = null;
            });
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_handle != 0)
            {
                VncRustNative.sr_vnc_disconnect(_handle);
                _handle = 0;
            }
            base.Close();
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Native;
        }

        public override IntPtr GetHostHwnd()
        {
            return IntPtr.Zero;
        }

        #endregion Base Interface

        #region rendering

        /// <summary>
        /// Poll loop: wait for the RFB handshake, then copy the framebuffer into
        /// a WriteableBitmap every time the frame sequence advances.
        /// </summary>
        private void RenderLoop(CancellationToken token)
        {
            // Poll until connected (handshake done) or closed.
            var errBuf = new byte[1024];
            while (!token.IsCancellationRequested)
            {
                var rc = VncRustNative.sr_vnc_poll(_handle, errBuf, errBuf.Length);
                if (rc == VncRustNative.SR_ERR_INVALID_HANDLE || rc == VncRustNative.SR_ERR_CLOSED)
                {
                    var msg = Encoding.UTF8.GetString(errBuf).TrimEnd('\0');
                    Execute.OnUIThread(() => OnDisconnected(msg));
                    return;
                }
                if (rc == VncRustNative.SR_OK) break;
                Thread.Sleep(100);
            }
            if (token.IsCancellationRequested) return;

            VncRustNative.sr_vnc_get_size(_handle, out var w, out var h);
            var width = (int)w;
            var height = (int)h;
            var buffer = new byte[width * height * 4];

            Execute.OnUIThread(() =>
            {
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                VncImage.Source = _bitmap;
                GridLoading.Visibility = Visibility.Collapsed;
                Status = ProtocolHostStatus.Connected;
            });

            VncRustNative.sr_vnc_frame_seq(_handle, out var lastSeq);
            while (!token.IsCancellationRequested)
            {
                // Copy the framebuffer once per frame bump.
                VncRustNative.sr_vnc_frame_seq(_handle, out var seq);
                if (seq != lastSeq)
                {
                    var rc = VncRustNative.sr_vnc_get_frame(_handle, buffer, buffer.Length, out var len);
                    if (rc == VncRustNative.SR_OK && len == buffer.Length)
                    {
                        Execute.OnUIThread(() => Blit(buffer));
                        lastSeq = seq;
                    }
                    else if (rc == VncRustNative.SR_ERR_CLOSED || rc == VncRustNative.SR_ERR_INVALID_HANDLE)
                    {
                        Execute.OnUIThread(() => OnDisconnected(""));
                        return;
                    }
                }
                // Ask Rust to request incremental updates periodically.
                if (lastSeq == seq)
                {
                    VncRustNative.sr_vnc_request_update(_handle);
                }
                Thread.Sleep(30);
            }
        }

        private void Blit(byte[] buffer)
        {
            if (_bitmap == null) return;
            _bitmap.Lock();
            try
            {
                var stride = _bitmap.PixelWidth * 4;
                _bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight), buffer, stride, 0);
            }
            finally
            {
                _bitmap.Unlock();
            }
        }

        private void OnDisconnected(string message)
        {
            if (Status == ProtocolHostStatus.Disconnected) return;
            Status = ProtocolHostStatus.Disconnected;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Visible;
            TbMessageTitle.Visibility = Visibility.Collapsed;
            BtnReconn.Visibility = Visibility.Visible;
            TbMessage.Text = string.IsNullOrEmpty(message) ? IoC.Translate("Disconnected") : message;
            if (_invokeOnClosedWhenDisconnected)
                base.OnClosed?.Invoke(base.ConnectionId);
        }

        private void ShowError(string message)
        {
            Status = ProtocolHostStatus.Disconnected;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Visible;
            TbMessageTitle.Visibility = Visibility.Collapsed;
            BtnReconn.Visibility = Visibility.Visible;
            TbMessage.Text = message;
        }

        #endregion rendering

        #region input

        /// <summary>
        /// Map a WPF pointer position to RFB framebuffer coordinates. With
        /// Stretch=Uniform the image is letterboxed; compute the viewport rect.
        /// </summary>
        private void VncImage_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_handle == 0 || _bitmap == null) return;
            if (!TryMapToFb(e.GetPosition(VncImage), out var x, out var y)) return;
            VncRustNative.sr_vnc_send_pointer(_handle, x, y, _mouseButtons);
        }

        private void VncImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_handle == 0 || _bitmap == null) return;
            VncImage.Focus();
            _mouseButtons |= ButtonMask(e.ChangedButton);
            if (!TryMapToFb(e.GetPosition(VncImage), out var x, out var y)) return;
            VncRustNative.sr_vnc_send_pointer(_handle, x, y, _mouseButtons);
        }

        private void VncImage_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_handle == 0 || _bitmap == null) return;
            _mouseButtons &= (byte)~ButtonMask(e.ChangedButton);
            if (!TryMapToFb(e.GetPosition(VncImage), out var x, out var y)) return;
            VncRustNative.sr_vnc_send_pointer(_handle, x, y, _mouseButtons);
        }

        private void VncImage_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_handle == 0 || _bitmap == null) return;
            var button = e.Delta > 0 ? PtrWheelUp : PtrWheelDown;
            if (!TryMapToFb(e.GetPosition(VncImage), out var x, out var y)) return;
            VncRustNative.sr_vnc_send_pointer(_handle, x, y, (byte)(_mouseButtons | button));
            VncRustNative.sr_vnc_send_pointer(_handle, x, y, _mouseButtons);
        }

        private const byte PtrWheelUp = 8;
        private const byte PtrWheelDown = 16;

        private bool TryMapToFb(System.Windows.Point p, out ushort x, out ushort y)
        {
            x = 0;
            y = 0;
            if (_bitmap == null) return false;
            var imageW = VncImage.ActualWidth;
            var imageH = VncImage.ActualHeight;
            if (imageW <= 0 || imageH <= 0) return false;
            var fbW = _bitmap.PixelWidth;
            var fbH = _bitmap.PixelHeight;

            // Stretch=Uniform: compute the fitted rect.
            var scale = Math.Min(imageW / fbW, imageH / fbH);
            var drawW = fbW * scale;
            var drawH = fbH * scale;
            var offX = (imageW - drawW) / 2;
            var offY = (imageH - drawH) / 2;

            var relX = p.X - offX;
            var relY = p.Y - offY;
            if (relX < 0 || relY < 0 || relX > drawW || relY > drawH) return false;
            x = (ushort)Math.Max(0, Math.Min(fbW - 1, relX / scale));
            y = (ushort)Math.Max(0, Math.Min(fbH - 1, relY / scale));
            return true;
        }

        private static byte ButtonMask(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => 1,
                MouseButton.Middle => 2,
                MouseButton.Right => 4,
                _ => 0,
            };
        }

        private void VncImage_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_handle == 0) return;
            var sym = KeyToKeysym(e.Key);
            if (sym != 0) VncRustNative.sr_vnc_send_key(_handle, sym, true);
            e.Handled = true;
        }

        private void VncImage_OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (_handle == 0) return;
            var sym = KeyToKeysym(e.Key);
            if (sym != 0) VncRustNative.sr_vnc_send_key(_handle, sym, false);
            e.Handled = true;
        }

        /// <summary>
        /// Best-effort WPF Key -> X11 keysym mapping covering the common
        /// alphanumeric and control keys.
        /// </summary>
        private static uint KeyToKeysym(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return (uint)(0x61 + (key - Key.A)); // lowercase a-z
            if (key >= Key.D0 && key <= Key.D9)
                return (uint)(0x30 + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (uint)(0xffb0 + (key - Key.NumPad0));
            return key switch
            {
                Key.Enter or Key.Return => 0xff0d,
                Key.Back => 0xff08,
                Key.Tab => 0xff09,
                Key.Space => 0x0020,
                Key.Escape => 0xff1b,
                Key.Delete => 0xffff,
                Key.Insert => 0xff63,
                Key.Home => 0xff50,
                Key.End => 0xff57,
                Key.PageUp => 0xff55,
                Key.PageDown => 0xff56,
                Key.Up => 0xff52,
                Key.Down => 0xff54,
                Key.Left => 0xff51,
                Key.Right => 0xff53,
                Key.LeftShift => 0xffe1,
                Key.RightShift => 0xffe2,
                Key.LeftCtrl => 0xffe3,
                Key.RightCtrl => 0xffe4,
                Key.LeftAlt => 0xffe9,
                Key.RightAlt => 0xffea,
                Key.F1 => 0xffbe,
                Key.F2 => 0xffbf,
                Key.F3 => 0xffc0,
                Key.F4 => 0xffc1,
                Key.F5 => 0xffc2,
                Key.F6 => 0xffc3,
                Key.F7 => 0xffc4,
                Key.F8 => 0xffc5,
                Key.F9 => 0xffc6,
                Key.F10 => 0xffc7,
                Key.F11 => 0xffc8,
                Key.F12 => 0xffc9,
                _ => 0,
            };
        }

        /// <summary>
        /// Send a chord of keysyms (press in order, then release in reverse).
        /// Used for Ctrl+Alt+Del / Ctrl+Esc / Alt+F4.
        /// </summary>
        private void SendSpecialKeySeq(params uint[] keysyms)
        {
            if (_handle == 0) return;
            foreach (var sym in keysyms) VncRustNative.sr_vnc_send_key(_handle, sym, true);
            for (var i = keysyms.Length - 1; i >= 0; i--) VncRustNative.sr_vnc_send_key(_handle, keysyms[i], false);
        }

        #endregion input

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }
    }
}
