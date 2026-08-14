using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using _1RM.Utils.Tracing;
using Newtonsoft.Json.Linq;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service
{
    /// <summary>
    /// In-app self-update: query GitHub Releases for the self-contained zip, spawn the
    /// Rust updater.exe, stream its stdout JSON progress, and let the caller show UI.
    /// </summary>
    public class SelfUpdateService
    {
        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        static SelfUpdateService()
        {
            // GitHub API requires a User-Agent.
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RemoteX/" + AppVersion.Version);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        public static bool UpdaterExists()
        {
            var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.exe");
            return File.Exists(updaterPath);
        }

        /// <summary>
        /// Query the latest release and return the browser_download_url of the
        /// self-contained zip asset (RemoteX-{ver}-net9-x64-self-contained.zip).
        /// </summary>
        public static async Task<string?> GetLatestSelfContainedZipUrlAsync()
        {
            try
            {
                using var resp = await HttpClient.GetAsync(AppVersion.GitHubApiReleasesLatest);
                if (!resp.IsSuccessStatusCode)
                    return null;
                var json = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(json);
                var assets = root["assets"] as JArray;
                if (assets == null)
                    return null;
                var asset = assets
                    .Select(a => (Name: a["name"]?.ToString(), Url: a["browser_download_url"]?.ToString()))
                    .FirstOrDefault(a => a.Name != null && a.Name.EndsWith(AppVersion.ReleaseAssetNameSuffix, StringComparison.OrdinalIgnoreCase));
                return asset.Url;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// Run updater.exe <url> <exePath> --restart and raise progress/error events as it goes.
        /// When the updater reaches the "wait-exit" stage (download+verify+extract done), it
        /// invokes <paramref name="onWaitExit"/>; the caller should then close the app so the
        /// updater can swap the exe and restart. The task completes when updater.exe exits.
        /// </summary>
        public static async Task<bool> RunUpdaterAsync(string zipUrl, IProgress<(string stage, double pct)>? progress = null, Action? onWaitExit = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterExe = Path.Combine(baseDir, "updater.exe");
            var appExe = Path.Combine(baseDir, "RemoteX.exe");
            if (!File.Exists(updaterExe))
            {
                SimpleLogHelper.Error($"updater.exe not found at {updaterExe}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = $"\"{zipUrl}\" \"{appExe}\" --restart",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                try
                {
                    var j = JObject.Parse(e.Data);
                    switch (j["type"]?.ToString())
                    {
                        case "stage":
                            var stage = j["stage"]?.ToString();
                            SimpleLogHelper.Debug($"[Updater] stage={stage}");
                            progress?.Report((stage, 0));
                            if (stage == "wait-exit")
                                onWaitExit?.Invoke();
                            break;
                        case "progress":
                            var pct = j["pct"]?.ToObject<double>() ?? 0;
                            progress?.Report(("download", pct));
                            break;
                        case "error":
                            var msg = j["message"]?.ToString();
                            SimpleLogHelper.Error($"updater error: {msg}");
                            progress?.Report(("error", 0));
                            break;
                    }
                }
                catch
                {
                    // non-JSON line: ignore
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    SimpleLogHelper.Error($"updater stderr: {e.Data}");
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
    }
}
