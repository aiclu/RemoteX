using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils;
using _1RM.View.Utils.MaskAndPop;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.View
{
    public class AboutPageViewModel : PopupBase
    {
        private Timer? _checkUpdateTimer;
        private VersionHelper? _checker;

        public AboutPageViewModel()
        {
            StartVersionCheckTimer();
        }

        public void StartVersionCheckTimer()
        {
            if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                return;

            if (_checker == null)
            {
                _checker = new VersionHelper(AppVersion.VersionData,
                    AppVersion.UpdateCheckUrls,
                    AppVersion.UpdatePublishUrls,
                    customCheckMethod: CustomCheckMethod);
                _checker.OnNewVersionRelease += OnNewVersionRelease;
            }
            if (_checkUpdateTimer == null)
            {
                _checkUpdateTimer = new Timer()
                {
                    Interval = 1000 * 60 * 60,
                    AutoReset = true,
                };
                _checkUpdateTimer.Elapsed += (sender, args) =>
                {
                    if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                    {
                        _checkUpdateTimer.Stop(); // Stop timer if checking is disabled
                        return;
                    }
                    _checker.CheckUpdateAsync();
                };
            }
            _checker.CheckUpdateAsync();
            _checkUpdateTimer.Stop();
            _checkUpdateTimer.Start();
        }

        private static VersionHelper.CheckUpdateResult CustomCheckMethod(string html, string publishUrl, VersionHelper.Version currentVersion, VersionHelper.Version? ignoreVersion)
        {
            var ret = VersionHelper.DefaultCheckMethod(html, publishUrl, currentVersion, ignoreVersion);
            if (ret.NewerPublished)
                return ret;

            var patterns = new List<string>()
            {
                // GitHub API /releases/latest -> {"tag_name":"v1.0.4",...} (lowercased by HttpHelper).
                @"tag_name[^0-9]*([\d.]+)",
                @".?remotex-([\d.]+)",
                @".?latest\sversion:\s*([\d|.]*)",
            };
            foreach (var pattern in patterns)
            {
                var mc = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                if (mc.Count <= 0) continue;
                var versionString = mc[0].Groups[1].Value;
                var releasedVersion = VersionHelper.Version.FromString(versionString);
                if (ignoreVersion is not null)
                {
                    if (releasedVersion <= ignoreVersion)
                    {
                        return VersionHelper.CheckUpdateResult.False();
                    }
                }
                if (releasedVersion > currentVersion)
                    return new VersionHelper.CheckUpdateResult(true, versionString, publishUrl, versionString.FirstOrDefault() == '!' || versionString.LastOrDefault() == '!');
            }
            return VersionHelper.CheckUpdateResult.False();
        }

        ~AboutPageViewModel()
        {
            _checkUpdateTimer?.Stop();
            _checkUpdateTimer?.Dispose();
        }

        public string CurrentVersion => AppVersion.Version;
        public string CurrentVersionDate => AppVersion.BuildDate.IndexOf("+", StringComparison.Ordinal) > 0 ? AppVersion.BuildDate.Substring(0, AppVersion.BuildDate.LastIndexOf("+", StringComparison.Ordinal)) : AppVersion.BuildDate;


        private string _newVersion = "";
        public string NewVersion
        {
            get => _newVersion;
            set => SetAndNotifyIfChanged(ref _newVersion, value);
        }

        private string _newVersionUrl = "";

        public string NewVersionUrl
        {
            get => _newVersionUrl;
            set => SetAndNotifyIfChanged(ref _newVersionUrl, value);
        }

        private bool _isBreakingNewVersion;
        public bool IsBreakingNewVersion
        {
            get => _isBreakingNewVersion;
            set => SetAndNotifyIfChanged(ref _isBreakingNewVersion, value);
        }

        // ---- In-place update progress (shown next to the Update link instead of a full-screen mask) ----

        private bool _isUpdating;
        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (SetAndNotifyIfChanged(ref _isUpdating, value))
                    RaisePropertyChanged(nameof(IsUpdatePanelVisible));
            }
        }

        private double _updateProgress;
        public double UpdateProgress
        {
            get => _updateProgress;
            set => SetAndNotifyIfChanged(ref _updateProgress, value);
        }

        private bool _updateIsIndeterminate = true;
        public bool UpdateIsIndeterminate
        {
            get => _updateIsIndeterminate;
            set => SetAndNotifyIfChanged(ref _updateIsIndeterminate, value);
        }

        private string _updateStatus = "";
        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetAndNotifyIfChanged(ref _updateStatus, value);
        }

        private bool _updateFailed;
        public bool UpdateFailed
        {
            get => _updateFailed;
            set
            {
                if (SetAndNotifyIfChanged(ref _updateFailed, value))
                    RaisePropertyChanged(nameof(IsUpdatePanelVisible));
            }
        }

        public bool IsUpdatePanelVisible => IsUpdating || UpdateFailed;

        private IProgress<(string stage, double pct)>? _updateProgressSink;
        private IProgress<(string stage, double pct)> UpdateProgressSink => _updateProgressSink ??= new Progress<(string stage, double pct)>(p =>
        {
            var (stage, pct) = p;
            switch (stage)
            {
                case "download":
                    UpdateStatus = $"{TranslateUpdateText("Downloading update...", "Downloading update...")} {pct:F0}%";
                    UpdateProgress = pct;
                    UpdateIsIndeterminate = false;
                    break;
                case "verify":
                    UpdateStatus = TranslateUpdateText("Verifying update...", "Verifying update...");
                    UpdateIsIndeterminate = true;
                    break;
                case "extract":
                    UpdateStatus = TranslateUpdateText("Extracting update...", "Extracting update...");
                    UpdateIsIndeterminate = true;
                    break;
                case "ready-to-swap":
                    UpdateStatus = TranslateUpdateText("Update is ready to install...", "Update is ready to install...");
                    UpdateIsIndeterminate = true;
                    break;
                case "wait-exit":
                    UpdateStatus = TranslateUpdateText("Waiting for RemoteX to exit...", "Waiting for RemoteX to exit...");
                    UpdateIsIndeterminate = true;
                    break;
                case "swap":
                    UpdateStatus = TranslateUpdateText("Installing update...", "Installing update...");
                    UpdateIsIndeterminate = true;
                    break;
                case "swapped":
                    UpdateStatus = TranslateUpdateText("Update installed. Restarting...", "Update installed. Restarting...");
                    UpdateIsIndeterminate = true;
                    break;
                case "error":
                    UpdateStatus = TranslateUpdateText("Update failed.", "Update failed.");
                    UpdateIsIndeterminate = false;
                    break;
                case "uac-cancelled":
                    UpdateStatus = TranslateUpdateText("Administrator permission was not granted.", "Administrator permission was not granted.");
                    UpdateIsIndeterminate = false;
                    break;
                case "failed":
                case "rolled-back":
                case "rollback-failed":
                    UpdateStatus = TranslateUpdateText("Update failed.", "Update failed.");
                    UpdateIsIndeterminate = false;
                    break;
            }
        });

        private static string TranslateUpdateText(string key, string fallback)
        {
            try
            {
                var text = IoC.Translate(key);
                return string.IsNullOrWhiteSpace(text) ? fallback : text;
            }
            catch
            {
                return fallback;
            }
        }

        private static string UpdateFailureText(string detail = "")
        {
            var message = TranslateUpdateText(
                "Update failed. The previous version is still running. You can retry or download it manually.",
                "Update failed. The previous version is still running. You can retry or download it manually.");
            return string.IsNullOrWhiteSpace(detail) ? message : $"{message}\n{detail}";
        }

        public void CheckUpdateAsync()
        {
            _checker?.CheckUpdateAsync();
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            // VersionHelper raises this callback from its background check task.
            // All state consumed by AboutPageView is WPF-bound and must be changed
            // on the dispatcher thread.
            Execute.OnUIThread(() =>
            {
                NewVersion = result.NewerVersion;
                NewVersionUrl = result.NewerUrl;
                IsBreakingNewVersion = result.NewerHasBreakChange;
                var v = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
                if (IsBreakingNewVersion
                    && VersionHelper.Version.FromString(result.NewerVersion) > v)
                {
                    IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>());
                }
            });
        }


        public string RepositoryUrl => "https://github.com/aiclu/RemoteX";

        private RelayCommand? _cmdOpenRepository;
        public RelayCommand CmdOpenRepository => _cmdOpenRepository ??= new RelayCommand(
            _ => HyperlinkHelper.OpenUriBySystem(RepositoryUrl));

        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    this.RequestClose();
                });
            }
        }

        private RelayCommand? _cmdUpdate;
        public RelayCommand CmdUpdate
        {
            get
            {
                return _cmdUpdate ??= new RelayCommand(async (o) =>
                {
                    if (IsUpdating) return;
                    if (IsBreakingNewVersion)
                    {
                        MaskLayerController.ShowProcessingRing();
                        IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>(), ownerViewModel: IoC.Get<MainWindowViewModel>());
                        MaskLayerController.HideMask();
                        return;
                    }
#if FOR_MICROSOFT_STORE_ONLY
                    HyperlinkHelper.OpenUriBySystem("ms-windows-store://review/?productid=9PNMNF92JNFP");
#else
                    if (!SelfUpdateService.UpdaterExists())
                    {
                        // No updater shipped (e.g. dev build): fall back to the browser.
                        HyperlinkHelper.OpenUriBySystem(string.IsNullOrWhiteSpace(NewVersionUrl)
                            ? AppVersion.UpdatePublishUrls[0]
                            : NewVersionUrl);
                        return;
                    }

                    // In-place progress next to the Update link; no full-screen mask.
                    IsUpdating = true;
                    UpdateFailed = false;
                    UpdateIsIndeterminate = true;
                    UpdateProgress = 0;
                    UpdateStatus = TranslateUpdateText("Checking update...", "Checking update...");
                    SelfUpdatePackage? package = null;
                    try
                    {
                        package = await SelfUpdateService.GetLatestSelfContainedPackageAsync();
                        if (package == null)
                        {
                            if (string.IsNullOrWhiteSpace(NewVersionUrl))
                                NewVersionUrl = AppVersion.UpdatePublishUrls[0];
                            UpdateFailed = true;
                            UpdateStatus = TranslateUpdateText("Cannot locate the update package.", "Cannot locate the update package.");
                            MessageBoxHelper.ErrorAlert(UpdateFailureText("The update package could not be located. Use the manual download link below to continue."));
                            return;
                        }

                        var releaseVersion = VersionHelper.Version.FromString(package.Version);
                        if (releaseVersion <= AppVersion.VersionData)
                        {
                            NewVersion = "";
                            UpdateStatus = TranslateUpdateText("RemoteX is already up to date.", "RemoteX is already up to date.");
                            return;
                        }

                        NewVersion = package.Version;
                        NewVersionUrl = string.IsNullOrWhiteSpace(package.ReleaseUrl) ? NewVersionUrl : package.ReleaseUrl;
                        var targetExe = SelfUpdateService.GetCurrentExecutablePath();
                        var runElevated = !SelfUpdateService.IsInstallDirectoryWritable(targetExe);
                        var result = await SelfUpdateService.RunUpdaterAsync(package, targetExe, runElevated, UpdateProgressSink, onReadyToSwap: () =>
                        {
                            // The updater has downloaded, verified, extracted and preflighted
                            // the package. Close only at this handoff point so failures before
                            // replacement leave the current application running.
                            Execute.OnUIThreadSync(() => App.Close());
                        });
                        if (result.Succeeded && !App.ExitingFlag)
                        {
                            UpdateFailed = true;
                            UpdateStatus = TranslateUpdateText("The update is ready, but RemoteX did not close.", "The update is ready, but RemoteX did not close.");
                            MessageBoxHelper.ErrorAlert(UpdateFailureText("Please close and restart RemoteX, then try again if the update is still offered."));
                        }
                        else if (!result.Succeeded)
                        {
                            UpdateFailed = true;
                            var statusKey = result.ElevationCancelled ? "Administrator permission was not granted." : "Update failed.";
                            UpdateStatus = TranslateUpdateText(statusKey, statusKey);
                            var detail = string.IsNullOrWhiteSpace(result.Message)
                                ? $"Updater log: {SelfUpdateService.UpdaterLogPath}"
                                : $"{result.Message}\nUpdater log: {SelfUpdateService.UpdaterLogPath}";
                            MessageBoxHelper.ErrorAlert(UpdateFailureText(detail));
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Error(ex);
                        if (package != null)
                            SelfUpdateService.MarkUpdateFailed(package.Version, ex.Message);
                        UpdateFailed = true;
                        UpdateStatus = TranslateUpdateText("Update failed.", "Update failed.");
                        MessageBoxHelper.ErrorAlert(UpdateFailureText($"Updater log: {SelfUpdateService.UpdaterLogPath}"));
                    }
                    finally
                    {
                        IsUpdating = false;
                    }
#endif
                });
            }
        }

        private RelayCommand? _cmdOpenUpdatePage;
        public RelayCommand CmdOpenUpdatePage
        {
            get
            {
                return _cmdOpenUpdatePage ??= new RelayCommand(_ =>
                {
                    if (!string.IsNullOrWhiteSpace(NewVersionUrl))
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                });
            }
        }
    }
}
