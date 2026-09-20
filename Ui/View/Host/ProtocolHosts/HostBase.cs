using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.View;
using _1RM.View.Host;
using _1RM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    public enum ProtocolHostStatus
    {
        NotInit,
        Initializing,
        Initialized,
        Connecting,
        WaitingForReconnect,
        Connected,
        Disconnected,
    }

    public enum ProtocolHostType
    {
        Native,
        Integrate
    }

    public abstract class HostBase : UserControl
    {
        public ProtocolBase ProtocolServer { get; }

        private WindowBase? _parentWindow;
        public WindowBase? ParentWindow => _parentWindow;

        public virtual void SetParentWindow(WindowBase? value)
        {
            if (_parentWindow == value) return;
            _parentWindow = value;
            ParentWindowHandle = IntPtr.Zero;

            if (null == value) return;
            var window = Window.GetWindow(value);
            if (window != null)
            {
                var wih = new WindowInteropHelper(window);
                ParentWindowHandle = wih.Handle;
            }
        }

        public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;

        /// <summary>
        /// a flag to id if ProtocolServer can open session successfully.
        /// </summary>
        public bool HasConnected = false;

        private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;
        public ProtocolHostStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    if (value == ProtocolHostStatus.Connected)
                        HasConnected = true;

                    SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
                    _status = value;
                    OnCanResizeNowChanged?.Invoke();
                }
            }
        }

        protected HostBase(ProtocolBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "View connection information");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((_) => ShowConnectionInfo())
                });
            }

            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }

            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); }),
                });
            }

            MenuItems.Add(new System.Windows.Controls.Separator());

            {
	            var subMenu = new System.Windows.Controls.MenuItem()
	            {
		            Header = IoC.Translate("Custom"),
	            };


	            {
		            var cb = new CheckBox
		            {
			            Content = IoC.Translate("Show XXX button", IoC.Translate("Reconnect") ?? ""),
			            IsHitTestVisible = false,
		            };

		            var binding = new Binding("TabHeaderShowReConnectButton")
		            {
			            Source = SettingsPage,
			            Mode = BindingMode.TwoWay,
			            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
		            };
		            cb.SetBinding(CheckBox.IsCheckedProperty, binding);
		            subMenu.Items.Add(new System.Windows.Controls.MenuItem()
		            {
			            Header = cb,
			            Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowReConnectButton = !SettingsPage.TabHeaderShowReConnectButton; }),
		            });
				}

	            {
		            var cb = new CheckBox
		            {
			            Content = IoC.Translate("Show XXX button", IoC.Translate("Close") ?? ""),
			            IsHitTestVisible = false,
		            };

		            var binding = new Binding("TabHeaderShowCloseButton")
		            {
			            Source = SettingsPage,
			            Mode = BindingMode.TwoWay,
			            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
		            };
		            cb.SetBinding(CheckBox.IsCheckedProperty, binding);
		            subMenu.Items.Add(new System.Windows.Controls.MenuItem()
		            {
			            Header = cb,
			            Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowCloseButton = !SettingsPage.TabHeaderShowCloseButton; }),
		            });
				}


	            {
		            var cb = new CheckBox
		            {
			            Content = IoC.Translate("Show XXX button", "Icon"),
			            IsHitTestVisible = false,
		            };

		            var binding = new Binding("TabHeaderShowIconButton")
		            {
			            Source = SettingsPage,
			            Mode = BindingMode.TwoWay,
			            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
		            };
		            cb.SetBinding(CheckBox.IsCheckedProperty, binding);
		            subMenu.Items.Add(new System.Windows.Controls.MenuItem()
		            {
			            Header = cb,
			            Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowIconButton = !SettingsPage.TabHeaderShowIconButton; }),
		            });
	            }

				MenuItems.Add(subMenu);
			}

			MenuItems.Add(new Separator());

			var actions = protocolServer.GetActions(true);
            foreach (var action in actions)
            {
                var tb = new TextBlock()
                {
                    Text = action.ActionName,
                };
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { action.Run(); }),
                });
            }
        }

        protected virtual bool TryShowNativeConnectionInfo() => false;

        private bool _showingConnectionInfo;

        private void ShowConnectionInfo()
        {
            try
            {
                Execute.OnUIThreadSync(() =>
                {
                    if (_showingConnectionInfo)
                        return;
                    _showingConnectionInfo = true;
                    try
                    {
                        ConnectionInfoPresentation.Show(TryShowNativeConnectionInfo, ShowConnectionInfoSnapshot,
                            e => SimpleLogHelper.Warning($"Native connection information failed: {e.GetType().Name}, HRESULT=0x{e.HResult:X8}"));
                    }
                    finally
                    {
                        _showingConnectionInfo = false;
                    }
                });
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                ShowConnectionInfoError();
            }
        }

        private void ShowConnectionInfoSnapshot()
        {
            // Collect the diagnostic data before showing the modal window so the
            // dialog remains a stable snapshot and never interferes with a session.
            var snapshot = CreateConnectionInfoSnapshot();
            var viewModel = new ConnectionInfoViewModel(snapshot);
            var owner = ParentWindow?.DataContext as IViewAware
                ?? IoC.TryGet<MainWindowViewModel>();
            viewModel.ShowDialog(owner);
        }

        private void ShowConnectionInfoError()
        {
            // A diagnostic command must never take down the whole application.  In
            // particular, WPF window initialization errors are otherwise routed to
            // Bootstrapper.OnUnhandledException, which intentionally closes RemoteX.
            try
            {
                Execute.OnUIThreadSync(() =>
                {
                    var title = ConnectionInfoTranslate("Connection information", "Connection information");
                    var message = ConnectionInfoTranslate(
                        "Unable to show connection information",
                        "Unable to show connection information. Details were written to the log.");
                    System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception fallbackException)
            {
                SimpleLogHelper.Error(fallbackException);
            }
        }

        public virtual ConnectionInfoSnapshot CreateConnectionInfoSnapshot()
        {
            var capturedAtUtc = DateTimeOffset.UtcNow;
            return new ConnectionInfoSnapshot(
                SafeConnectionInfoText("window title", GetConnectionInfoWindowTitle, ConnectionInfoTranslate("Connection information", "Connection information")),
                SafeConnectionInfoText("summary", GetConnectionInfoSummary, ConnectionInfoTranslate("Unavailable", "Unavailable")),
                capturedAtUtc,
                new[]
                {
                    SafeConnectionInfoSection("Session details", () => BuildSessionDetailsSection(capturedAtUtc)),
                    SafeConnectionInfoSection("Client details", BuildClientDetailsSection),
                    SafeConnectionInfoSection("Network details", BuildNetworkDetailsSection),
                    SafeConnectionInfoSection("Remote computer details", BuildRemoteComputerDetailsSection),
                });
        }

        protected virtual string GetConnectionInfoWindowTitle()
        {
            var displayName = SafeConnectionInfoValue("display name", () => ProtocolServer.DisplayName);
            if (displayName == ConnectionInfoUnavailable)
                displayName = SafeConnectionInfoValue("protocol name", () => ProtocolServer.ProtocolDisplayName);
            return $"{ConnectionInfoTranslate("Connection information", "Connection information")} - {displayName}";
        }

        protected virtual string GetConnectionInfoSummary()
        {
            return $"{ConnectionInfoTranslate("Connection status", "Connection status")}: {GetConnectionInfoStatus()}";
        }

        protected virtual ConnectionInfoSection BuildSessionDetailsSection(DateTimeOffset capturedAtUtc)
        {
            var rows = new List<ConnectionInfoRow>
            {
                CreateConnectionInfoRow("Time (UTC)", () => capturedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                CreateConnectionInfoRow("Activity ID", () => null),
                CreateConnectionInfoRow("Protocol", () => ProtocolServer.ProtocolDisplayName),
                CreateConnectionInfoRow("Connection status", GetConnectionInfoStatus),
                CreateConnectionInfoRow("Session ID", () => ProtocolServer.SessionId),
            };

            if (ProtocolServer is ProtocolBaseWithAddressPort addressPort)
            {
                rows.Add(CreateConnectionInfoRow("Hostname", () => addressPort.Address));
                rows.Add(CreateConnectionInfoRow("Port", () => addressPort.Port));
            }

            if (ProtocolServer is ProtocolBaseWithAddressPortUserPwd userPwd)
            {
                rows.Add(CreateConnectionInfoRow("User", () => userPwd.UserName));
            }

            return new ConnectionInfoSection(ConnectionInfoTranslate("Session details", "Session details"), rows);
        }

        protected virtual ConnectionInfoSection BuildClientDetailsSection()
        {
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Client details", "Client details"),
                new[]
                {
                    CreateConnectionInfoRow("Client version", () => $"{Assert.APP_DISPLAY_NAME} {AppVersion.Version}"),
                    CreateConnectionInfoRow("Local OS", GetLocalOsDescription),
                });
        }

        protected virtual ConnectionInfoSection BuildNetworkDetailsSection()
        {
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Network details", "Network details"),
                new[]
                {
                    CreateConnectionInfoRow("Transport protocol", () => null),
                    CreateConnectionInfoRow("Round-trip time", () => null),
                    CreateConnectionInfoRow("Available bandwidth", () => null),
                    CreateConnectionInfoRow("Frame rate", () => null),
                });
        }

        protected virtual ConnectionInfoSection BuildRemoteComputerDetailsSection()
        {
            return new ConnectionInfoSection(
                ConnectionInfoTranslate("Remote computer details", "Remote computer details"),
                new[]
                {
                    CreateConnectionInfoRow("Remote session type", () => ProtocolServer.ProtocolDisplayName),
                    CreateConnectionInfoRow("Network name", () => null),
                    CreateConnectionInfoRow("Remote computer", GetConnectionInfoRemoteComputer),
                });
        }

        protected virtual string GetConnectionInfoRemoteComputer()
        {
            return ProtocolServer is ProtocolBaseWithAddressPort addressPort
                ? SafeConnectionInfoValue("remote computer", () => addressPort.Address)
                : ConnectionInfoUnavailable;
        }

        protected string GetConnectionInfoStatus()
        {
            return Status switch
            {
                ProtocolHostStatus.NotInit => ConnectionInfoTranslate("Not initialized", "Not initialized"),
                ProtocolHostStatus.Initializing => ConnectionInfoTranslate("Initializing", "Initializing"),
                ProtocolHostStatus.Initialized => ConnectionInfoTranslate("Initialized", "Initialized"),
                ProtocolHostStatus.Connecting => ConnectionInfoTranslate("Connecting", "Connecting"),
                ProtocolHostStatus.WaitingForReconnect => ConnectionInfoTranslate("Waiting for reconnect", "Waiting for reconnect"),
                ProtocolHostStatus.Connected => ConnectionInfoTranslate("Connected", "Connected"),
                ProtocolHostStatus.Disconnected => ConnectionInfoTranslate("Disconnected", "Disconnected"),
                _ => ConnectionInfoUnavailable,
            };
        }

        protected static string GetLocalOsDescription()
        {
            var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            return $"{Environment.OSVersion.VersionString} ({architecture})";
        }

        protected static string ConnectionInfoValueOrUnavailable(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? ConnectionInfoUnavailable : text;
        }

        protected static string ConnectionInfoUnavailable => ConnectionInfoTranslate("Unavailable", "Unavailable");

        protected static string ConnectionInfoTranslate(string key, string fallback)
        {
            try
            {
                var translated = IoC.Translate(key);
                return string.IsNullOrWhiteSpace(translated) ? fallback : translated;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to translate connection-info key '{key}': {e.Message}");
                return fallback;
            }
        }

        protected ConnectionInfoRow CreateConnectionInfoRow(string labelKey, Func<object?> valueFactory)
        {
            return new ConnectionInfoRow(
                ConnectionInfoTranslate(labelKey, labelKey),
                SafeConnectionInfoValue(labelKey, valueFactory));
        }

        private static string SafeConnectionInfoText(string fieldName, Func<string> valueFactory, string fallback)
        {
            try
            {
                var value = valueFactory();
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error($"Unable to read connection-info {fieldName}: {e}");
                return fallback;
            }
        }

        private static ConnectionInfoSection SafeConnectionInfoSection(string sectionKey, Func<ConnectionInfoSection> sectionFactory)
        {
            try
            {
                var section = sectionFactory();
                if (section != null)
                    return section;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error($"Unable to build connection-info section '{sectionKey}': {e}");
            }

            return new ConnectionInfoSection(
                ConnectionInfoTranslate(sectionKey, sectionKey),
                new[]
                {
                    new ConnectionInfoRow(ConnectionInfoTranslate("Unavailable", "Unavailable"), ConnectionInfoUnavailable),
                });
        }

        private static string SafeConnectionInfoValue(string fieldName, Func<object?> valueFactory)
        {
            try
            {
                return ConnectionInfoValueOrUnavailable(valueFactory());
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to read connection-info field '{fieldName}': {e.Message}");
                return ConnectionInfoUnavailable;
            }
        }

        public string ConnectionId
        {
            get
            {
                if (ProtocolServer.IsOnlyOneInstance())
                {
                    return ProtocolServer.BuildConnectionId();
                }
                else
                {
                    return ProtocolServer.BuildConnectionId() + "_" + this.GetHashCode();
                }
            }
        }

        public SettingsPageViewModel SettingsPage => IoC.Get<SettingsPageViewModel>();

        public bool CanFullScreen { get; protected set; }

        /// <summary>
        /// special menu for tab
        /// </summary>
        public List<System.Windows.Controls.Control> MenuItems { get; set; } = new List<System.Windows.Controls.Control>();

        /// <summary>
        /// since resizing when rdp is connecting would not tiger the rdp size change event
        /// then I let rdp host return false when rdp is on connecting to prevent TabWindow resize or maximize.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanResizeNow()
        {
            return true;
        }

        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; } = null;

        public virtual void ToggleAutoResize(bool isEnable)
        {
        }

        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public virtual void Close()
        {
            this.OnClosed?.Invoke(ConnectionId);
        }

        protected virtual void GoFullScreen()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// call to focus the embedded host window
        /// </summary>
        public virtual void FocusOnMe()
        {
            // do nothing
        }

        public abstract ProtocolHostType GetProtocolHostType();

        /// <summary>
        /// if it is a Integrate host, then return process's hwnd.
        /// </summary>
        /// <returns></returns>
        public abstract IntPtr GetHostHwnd();

        public Action<string>? OnClosed { get; set; } = null;
        public Action<string>? OnFullScreen2Window { get; set; } = null;
    }
}
