using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AxMSTSCLib;
using MSTSCLib;
using _1RM.Model.Protocol;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using _1RM.Service;
using _1RM.Utils.Tracing;
using _1RM.View.Host;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class AxMsRdpClient10Host : HostBase, IDisposable
    {
        protected override bool TryShowNativeConnectionInfo()
        {
            Dispatcher.VerifyAccess();
            var client = _rdpClient;
            return ConnectionInfoPresentation.TryShowRdp(
                () => client is { IsDisposed: false, Disposing: false, IsHandleCreated: true }
                      && ReferenceEquals(client, _rdpClient),
                () => client!.Connected,
                () => client!.TrySetExtendedProperty("ShowConnectionInformation", true));
        }

        protected override string GetConnectionInfoSummary()
        {
            var metrics = _rdpNetworkMetrics.Snapshot();
            var qualityKey = RdpNetworkMetricsFormatter.GetQualityTranslationKey(metrics.QualityLevel);
            var quality = qualityKey is null
                ? ConnectionInfoUnavailable
                : ConnectionInfoTranslate(qualityKey, qualityKey);
            return $"{ConnectionInfoTranslate("Connection quality", "Connection quality")}: {quality}";
        }

        protected override ConnectionInfoSection BuildNetworkDetailsSection()
        {
            var metrics = _rdpNetworkMetrics.Snapshot();
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Network details", "Network details"),
                new[]
                {
                    CreateConnectionInfoRow("Transport protocol", () => null),
                    CreateConnectionInfoRow("Round-trip time", () => RdpNetworkMetricsFormatter.FormatRoundTripTime(metrics.RoundTripTimeMilliseconds)),
                    CreateConnectionInfoRow("Available bandwidth", () => FormatRdpBandwidth(metrics.BandwidthKbps)),
                    CreateConnectionInfoRow("Frame rate", () => null),
                });
        }

        private string FormatRdpBandwidth(int? bandwidthKbps)
        {
            var formatted = RdpNetworkMetricsFormatter.FormatBandwidth(bandwidthKbps);
            if (formatted is null)
            {
                return ConnectionInfoUnavailable;
            }

            return formatted == "Less than 100 Kbps"
                ? ConnectionInfoTranslate("Less than 100 Kbps", formatted)
                : formatted;
        }

        protected override ConnectionInfoSection BuildClientDetailsSection()
        {
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Client details", "Client details"),
                new[]
                {
                    CreateConnectionInfoRow("Client version", GetRdpClientVersion),
                    CreateConnectionInfoRow("Local OS", GetLocalOsDescription),
                });
        }

        protected override ConnectionInfoSection BuildRemoteComputerDetailsSection()
        {
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Remote computer details", "Remote computer details"),
                new[]
                {
                    CreateConnectionInfoRow("Remote session type", () => ConnectionInfoTranslate("Remote desktop", "Remote desktop")),
                    CreateConnectionInfoRow("Network name", () => null),
                    CreateConnectionInfoRow("Gateway name", GetRdpGatewayName),
                    CreateConnectionInfoRow("Gateway logon method", GetRdpGatewayLogonMethod),
                    CreateConnectionInfoRow("Remote computer", GetRdpRemoteComputer),
                });
        }

        private string GetRdpClientVersion()
        {
            try
            {
                return ConnectionInfoValueOrUnavailable(_rdpClient?.Version);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read RDP client version: {e.Message}");
                return ConnectionInfoUnavailable;
            }
        }

        private string GetRdpGatewayName()
        {
            try
            {
                if (_rdpSettings.GatewayMode is null or EGatewayMode.DoNotUseGateway)
                {
                    return ConnectionInfoTranslate("Not in use", "Not in use");
                }

                var gatewayHostName = _rdpClient?.TransportSettings2?.GatewayHostname;
                if (!string.IsNullOrWhiteSpace(gatewayHostName))
                {
                    return gatewayHostName;
                }

                if (!string.IsNullOrWhiteSpace(_rdpSettings.GatewayHostName))
                {
                    return _rdpSettings.GatewayHostName;
                }

                return _rdpSettings.GatewayMode == EGatewayMode.AutomaticallyDetectGatewayServerSettings
                    ? ConnectionInfoTranslate("Automatically detected", "Automatically detected")
                    : ConnectionInfoUnavailable;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read RDP gateway name: {e.Message}");
                return ConnectionInfoUnavailable;
            }
        }

        private string GetRdpGatewayLogonMethod()
        {
            try
            {
                if (_rdpSettings.GatewayMode is null or EGatewayMode.DoNotUseGateway)
                {
                    return ConnectionInfoTranslate("Not in use", "Not in use");
                }

                return _rdpSettings.GatewayLogonMethod switch
                {
                    EGatewayLogonMethod.Password => ConnectionInfoTranslate("Password", "Password"),
                    EGatewayLogonMethod.SmartCard => ConnectionInfoTranslate("server_editor_gateway_logon_method_smart_card", "Smart card"),
                    _ => ConnectionInfoUnavailable,
                };
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read RDP gateway logon method: {e.Message}");
                return ConnectionInfoUnavailable;
            }
        }

        private string GetRdpRemoteComputer()
        {
            try
            {
                var server = _rdpClient?.Server;
                if (!string.IsNullOrWhiteSpace(server))
                {
                    return server;
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read RDP remote computer: {e.Message}");
            }

            try
            {
                return ConnectionInfoValueOrUnavailable(_rdpSettings.Address);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read RDP configured remote computer: {e.Message}");
                return ConnectionInfoUnavailable;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.Dispose();
            base.OnClosed?.Invoke(base.ConnectionId);
        }
        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }

        public override void ReConn()
        {
            Debug.Assert(_rdpClient != null);
            if (Status != ProtocolHostStatus.Connected
                && Status != ProtocolHostStatus.Disconnected)
            {
                SimpleLogHelper.Warning($"RDP Host: Call ReConn, but current status = " + Status);
                return;
            }
            else
            {
                SimpleLogHelper.Warning($"RDP Host: Call ReConn");
            }
            Status = ProtocolHostStatus.WaitingForReconnect;
            Execute.OnUIThreadSync(() =>
            {
                RdpHost.Visibility = System.Windows.Visibility.Collapsed;
                GridLoading.Visibility = System.Windows.Visibility.Visible;
            });
            RdpClientDispose();


            var t = Task.Factory.StartNew(async () =>
            {
                // check if it needs to auto switch address
                var isAutoAlternateAddressSwitching = _rdpSettings.IsAutoAlternateAddressSwitching == true
                                                      // if none of the alternate credential has host or port，then disabled `AutoAlternateAddressSwitching`
                                                      && _rdpSettings.AlternateCredentials.Any(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port));
                if (isAutoAlternateAddressSwitching)
                {
                    var c = await SessionControlService.GetCredential(_rdpSettings);
                    if (c != null)
                    {
                        _rdpSettings.SetCredential(c, true);
                        _rdpSettings.DisplayName = c.Name;
                    }
                }

                Status = ProtocolHostStatus.NotInit;

                await Execute.OnUIThreadAsync(() =>
                {
                    int w = 0;
                    int h = 0;
                    if (ParentWindow is TabWindowView tab)
                    {
                        var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(this._rdpSettings.ColorHex) == true);
                        w = (int)size.Width;
                        h = (int)size.Height;
                    }
                    InitRdp(w, h, true);
                    Conn();
                });
            });
        }


        private void ParentWindowSetToWindow()
        {
            // make sure ParentWindow is FullScreen Window
            if (ParentWindow is not FullScreenWindowView)
            {
                return;
            }

            if (ParentWindow is FullScreenWindowView { IsLoaded: false })
            {
                return;
            }

            ParentWindow.Topmost = false;
            ParentWindow.ResizeMode = ResizeMode.CanResize;
            ParentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.Width = FullScreenWindowView.DESIGN_WIDTH / (_primaryScaleFactor / 100.0);
            ParentWindow.Height = FullScreenWindowView.DESIGN_HEIGHT / (_primaryScaleFactor / 100.0);
            var screenEx = ScreenInfoEx.GetCurrentScreen(this.ParentWindow);
            ParentWindow.Top = screenEx.VirtualWorkingAreaCenter.Y - ParentWindow.Height / 2;
            ParentWindow.Left = screenEx.VirtualWorkingAreaCenter.X - ParentWindow.Width / 2;
        }



        #region event handler

        private int _retryCount = 0;
        private const int MAX_RETRY_COUNT = 5;
        private void OnRdpClientDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            SimpleLogHelper.Debug("RDP Host: RdpOnDisconnected");
            StopRdpNetworkMetricsCollection();
            // Do not allow late COM callbacks from a disconnected control to
            // repopulate the snapshot. Reconnect creates a fresh control and
            // subscribes a fresh handler.
            Interlocked.Increment(ref _rdpNetworkMetricsGeneration);
            DetachRdpNetworkStatusHandler(_rdpClient);

            lock (this)
            {
                if (_rdpClient == null) return;

                Status = ProtocolHostStatus.Disconnected;
                ParentWindowResize_StopWatch();

                // https://learn.microsoft.com/windows/win32/termserv/imstscaxevents-ondisconnected
                const int disconnectReasonLocalNotError = 1;  // Local disconnection. This is not an error code. Note that this will also be returned if the user cancels the RdpClient's attempt to reconnect.
                const int disconnectReasonRemoteByUser = 2;   // Remote disconnection by user (via 'Disconnect', 'Signout', 'Shutdown' and 'Reboot'). This is not an error code.
                const int disconnectReasonByServer = 3;       // Remote disconnection by server. This will be returned when switching to another connection. This is not an error code.

                string reason = _rdpClient.GetErrorDescription((uint)e.discReason, (uint)_rdpClient.ExtendedDisconnectReason) ?? "";
                ExtendedDisconnectReasonCode excode = _rdpClient.ExtendedDisconnectReason;
                SimpleLogHelper.Debug($"RDP({_rdpSettings.DisplayName}) Disconnected with code {e.discReason}({reason}) ex:{excode}");

                switch (e.discReason)
                {
                    case disconnectReasonRemoteByUser:
                        // we can close the window immediately in the case of disconnectReasonRemoteByUser. No further case analysis is needed.
                        RdpClientDispose();
                        base.OnClosed?.Invoke(base.ConnectionId);
                        break;

                    case disconnectReasonByServer:
                        // https://learn.microsoft.com/en-us/windows/win32/termserv/extendeddisconnectreasoncode
                        if (   _rdpClient.ExtendedDisconnectReason == ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff              // log out from win2012 by user
                            || _rdpClient.ExtendedDisconnectReason == ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect          // An application initiated the disconnection.
                            || _rdpClient.ExtendedDisconnectReason == ExtendedDisconnectReasonCode.exDiscReasonNoInfo                          // log out from win2008 by user
                            || _rdpClient.ExtendedDisconnectReason == ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser                    // log out from win10 by user
                            || _rdpClient.ExtendedDisconnectReason == ExtendedDisconnectReasonCode.exDiscReasonRpcInitiatedDisconnectByUser    // log out from win2016 by user
                            )
                        {
                            // Terminate the session without notifying the user. Because the disconnection is initiated by the user.
                            RdpClientDispose();
                            base.OnClosed?.Invoke(base.ConnectionId);
                            break;
                        }
                        else
                        {
                            // show the message to user, and let user decide what to do next.
                            // potential reasons: 
                            // exDiscReasonServerIdleTimeout: user leave and no input for a long time without disconnect or log off, and server set a timeout to drop the session.
                            // exDiscReasonReplacedByOtherConnection: another user (maybe the same user) logon to the server, and the server drop this session.
                            RdpHost.Visibility = Visibility.Collapsed;
                            GridMessageBox.Visibility = Visibility.Visible;
                            TbMessageTitle.Visibility = Visibility.Collapsed;
                            BtnReconn.Visibility = Visibility.Visible;
                            TbMessage.Text = reason;
                            ParentWindowSetToWindow();
                            this.ParentWindow?.FlashIfNotActive();
                            break;
                        }

                    case disconnectReasonLocalNotError:
                        // Maybe the user has cancelled the RdpClient's attempt to reconnect.
                        // In this case, we will attempt to reconnect in order to switch to an alternate host.
                    default:
                        // A communication breakdown occurred by unplugging, invalidating, going to sleep or crashing, etc.
                        SimpleLogHelper.Warning($"RDP({_rdpSettings.DisplayName}) exit with error code {e.discReason}({reason}) ex:{excode}");
                        // We try reconnecting.
                        RdpHost.Visibility = Visibility.Collapsed;
                        GridMessageBox.Visibility = Visibility.Visible;
                        if (_flagHasEverConnected // a rdp session with never successful connection should not retry (in the case error code = 4 ex:exDiscReasonNoInfo)
                            && _retryCount < MAX_RETRY_COUNT)
                        {
                            // Continue to retry.
                            ++_retryCount;
                            TbMessageTitle.Text = IoC.Translate("host_reconecting_info") + $"({_retryCount}/{MAX_RETRY_COUNT})";
                            TbMessageTitle.Visibility = Visibility.Visible;
                            TbMessage.Visibility = Visibility.Collapsed;
                            BtnReconn.Visibility = Visibility.Collapsed;
                            this.ReConn();
                        }
                        else
                        {
                            // The number of retries has reached its limit. Display an error.
                            _retryCount = 0;  // Reset for next time.
                            TbMessageTitle.Visibility = Visibility.Collapsed;
                            BtnReconn.Visibility = Visibility.Visible;
                            TbMessage.Text = reason;
                            ParentWindowSetToWindow();
                        }
                        this.ParentWindow?.FlashIfNotActive();
                        break;
                }
            }
        }

        private void OnRdpClientConnected(object? sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnOnConnected");
            this.ParentWindow?.FlashIfNotActive();

            StartRdpNetworkMetricsCollection();

            _lastLoginTime = DateTime.Now;
            _loginResizeTimer.Start();

            _flagHasConnected = true;
            _flagHasEverConnected = true;
            _retryCount = 0;
            Execute.OnUIThread(() =>
            {
                RdpHost.Visibility = Visibility.Visible;
                GridLoading.Visibility = Visibility.Collapsed;
                GridMessageBox.Visibility = Visibility.Collapsed;

                // if parent is FullScreenWindowView, go to full screen.
                if (ParentWindow is FullScreenWindowView)
                {
                    SimpleLogHelper.Debug("RDP Host: ReConn with full screen");
                    GoFullScreen();
                }
            });
        }

        private void OnRdpClientNetworkStatusChanged(
            AxMsRdpClient10NotSafeForScriptingEx client,
            long generation,
            uint qualityLevel,
            int bandwidthKbps,
            int roundTripTimeMilliseconds)
        {
            try
            {
                if (!_rdpNetworkMetricsCollectionEnabled
                    || !ReferenceEquals(_rdpNetworkMetricsClient, client)
                    || generation != Volatile.Read(ref _rdpNetworkMetricsGeneration))
                {
                    return;
                }

                _rdpNetworkMetrics.RecordNetworkStatus(
                    qualityLevel,
                    bandwidthKbps,
                    roundTripTimeMilliseconds);
            }
            catch (Exception e)
            {
                // COM event handlers must not let a managed exception escape back
                // into the RDP ActiveX event dispatcher.
                SimpleLogHelper.Debug($"Unable to record RDP network status: {e.Message}");
            }
        }

        private void OnRdpClientLoginComplete(object? sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  OnRdpClientLoginComplete");

            OnCanResizeNowChanged?.Invoke();
            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;
            ParentWindowResize_StartWatch();
            //_resizeEndTimer?.Start();
            //Task.Factory.StartNew(() =>
            //{
            //    Thread.Sleep(5000);
            //    _resizeEndTimer?.Stop();
            //});
        }


        private void OnGoToFullScreenRequested()
        {
            Debug.Assert(_rdpClient != null);
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            switch (ParentWindow)
            {
                case null:
                    return;
                case TabWindowView:
                {
                    // full-all-screen session switch to TabWindow, and click "Reconn" button, will entry this case.
                    _rdpClient!.FullScreen = false;
                    LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
                    return;
                }
            }


            var screenSize = this.GetScreenSizeIfRdpIsFullScreen();

            double width = screenSize.Width / (_primaryScaleFactor / 100.0);
            double height = screenSize.Height / (_primaryScaleFactor / 100.0);
            int ceilingWidth = (int)Math.Ceiling(width);
            int ceilingHeight = (int)Math.Ceiling(height);
            ParentWindow.Dispatcher.Invoke(() =>
            {
                // ! do not remove
                ParentWindow.WindowState = WindowState.Normal;
                ParentWindow.WindowStyle = WindowStyle.None;
                ParentWindow.ResizeMode = ResizeMode.NoResize;

                ParentWindow.Width = ceilingWidth;
                ParentWindow.Height = ceilingHeight;
                ParentWindow.Left = screenSize.Left / (_primaryScaleFactor / 100.0);
                ParentWindow.Top = screenSize.Top / (_primaryScaleFactor / 100.0);
            });

            SimpleLogHelper.Debug($"RDP to FullScreen resize ParentWindow to : W = {ceilingWidth}({width}), H = {ceilingHeight}({height}), while screen size is {screenSize.Width} × {screenSize.Height}, ScaleFactor = {_primaryScaleFactor}");

            // WARNING!: EnableFullAllScreens do not need a SetRdpResolution
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                switch (_rdpSettings.RdpWindowResizeMode)
                {
                    case null:
                    case ERdpWindowResizeMode.AutoResize:
                    case ERdpWindowResizeMode.FixedFullScreen:
                    case ERdpWindowResizeMode.StretchFullScreen:
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height, true);
                        break;
                    case ERdpWindowResizeMode.Stretch:
                    case ERdpWindowResizeMode.Fixed:
                        SetRdpResolution((uint)(_rdpSettings.RdpWidth ?? 800), (uint)(_rdpSettings.RdpHeight ?? 600), true);
                        break;
                    default:
                        UnifyTracing.Error(new ArgumentOutOfRangeException($"{_rdpSettings.RdpWindowResizeMode} is not processed!"));
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height, true);
                        break;
                }
            }
        }

        private void OnConnectionBarRestoreWindowCall()
        {
            // make sure ParentWindow is FullScreen Window
            if (ParentWindow is not FullScreenWindowView)
            {
                return;
            }

            // !do not remove
            ParentWindowSetToWindow();
            LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
            base.OnFullScreen2Window?.Invoke(base.ConnectionId);
        }

        #endregion event handler
    }
}
