using System;
using _1RM.Service;
using Shawn.Utils.Wpf;

namespace _1RM.View
{
    public partial class MainWindowViewModel
    {
        private FluentPreferences FluentOptions =>
            ConfigurationService.Theme.Fluent ??= new FluentPreferences();

        public bool IsFluentPreview
        {
            get => FluentOptions.Enabled;
            set
            {
                if (FluentOptions.Enabled == value) return;
                FluentOptions.Enabled = value;
                RaisePropertyChanged();
                CurrentView = (EnumServerViewStatus)Enum.Parse(typeof(EnumServerViewStatus),
                    FluentOptions.ResolveView(ConfigurationService.General.ServerViewStatus.ToString()));
                ConfigurationService.Save();
                IoC.Get<FluentAppearanceService>().NotifyChanged();
            }
        }

        public string FluentTheme
        {
            get => FluentPreferences.NormalizeTheme(FluentOptions.Theme);
            set
            {
                FluentOptions.Theme = FluentPreferences.NormalizeTheme(value);
                RaisePropertyChanged();
                ConfigurationService.Save();
                IoC.Get<FluentAppearanceService>().NotifyChanged();
            }
        }

        private RelayCommand? _cmdFluentTheme;
        public RelayCommand CmdFluentTheme => _cmdFluentTheme ??= new RelayCommand(o => FluentTheme = o?.ToString() ?? "System");
        private RelayCommand? _cmdClassicAppearance;
        public RelayCommand CmdClassicAppearance => _cmdClassicAppearance ??= new RelayCommand(_ => IsFluentPreview = false);
    }
}
