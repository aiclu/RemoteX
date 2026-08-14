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
    /// Host for the Rust-backed terminal (SSH / Telnet / Serial). Renders remote
    /// output via <c>Microsoft.Terminal.Wpf.TerminalControl</c>, driven by the
    /// in-process Rust core through <see cref="SshRustBridge"/>.
    ///
    /// Only compiled into net9 builds (<c>RUST_SSH</c>).
    /// </summary>
    public sealed partial class RustTerminalHost : HostBase
    {
        private readonly ProtocolBase _protocol;
        private SshRustBridge? _bridge;
        private RustSshConnection? _connection;
        private TerminalControl? _terminal;
        private volatile bool _connectRequested;
        private bool _invokeOnClosedWhenDisconnected = true;

        public static RustTerminalHost Create(ProtocolBase protocolServer)
        {
            RustTerminalHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new RustTerminalHost(protocolServer);
            });
            return view!;
        }

        private RustTerminalHost(ProtocolBase protocolServer) : base(protocolServer, true)
        {
            InitializeComponent();
            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;
            _protocol = protocolServer;
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
            var protocol = _protocol;
            var encoding = GetEncoding(protocol);
            var startupCommand = GetStartupAutoCommand(protocol);
            var connectParams = BuildConnectParams(protocol);

            Task.Run(() =>
            {
                var bridge = new SshRustBridge();
                string? error = null;
                try
                {
                    error = connectParams.Connect(bridge);
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
                    _connection = new RustSshConnection(bridge, encoding);
                    EnsureTerminal();

                    bridge.OnData += data => Execute.OnUIThread(() => _connection?.PushRemoteData(data));
                    bridge.OnClosed += () => Execute.OnUIThread(OnDisconnected);
                    bridge.OnError += msg => Execute.OnUIThread(() => ShowError(msg));

                    _terminal!.Connection = _connection;

                    // Inject the startup auto-command into the session.
                    if (!string.IsNullOrEmpty(startupCommand))
                    {
                        var cmd = encoding.GetBytes(startupCommand);
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

        /// <summary>
        /// Session byte-stream encoding: SSH/Telnet use UTF-8; Serial uses the
        /// user-configured code page.
        /// </summary>
        private static Encoding GetEncoding(ProtocolBase protocol)
        {
            if (protocol is Serial serial)
            {
                try
                {
                    return Encoding.GetEncoding(serial.EncodingCodePage);
                }
                catch
                {
                    // fall back to UTF-8 for an invalid/unsupported code page
                }
            }
            return Encoding.UTF8;
        }

        private static string? GetStartupAutoCommand(ProtocolBase protocol)
        {
            return protocol switch
            {
                SSH ssh => ssh.StartupAutoCommand,
                Telnet telnet => telnet.StartupAutoCommand,
                Serial serial => serial.StartupAutoCommand,
                _ => null,
            };
        }

        /// <summary>
        /// Capture the protocol-specific connect parameters on the UI thread,
        /// then run the actual FFI connect on the background thread.
        /// </summary>
        private static ConnectParams BuildConnectParams(ProtocolBase protocol)
        {
            switch (protocol)
            {
                case SSH ssh:
                    ssh.DecryptToConnectLevel();
                    return new SshConnectParams(ssh.Address, (ushort)ssh.GetPort(), ssh.UserName, ssh.Password, string.IsNullOrEmpty(ssh.PrivateKey) ? null : ssh.PrivateKey);
                case Telnet telnet:
                    return new TelnetConnectParams(telnet.Address, (ushort)telnet.GetPort());
                case Serial serial:
                    return new SerialConnectParams(serial);
                default:
                    throw new NotSupportedException($"Protocol {protocol.GetType().Name} is not supported by RustTerminalHost");
            }
        }

        private abstract class ConnectParams
        {
            public abstract string? Connect(SshRustBridge bridge);
        }

        private sealed class SshConnectParams : ConnectParams
        {
            private readonly string _host;
            private readonly ushort _port;
            private readonly string _user;
            private readonly string? _password;
            private readonly string? _keyPath;

            public SshConnectParams(string host, ushort port, string user, string? password, string? keyPath)
            {
                _host = host;
                _port = port;
                _user = user;
                _password = password;
                _keyPath = keyPath;
            }

            public override string? Connect(SshRustBridge bridge) => bridge.Connect(_host, _port, _user, _password, _keyPath);
        }

        private sealed class TelnetConnectParams : ConnectParams
        {
            private readonly string _host;
            private readonly ushort _port;

            public TelnetConnectParams(string host, ushort port)
            {
                _host = host;
                _port = port;
            }

            public override string? Connect(SshRustBridge bridge) => bridge.ConnectTelnet(_host, _port);
        }

        private sealed class SerialConnectParams : ConnectParams
        {
            private readonly string _portName;
            private readonly uint _baudRate;
            private readonly byte _dataBits;
            private readonly byte _parity;
            private readonly byte _stopBits;
            private readonly byte _flowControl;

            public SerialConnectParams(Serial serial)
            {
                _portName = serial.SerialPort;
                _baudRate = (uint)serial.GetBitRate();
                _dataBits = byte.TryParse(serial.DataBits, out var db) ? db : (byte)8;
                _parity = serial.Parity switch
                {
                    "ODD" => (byte)1,
                    "EVEN" => (byte)2,
                    "MARK" => (byte)3,
                    "SPACE" => (byte)4,
                    _ => (byte)0,
                };
                _stopBits = serial.StopBits == "2" ? (byte)1 : (byte)0;
                _flowControl = serial.FlowControl switch
                {
                    "XON/XOFF" => (byte)1,
                    "RTS/CTS" => (byte)2,
                    "DSR/DTR" => (byte)3,
                    _ => (byte)0,
                };
            }

            public override string? Connect(SshRustBridge bridge) => bridge.ConnectSerial(_portName, _baudRate, _dataBits, _parity, _stopBits, _flowControl);
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
