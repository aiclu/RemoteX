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

        public void CheckUpdateAsync()
        {
            _checker.CheckUpdateAsync();
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            this.NewVersion = result.NewerVersion;
            this.NewVersionUrl = result.NewerUrl;
            this.IsBreakingNewVersion = result.NewerHasBreakChange;
            var v = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
            if (this.IsBreakingNewVersion
                && VersionHelper.Version.FromString(result.NewerVersion) > v)
            {
                Execute.OnUIThreadSync(() =>
                {
                    IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>());
                });
            }
        }


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
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                        return;
                    }

                    var maskId = MaskLayerController.ShowProcessingRing("Checking update...");
                    try
                    {
                        var zipUrl = await SelfUpdateService.GetLatestSelfContainedZipUrlAsync();
                        if (string.IsNullOrEmpty(zipUrl))
                        {
                            MaskLayerController.HideMask(maskId);
                            MessageBoxHelper.ErrorAlert("Cannot locate the update package. Please download it manually.");
                            HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                            return;
                        }

                        var pvm = IoC.Get<ProcessingRingViewModel>();
                        var progress = new Progress<(string stage, double pct)>(p =>
                        {
                            var (stage, pct) = p;
                            switch (stage)
                            {
                                case "download":
                                    pvm.ProcessingRingMessage = $"Downloading update... {pct:F0}%";
                                    break;
                                case "verify":
                                    pvm.ProcessingRingMessage = "Verifying update...";
                                    break;
                                case "extract":
                                    pvm.ProcessingRingMessage = "Extracting update...";
                                    break;
                                case "wait-exit":
                                    pvm.ProcessingRingMessage = "Waiting for RemoteX to exit...";
                                    break;
                                case "swap":
                                    pvm.ProcessingRingMessage = "Installing update...";
                                    break;
                                case "swapped":
                                    pvm.ProcessingRingMessage = "Update installed. Restarting...";
                                    break;
                                case "error":
                                    pvm.ProcessingRingMessage = "Update failed.";
                                    break;
                            }
                        });

                        var ok = await SelfUpdateService.RunUpdaterAsync(zipUrl, progress, onWaitExit: () =>
                        {
                            // updater has downloaded+verified+extracted; close the app so it can
                            // swap the exe and restart us. The updater runs detached (no parent wait).
                            Execute.OnUIThreadSync(() => App.Close());
                        });
                        if (ok)
                        {
                            // If the app is still alive after updater exits, it means the updater
                            // failed to restart us; just inform the user.
                            MaskLayerController.HideMask(maskId);
                            MessageBoxHelper.ErrorAlert("Update finished but the app did not restart automatically. Please restart RemoteX.");
                        }
                        else
                        {
                            MaskLayerController.HideMask(maskId);
                            MessageBoxHelper.ErrorAlert("Update failed. Please try downloading it manually.");
                            HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Error(ex);
                        MaskLayerController.HideMask(maskId);
                        MessageBoxHelper.ErrorAlert("Update failed. Please try downloading it manually.");
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                    }
#endif
                });
            }
        }
    }
}