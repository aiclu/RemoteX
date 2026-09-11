using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
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

        private void ShowConnectionInfo()
        {
            try
            {
                // Collect the diagnostic data before showing the modal window so the
                // dialog remains a stable snapshot and never interferes with a session.
                var viewModel = new ConnectionInfoViewModel(CreateConnectionInfoSnapshot());
                viewModel.ShowDialog(ParentWindow?.DataContext as IViewAware);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public virtual ConnectionInfoSnapshot CreateConnectionInfoSnapshot()
        {
            var capturedAtUtc = DateTimeOffset.UtcNow;
            return new ConnectionInfoSnapshot(
                GetConnectionInfoWindowTitle(),
                GetConnectionInfoSummary(),
                capturedAtUtc,
                new[]
                {
                    BuildSessionDetailsSection(capturedAtUtc),
                    BuildClientDetailsSection(),
                    BuildNetworkDetailsSection(),
                    BuildRemoteComputerDetailsSection(),
                });
        }

        protected virtual string GetConnectionInfoWindowTitle()
        {
            var displayName = string.IsNullOrWhiteSpace(ProtocolServer.DisplayName)
                ? ProtocolServer.ProtocolDisplayName
                : ProtocolServer.DisplayName;
            return $"{IoC.Translate("Connection information")} - {displayName}";
        }

        protected virtual string GetConnectionInfoSummary()
        {
            return $"{IoC.Translate("Connection status")}: {GetConnectionInfoStatus()}";
        }

        protected virtual ConnectionInfoSection BuildSessionDetailsSection(DateTimeOffset capturedAtUtc)
        {
            var rows = new List<ConnectionInfoRow>
            {
                new(IoC.Translate("Time (UTC)"), capturedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new(IoC.Translate("Activity ID"), ConnectionInfoUnavailable),
                new(IoC.Translate("Protocol"), ConnectionInfoValueOrUnavailable(ProtocolServer.ProtocolDisplayName)),
                new(IoC.Translate("Connection status"), GetConnectionInfoStatus()),
                new(IoC.Translate("Session ID"), ConnectionInfoValueOrUnavailable(ProtocolServer.SessionId)),
            };

            if (ProtocolServer is ProtocolBaseWithAddressPort addressPort)
            {
                rows.Add(new ConnectionInfoRow(IoC.Translate("Hostname"), ConnectionInfoValueOrUnavailable(addressPort.Address)));
                rows.Add(new ConnectionInfoRow(IoC.Translate("Port"), ConnectionInfoValueOrUnavailable(addressPort.Port)));
            }

            if (ProtocolServer is ProtocolBaseWithAddressPortUserPwd userPwd)
            {
                rows.Add(new ConnectionInfoRow(IoC.Translate("User"), ConnectionInfoValueOrUnavailable(userPwd.UserName)));
            }

            return new ConnectionInfoSection(IoC.Translate("Session details"), rows);
        }

        protected virtual ConnectionInfoSection BuildClientDetailsSection()
        {
            return new ConnectionInfoSection(
                IoC.Translate("Client details"),
                new[]
                {
                    new ConnectionInfoRow(
                        IoC.Translate("Client version"),
                        $"{Assert.APP_DISPLAY_NAME} {AppVersion.Version}"),
                    new ConnectionInfoRow(IoC.Translate("Local OS"), GetLocalOsDescription()),
                });
        }

        protected virtual ConnectionInfoSection BuildNetworkDetailsSection()
        {
            return new ConnectionInfoSection(
                IoC.Translate("Network details"),
                new[]
                {
                    new ConnectionInfoRow(IoC.Translate("Transport protocol"), ConnectionInfoUnavailable),
                    new ConnectionInfoRow(IoC.Translate("Round-trip time"), ConnectionInfoUnavailable),
                    new ConnectionInfoRow(IoC.Translate("Available bandwidth"), ConnectionInfoUnavailable),
                    new ConnectionInfoRow(IoC.Translate("Frame rate"), ConnectionInfoUnavailable),
                });
        }

        protected virtual ConnectionInfoSection BuildRemoteComputerDetailsSection()
        {
            return new ConnectionInfoSection(
                IoC.Translate("Remote computer details"),
                new[]
                {
                    new ConnectionInfoRow(IoC.Translate("Remote session type"), ConnectionInfoValueOrUnavailable(ProtocolServer.ProtocolDisplayName)),
                    new ConnectionInfoRow(IoC.Translate("Network name"), ConnectionInfoUnavailable),
                    new ConnectionInfoRow(IoC.Translate("Remote computer"), GetConnectionInfoRemoteComputer()),
                });
        }

        protected virtual string GetConnectionInfoRemoteComputer()
        {
            return ProtocolServer is ProtocolBaseWithAddressPort addressPort
                ? ConnectionInfoValueOrUnavailable(addressPort.Address)
                : ConnectionInfoUnavailable;
        }

        protected string GetConnectionInfoStatus()
        {
            return Status switch
            {
                ProtocolHostStatus.NotInit => IoC.Translate("Not initialized"),
                ProtocolHostStatus.Initializing => IoC.Translate("Initializing"),
                ProtocolHostStatus.Initialized => IoC.Translate("Initialized"),
                ProtocolHostStatus.Connecting => IoC.Translate("Connecting"),
                ProtocolHostStatus.WaitingForReconnect => IoC.Translate("Waiting for reconnect"),
                ProtocolHostStatus.Connected => IoC.Translate("Connected"),
                ProtocolHostStatus.Disconnected => IoC.Translate("Disconnected"),
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

        protected static string ConnectionInfoUnavailable => IoC.Translate("Unavailable");

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
