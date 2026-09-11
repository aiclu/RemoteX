using System;
using System.Collections.Generic;
using System.Windows;
using _1RM.View.Utils.MaskAndPop;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Host
{
    public sealed class ConnectionInfoViewModel : PopupBase
    {
        private string _copyStatus = "";

        public ConnectionInfoViewModel(ConnectionInfoSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            DisplayName = snapshot.WindowTitle;
        }

        public ConnectionInfoSnapshot Snapshot { get; }
        public string WindowTitle => Snapshot.WindowTitle;
        public string Summary => Snapshot.Summary;
        public IReadOnlyList<ConnectionInfoSection> Sections => Snapshot.Sections;
        public string CopyText => Snapshot.CopyText;

        public string CopyStatus
        {
            get => _copyStatus;
            private set => SetAndNotifyIfChanged(ref _copyStatus, value);
        }

        private RelayCommand? _cmdCopy;
        public RelayCommand CmdCopy
        {
            get
            {
                return _cmdCopy ??= new RelayCommand(_ =>
                {
                    try
                    {
                        Clipboard.SetText(CopyText);
                        CopyStatus = IoC.Translate("Copied");
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                        CopyStatus = IoC.Translate("Unable to copy");
                    }
                });
            }
        }

        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand(_ => RequestClose(true));
            }
        }
    }
}
