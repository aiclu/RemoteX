#if RUST_SSH
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.RustSsh;
using Microsoft.Terminal.Wpf;
using Shawn.Utils;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    /// <summary>
    /// Host for the Rust-backed SSH terminal. Renders remote output via
    /// <c>Microsoft.Terminal.Wpf.TerminalControl</c>, driven by the in-process
    /// Rust SSH core through <see cref="SshRustBridge"/>.
    ///
    /// Only compiled into net9 builds (<c>RUST_SSH</c>).
    /// </summary>
    public sealed partial class RustSshHost : HostBase
    {
        private readonly SSH _ssh;
        private SshRustBridge? _bridge;
        private RustSshConnection? _connection;
        private TerminalControl? _terminal;
        private volatile bool _connectRequested;
        private bool _invokeOnClosedWhenDisconnected = true;

        public static RustSshHost Create(SSH protocolServer)
        {
            RustSshHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new RustSshHost(protocolServer);
            });
            return view!;
        }

        private RustSshHost(SSH ssh) : base(ssh, true)
        {
            InitializeComponent();
            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;
            _ssh = ssh;
        }

        #region Base Interface

        public override void Conn()
        {
            if (_connectRequested) return;
            _connectRequested = true;

            Status = ProtocolHostStatus.Connecting;
            GridLoading.Visibility = Visibility.Visible;
            GridMessageBox.Visibility = Visibility.Collapsed;

            // Copy needed values so the background task does not touch the UI-owned object.
            var ssh = _ssh;
            ssh.DecryptToConnectLevel();
            var host = ssh.Address;
            var port = (ushort)ssh.GetPort();
            var user = ssh.UserName;
            var password = ssh.Password;
            var privateKey = string.IsNullOrEmpty(ssh.PrivateKey) ? null : ssh.PrivateKey;
            var startupCommand = ssh.StartupAutoCommand;

            Task.Run(() =>
            {
                var bridge = new SshRustBridge();
                string? error = null;
                try
                {
                    error = bridge.Connect(host, port, user, password, privateKey);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }

                Execute.OnUIThread(() =>
                {
                    if (error != null)
                    {
                        ShowError(error);
                        _connectRequested = false;
                        return;
                    }

                    _bridge = bridge;
                    _connection = new RustSshConnection(bridge);
                    EnsureTerminal();

                    bridge.OnData += data => Execute.OnUIThread(() => _connection?.PushRemoteData(data));
                    bridge.OnClosed += () => Execute.OnUIThread(OnDisconnected);
                    bridge.OnError += msg => Execute.OnUIThread(() => ShowError(msg));

                    _terminal!.Connection = _connection;

                    // Inject the startup auto-command into the shell (UTF-8).
                    if (!string.IsNullOrEmpty(startupCommand))
                    {
                        var cmd = Encoding.UTF8.GetBytes(startupCommand);
                        bridge.Write(cmd);
                    }

                    GridLoading.Visibility = Visibility.Collapsed;
                    Status = ProtocolHostStatus.Connected;
                });
            });
        }

        public override void ReConn()
        {
            _connectRequested = false;
            GridLoading.Visibility = Visibility.Visible;
            GridMessageBox.Visibility = Visibility.Collapsed;
            _invokeOnClosedWhenDisconnected = false;
            CloseConnection();
            Conn();
            _invokeOnClosedWhenDisconnected = true;
        }

        public override void Close()
        {
            Status = ProtocolHostStatus.Disconnected;
            CloseConnection();
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

        #region helpers

        private void EnsureTerminal()
        {
            if (_terminal != null) return;
            _terminal = new TerminalControl { AutoResize = true };
            TerminalHost.Children.Add(_terminal);
        }

        private void CloseConnection()
        {
            _connection?.Close();
            _bridge?.Dispose();
            _bridge = null;
            _connection = null;
        }

        private void OnDisconnected()
        {
            if (_connectRequested == false) return;
            Status = ProtocolHostStatus.Disconnected;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Visible;
            TbMessageTitle.Visibility = Visibility.Collapsed;
            BtnReconn.Visibility = Visibility.Visible;
            TbMessage.Text = IoC.Translate("Disconnected");
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

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }

        #endregion helpers
    }
}
#endif
