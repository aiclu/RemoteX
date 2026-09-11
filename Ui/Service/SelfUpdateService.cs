using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Utils.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shawn.Utils;

namespace _1RM.Service
{
    public sealed class SelfUpdatePackage
    {
        public SelfUpdatePackage(string version, string assetUrl, string releaseUrl, string? sha256)
        {
            Version = version;
            AssetUrl = assetUrl;
            ReleaseUrl = releaseUrl;
            Sha256 = sha256;
        }

        public string Version { get; }
        public string AssetUrl { get; }
        public string ReleaseUrl { get; }
        public string? Sha256 { get; }
    }

    public sealed class PendingUpdateState
    {
        public PendingUpdateState(string targetVersion, string phase, string? message, string? backupPath, double progress)
        {
            TargetVersion = targetVersion;
            Phase = phase;
            Message = message ?? "";
            BackupPath = backupPath ?? "";
            Progress = progress;
        }

        public string TargetVersion { get; }
        public string Phase { get; }
        public string Message { get; }
        public string BackupPath { get; }
        public double Progress { get; }

        public bool IsFailure => Phase.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || Phase.Equals("rolled-back", StringComparison.OrdinalIgnoreCase)
            || Phase.Equals("rollback-failed", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class SelfUpdateExecutionResult
    {
        public SelfUpdateExecutionResult(bool succeeded, string phase, string? message = null, bool elevationCancelled = false)
        {
            Succeeded = succeeded;
            Phase = phase;
            Message = message ?? "";
            ElevationCancelled = elevationCancelled;
        }

        public bool Succeeded { get; }
        public string Phase { get; }
        public string Message { get; }
        public bool ElevationCancelled { get; }
    }

    /// <summary>
    /// In-app self-update: query GitHub Releases for a self-contained package,
    /// stage it with updater.exe, and persist enough state to validate the next
    /// application start.
    /// </summary>
    public static class SelfUpdateService
    {
        private const string UpdateStateFileName = "update-state.json";
        private static readonly TimeSpan ElevatedReadyTimeout = TimeSpan.FromMinutes(30);
        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        static SelfUpdateService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RemoteX/" + AppVersion.Version);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        public static string UpdaterLogPath => Path.Combine(Path.GetTempPath(), "RemoteX-updater.log");

        public static string PendingUpdateStatePath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, UpdateStateFileName);

        public static bool UpdaterExists()
        {
            return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.exe"));
        }

        public static string GetCurrentExecutablePath()
        {
            try
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to resolve current executable path: {e.Message}");
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteX.exe");
        }

        /// <summary>
        /// Query the latest release once and return the exact package metadata
        /// that will be used by the updater.
        /// </summary>
        public static async Task<SelfUpdatePackage?> GetLatestSelfContainedPackageAsync()
        {
            try
            {
                using var resp = await HttpClient.GetAsync(AppVersion.GitHubApiReleasesLatest);
                if (!resp.IsSuccessStatusCode)
                    return null;

                var json = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(json);
                if (!TryNormalizeVersion(root["tag_name"]?.ToString(), out var version))
                    return null;

                var assets = root["assets"] as JArray;
                var asset = assets?
                    .OfType<JObject>()
                    .FirstOrDefault(a => IsReleaseAssetForVersion(a["name"]?.ToString(), version));
                var assetUrl = asset?["browser_download_url"]?.ToString();
                if (string.IsNullOrWhiteSpace(assetUrl))
                    return null;

                var digest = asset?["digest"]?.ToString();
                if (!string.IsNullOrWhiteSpace(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    digest = digest.Substring("sha256:".Length);

                return new SelfUpdatePackage(
                    version,
                    assetUrl,
                    root["html_url"]?.ToString() ?? AppVersion.UpdatePublishUrls[0],
                    string.IsNullOrWhiteSpace(digest) ? null : digest);
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// Compatibility wrapper for callers that only need the package URL.
        /// </summary>
        public static async Task<string?> GetLatestSelfContainedZipUrlAsync()
        {
            return (await GetLatestSelfContainedPackageAsync())?.AssetUrl;
        }

        public static bool IsInstallDirectoryWritable(string executablePath)
        {
            var directory = Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            var probe = Path.Combine(directory, $".remotex-update-probe-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.SequentialScan))
                {
                    stream.WriteByte(0);
                    stream.Flush(true);
                }

                File.Delete(probe);
                return true;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Update target directory is not writable: {e.Message}");
                try
                {
                    if (File.Exists(probe))
                        File.Delete(probe);
                }
                catch
                {
                    // Best-effort cleanup only.
                }

                return false;
            }
        }

        public static PendingUpdateState? CheckPendingUpdate()
        {
            var state = TryReadPendingUpdateState();
            if (state == null)
                return null;

            if (TryParseVersion(state.TargetVersion, out var targetVersion)
                && targetVersion <= AppVersion.VersionData)
            {
                ClearPendingUpdateState(state);
                return null;
            }

            return state;
        }

        public static PendingUpdateState? TryReadPendingUpdateState()
        {
            var statePath = PendingUpdateStatePath;
            var candidates = new[] { statePath, statePath + ".tmp" };
            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var root = JObject.Parse(File.ReadAllText(path));
                    var targetVersion = root["targetVersion"]?.ToString();
                    var phase = root["phase"]?.ToString();
                    if (string.IsNullOrWhiteSpace(targetVersion) || string.IsNullOrWhiteSpace(phase))
                        continue;

                    return new PendingUpdateState(
                        targetVersion,
                        phase,
                        root["message"]?.ToString(),
                        root["backupPath"]?.ToString(),
                        root["progress"]?.ToObject<double>() ?? 0);
                }
                catch (Exception e)
                {
                    // A torn primary file must not hide a valid .tmp marker.
                    SimpleLogHelper.Debug($"Unable to read self-update state candidate '{path}': {e.Message}");
                }
            }

            return null;
        }

        public static void WritePendingUpdateState(string targetVersion, string phase, string? message = null, string? backupPath = null, double progress = 0)
        {
            var statePath = PendingUpdateStatePath;
            try
            {
                var directory = Path.GetDirectoryName(statePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var root = new JObject
                {
                    ["targetVersion"] = targetVersion,
                    ["phase"] = phase,
                    ["message"] = message ?? "",
                    ["backupPath"] = backupPath ?? "",
                    ["progress"] = progress,
                };
                var tempPath = statePath + ".tmp";
                File.WriteAllText(tempPath, root.ToString(Formatting.None), new UTF8Encoding(false));

                try
                {
                    if (File.Exists(statePath))
                        File.Replace(tempPath, statePath, null, true);
                    else
                        File.Move(tempPath, statePath);
                }
                catch
                {
                    if (File.Exists(statePath))
                        File.Delete(statePath);
                    File.Move(tempPath, statePath);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error($"Unable to write self-update state: {e}");
                throw;
            }
        }

        public static void MarkUpdateFailed(string targetVersion, string message)
        {
            try
            {
                // Keep a backup directory reference so a later startup can clean
                // it up (or diagnose a failed rollback) instead of orphaning it.
                var previous = TryReadPendingUpdateState();
                if (previous != null
                    && (previous.Phase.Equals("swapped", StringComparison.OrdinalIgnoreCase)
                        || previous.Phase.Equals("rolled-back", StringComparison.OrdinalIgnoreCase)
                        || previous.Phase.Equals("rollback-failed", StringComparison.OrdinalIgnoreCase)))
                {
                    // Do not downgrade a terminal transaction state when a
                    // wrapper process reports a secondary exit error. The
                    // startup validator needs the original rollback context.
                    return;
                }

                WritePendingUpdateState(
                    targetVersion,
                    "failed",
                    message,
                    string.IsNullOrWhiteSpace(previous?.BackupPath) ? null : previous.BackupPath,
                    previous?.Progress ?? 0);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public static void ClearPendingUpdateState(PendingUpdateState? state = null)
        {
            try
            {
                if (state != null && !string.IsNullOrWhiteSpace(state.BackupPath))
                {
                    try
                    {
                        if (IsSafeBackupPath(state.BackupPath) && Directory.Exists(state.BackupPath))
                            Directory.Delete(state.BackupPath, true);
                        else if (Directory.Exists(state.BackupPath))
                            SimpleLogHelper.Warning($"Ignoring unexpected self-update backup path: {state.BackupPath}");
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug($"Unable to remove update backup: {e.Message}");
                    }
                }

                var statePath = PendingUpdateStatePath;
                if (File.Exists(statePath))
                    File.Delete(statePath);
                if (File.Exists(statePath + ".tmp"))
                    File.Delete(statePath + ".tmp");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error($"Unable to clear self-update state: {e}");
            }
        }

        private static bool IsSafeBackupPath(string path)
        {
            try
            {
                var backupPath = Path.GetFullPath(path);
                var backupDirectory = Path.GetDirectoryName(backupPath);
                var installDirectory = Path.GetDirectoryName(Path.GetFullPath(GetCurrentExecutablePath()));
                var backupName = Path.GetFileName(backupPath);
                return !string.IsNullOrWhiteSpace(backupDirectory)
                    && !string.IsNullOrWhiteSpace(installDirectory)
                    && string.Equals(backupDirectory, installDirectory, StringComparison.OrdinalIgnoreCase)
                    && backupName.StartsWith(".remotex-update-backup-", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to validate self-update backup path: {e.Message}");
                return false;
            }
        }

        private static bool SupportsTransactionalUpdater(string updaterExe)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updaterExe,
                        Arguments = "--version",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                if (!process.Start())
                    return false;

                // The output is deliberately drained so a future protocol
                // banner cannot fill the redirected pipe during probing.
                _ = process.StandardOutput.ReadToEnd();
                _ = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to probe updater protocol: {e.Message}");
                return false;
            }
        }

        private static async Task<SelfUpdateExecutionResult> RunLegacyUpdaterBootstrapAsync(
            SelfUpdatePackage package,
            string targetExe,
            bool runElevated,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            var bootstrapRoot = Path.Combine(Path.GetTempPath(), $"RemoteX-bootstrap-{Guid.NewGuid():N}");
            var zipPath = Path.Combine(bootstrapRoot, "update.zip");
            var stagePath = Path.Combine(bootstrapRoot, "stage");
            var handedOff = false;

            try
            {
                Directory.CreateDirectory(bootstrapRoot);
                await DownloadUpdateArchiveAsync(package.AssetUrl, zipPath, progress);

                if (!string.IsNullOrWhiteSpace(package.Sha256))
                {
                    progress?.Report(("verify", 0));
                    VerifyUpdateArchive(zipPath, package.Sha256!);
                }

                progress?.Report(("extract", 0));
                ExtractUpdateArchive(zipPath, stagePath);
                var stagedUpdater = FindFile(stagePath, "updater.exe");
                if (stagedUpdater == null || FindFile(stagePath, "RemoteX.exe") == null)
                    throw new InvalidDataException("The release archive does not contain RemoteX.exe and updater.exe.");

                // Let the staged updater perform the final target-directory
                // preflight and publish ready-to-swap.  In particular, do not
                // seed the marker with ready-to-swap: the elevated path polls
                // this file and must not mistake a bootstrap placeholder for a
                // completed handoff.
                WritePendingUpdateState(package.Version, "staged");
                progress?.Report(("extract", 100));
                var arguments = BuildPreparedUpdaterArguments(stagePath, targetExe, package.Version, PendingUpdateStatePath);
                SelfUpdateExecutionResult result;
                if (runElevated)
                {
                    result = await RunPreparedElevatedUpdaterAsync(stagedUpdater, arguments, package, progress, onReadyToSwap);
                    handedOff = result.Succeeded;
                }
                else
                {
                    result = await RunPreparedStandardUpdaterAsync(stagedUpdater, arguments, package, progress, onReadyToSwap);
                }

                return result;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                MarkUpdateFailed(package.Version, e.Message);
                return new SelfUpdateExecutionResult(false, "failed", e.Message);
            }
            finally
            {
                // An elevated helper continues after this method returns and
                // needs the extracted files. Standard helpers have exited.
                if (!handedOff)
                    TryDeleteDirectory(bootstrapRoot);
            }
        }

        private static async Task DownloadUpdateArchiveAsync(
            string url,
            string destination,
            IProgress<(string stage, double pct)>? progress)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Update download returned {(int)response.StatusCode} ({response.ReasonPhrase}).");

            using var input = await response.Content.ReadAsStreamAsync();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
            var total = response.Content.Headers.ContentLength ?? 0;
            long received = 0;
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, read);
                received += read;
                if (total > 0)
                    progress?.Report(("download", received * 100d / total));
            }

            await output.FlushAsync();
        }

        private static void VerifyUpdateArchive(string archivePath, string expectedSha256)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(archivePath);
            var actual = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
            if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Update SHA-256 mismatch: expected {expectedSha256}, got {actual}.");
        }

        private static void ExtractUpdateArchive(string archivePath, string destination)
        {
            Directory.CreateDirectory(destination);
            var root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            using var stream = File.OpenRead(archivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var output = Path.GetFullPath(Path.Combine(root, relative));
                if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unsafe update archive path: {entry.FullName}");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(output);
                    continue;
                }

                var parent = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);
                using var input = entry.Open();
                using var outputStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);
                input.CopyTo(outputStream);
            }
        }

        private static string? FindFile(string root, string fileName)
        {
            try
            {
                if (!Directory.Exists(root))
                    return null;

                foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to inspect bootstrap archive: {e.Message}");
            }

            return null;
        }

        private static async Task<SelfUpdateExecutionResult> RunPreparedStandardUpdaterAsync(
            string updaterExe,
            string arguments,
            SelfUpdatePackage package,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = arguments,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var readySignalled = 0;
            var callbackFailed = 0;
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                try
                {
                    var json = JObject.Parse(e.Data);
                    switch (json["type"]?.ToString())
                    {
                        case "stage":
                            var stage = json["stage"]?.ToString() ?? "";
                            SimpleLogHelper.Debug($"[Bootstrap updater] stage={stage}");
                            progress?.Report((stage, 0));
                            if (stage.Equals("ready-to-swap", StringComparison.OrdinalIgnoreCase)
                                && Interlocked.Exchange(ref readySignalled, 1) == 0)
                            {
                                try
                                {
                                    onReadyToSwap?.Invoke();
                                }
                                catch (Exception callbackException)
                                {
                                    Interlocked.Exchange(ref callbackFailed, 1);
                                    SimpleLogHelper.Error(callbackException);
                                    MarkUpdateFailed(package.Version, callbackException.Message);
                                    TryKill(process);
                                }
                            }
                            break;
                        case "progress":
                            progress?.Report(("download", json["pct"]?.ToObject<double>() ?? 0));
                            break;
                        case "error":
                            SimpleLogHelper.Error($"bootstrap updater error: {json["message"]?.ToString() ?? ""}");
                            progress?.Report(("error", 0));
                            break;
                    }
                }
                catch
                {
                    // Non-JSON diagnostic lines are intentionally ignored.
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    SimpleLogHelper.Error($"bootstrap updater stderr: {e.Data}");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                // WaitForExitAsync does not guarantee that all asynchronous
                // output callbacks have drained.
                process.WaitForExit();
                var state = TryReadPendingUpdateState();
                if (process.ExitCode == 0 && Volatile.Read(ref callbackFailed) == 0
                    && (Volatile.Read(ref readySignalled) != 0 || state?.Phase == "swapped"))
                    return new SelfUpdateExecutionResult(true, "swapped");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                TryKill(process);
                MarkUpdateFailed(package.Version, e.Message);
                return new SelfUpdateExecutionResult(false, "failed", e.Message);
            }

            const string message = "The bootstrap updater exited before the application was replaced.";
            MarkUpdateFailed(package.Version, message);
            return new SelfUpdateExecutionResult(false, "failed", message);
        }

        private static async Task<SelfUpdateExecutionResult> RunPreparedElevatedUpdaterAsync(
            string updaterExe,
            string arguments,
            SelfUpdatePackage package,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = updaterExe,
                    Arguments = arguments,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                });
                if (process == null)
                {
                    const string message = "The elevated updater could not be started.";
                    MarkUpdateFailed(package.Version, message);
                    return new SelfUpdateExecutionResult(false, "failed", message);
                }

                var ready = await WaitForReadyStateAsync(process, package.Version, progress);
                if (!ready)
                {
                    var state = TryReadPendingUpdateState();
                    if (state?.IsFailure != true)
                    {
                        const string message = "The elevated updater exited before the package was ready.";
                        MarkUpdateFailed(package.Version, message);
                        state = TryReadPendingUpdateState();
                    }

                    return new SelfUpdateExecutionResult(
                        false,
                        state?.Phase ?? "failed",
                        state?.Message ?? "The elevated updater exited before the package was ready.");
                }

                onReadyToSwap?.Invoke();
                return new SelfUpdateExecutionResult(true, "ready-to-swap");
            }
            catch (Win32Exception e)
            {
                var cancelled = e.NativeErrorCode == 1223;
                var message = cancelled ? "Administrator permission was not granted." : e.Message;
                progress?.Report((cancelled ? "uac-cancelled" : "error", 0));
                MarkUpdateFailed(package.Version, message);
                return new SelfUpdateExecutionResult(false, cancelled ? "uac-cancelled" : "failed", message, cancelled);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                MarkUpdateFailed(package.Version, e.Message);
                return new SelfUpdateExecutionResult(false, "failed", e.Message);
            }
        }

        private static string BuildPreparedUpdaterArguments(string stagePath, string targetExe, string targetVersion, string statePath)
        {
            var values = new[]
            {
                "--apply-stage",
                stagePath,
                targetExe,
                "--parent-pid",
                Environment.ProcessId.ToString(),
                "--target-version",
                targetVersion,
                "--state-file",
                statePath,
                "--restart",
            };
            return string.Join(" ", values.Select(QuoteArgument));
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to stop bootstrap updater: {e.Message}");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to remove bootstrap directory: {e.Message}");
            }
        }

        /// <summary>
        /// Run updater.exe. The normal path streams stdout. The elevated path
        /// uses the state file because ShellExecute/runas cannot redirect stdout.
        /// The callback is invoked only after download, verification, extraction,
        /// package validation and target-directory preflight have completed.
        /// </summary>
        public static async Task<SelfUpdateExecutionResult> RunUpdaterAsync(
            SelfUpdatePackage package,
            string targetExe,
            bool runElevated,
            IProgress<(string stage, double pct)>? progress = null,
            Action? onReadyToSwap = null)
        {
            var updaterExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.exe");
            if (!File.Exists(updaterExe))
            {
                SimpleLogHelper.Error($"updater.exe not found at {updaterExe}");
                return new SelfUpdateExecutionResult(false, "failed", "The updater executable was not found.");
            }

            try
            {
                WritePendingUpdateState(package.Version, "starting");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return new SelfUpdateExecutionResult(false, "failed", e.Message);
            }

            // Releases before the transactional updater was introduced contain
            // an older binary which rejects the new command-line switches and
            // cannot replace updater.exe. Bootstrap that first hop from the
            // downloaded archive so the installed updater is upgraded too.
            if (!SupportsTransactionalUpdater(updaterExe))
            {
                SimpleLogHelper.Warning("Legacy updater detected; bootstrapping the transactional updater from the release archive.");
                return await RunLegacyUpdaterBootstrapAsync(package, targetExe, runElevated, progress, onReadyToSwap);
            }

            var arguments = BuildUpdaterArguments(package, targetExe, PendingUpdateStatePath);

            if (runElevated)
                return await RunElevatedUpdaterAsync(updaterExe, arguments, package, progress, onReadyToSwap);

            return await RunStandardUpdaterAsync(updaterExe, arguments, package, progress, onReadyToSwap);
        }

        /// <summary>
        /// Compatibility overload retained for existing integrations.
        /// </summary>
        public static Task<bool> RunUpdaterAsync(string zipUrl, IProgress<(string stage, double pct)>? progress = null, Action? onWaitExit = null)
        {
            var package = new SelfUpdatePackage(AppVersion.Version, zipUrl, "", null);
            return RunUpdaterLegacyAsync(package, progress, onWaitExit);
        }

        private static async Task<bool> RunUpdaterLegacyAsync(
            SelfUpdatePackage package,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            var result = await RunUpdaterAsync(package, GetCurrentExecutablePath(), false, progress, onReadyToSwap);
            return result.Succeeded;
        }

        private static async Task<SelfUpdateExecutionResult> RunStandardUpdaterAsync(
            string updaterExe,
            string arguments,
            SelfUpdatePackage package,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = arguments,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.Environment["REMOTEX_TARGET_PID"] = Environment.ProcessId.ToString();
            psi.Environment["REMOTEX_TARGET_VERSION"] = package.Version;
            psi.Environment["REMOTEX_STATE_FILE"] = PendingUpdateStatePath;

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var readySignalled = 0;
            proc.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                try
                {
                    var json = JObject.Parse(e.Data);
                    switch (json["type"]?.ToString())
                    {
                        case "stage":
                            var stage = json["stage"]?.ToString() ?? "";
                            SimpleLogHelper.Debug($"[Updater] stage={stage}");
                            progress?.Report((stage, 0));
                            if (stage.Equals("ready-to-swap", StringComparison.OrdinalIgnoreCase)
                                && Interlocked.Exchange(ref readySignalled, 1) == 0)
                            {
                                try
                                {
                                    onReadyToSwap?.Invoke();
                                }
                                catch (Exception callbackException)
                                {
                                    SimpleLogHelper.Error(callbackException);
                                    MarkUpdateFailed(package.Version, callbackException.Message);
                                }
                            }
                            break;
                        case "progress":
                            var pct = json["pct"]?.ToObject<double>() ?? 0;
                            progress?.Report(("download", pct));
                            break;
                        case "error":
                            var message = json["message"]?.ToString() ?? "";
                            SimpleLogHelper.Error($"updater error: {message}");
                            progress?.Report(("error", 0));
                            break;
                    }
                }
                catch
                {
                    // Non-JSON diagnostic lines are intentionally ignored.
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    SimpleLogHelper.Error($"updater stderr: {e.Data}");
            };

            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync();
                proc.WaitForExit();
                var completedState = TryReadPendingUpdateState();
                if (proc.ExitCode == 0 && (Volatile.Read(ref readySignalled) != 0 || completedState?.Phase == "swapped"))
                    return new SelfUpdateExecutionResult(true, "swapped");

                if (completedState?.Phase == "swapped")
                    return new SelfUpdateExecutionResult(true, "swapped", completedState.Message);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }

            var state = TryReadPendingUpdateState();
            if (state?.Phase == "swapped")
                return new SelfUpdateExecutionResult(true, state.Phase, state.Message);
            if (state?.Phase == "ready-to-swap")
                return new SelfUpdateExecutionResult(false, state.Phase, state.Message);

            const string message = "Updater exited before the application was replaced.";
            MarkUpdateFailed(package.Version, message);
            state = TryReadPendingUpdateState();
            return new SelfUpdateExecutionResult(false, state?.Phase ?? "failed", state?.Message ?? message);
        }

        private static async Task<SelfUpdateExecutionResult> RunElevatedUpdaterAsync(
            string updaterExe,
            string arguments,
            SelfUpdatePackage package,
            IProgress<(string stage, double pct)>? progress,
            Action? onReadyToSwap)
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = arguments,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas",
            };

            Process? proc = null;
            try
            {
                proc = Process.Start(psi);
                if (proc == null)
                {
                    const string message = "The elevated updater could not be started.";
                    MarkUpdateFailed(package.Version, message);
                    return new SelfUpdateExecutionResult(false, "failed", message);
                }

                var ready = await WaitForReadyStateAsync(proc, package.Version, progress);
                if (!ready)
                {
                    var state = TryReadPendingUpdateState();
                    if (state?.IsFailure != true)
                    {
                        const string message = "The elevated updater exited before the package was ready.";
                        MarkUpdateFailed(package.Version, message);
                        state = TryReadPendingUpdateState();
                    }

                    return new SelfUpdateExecutionResult(false, state?.Phase ?? "failed", state?.Message ?? "The elevated updater exited before the package was ready.");
                }

                onReadyToSwap?.Invoke();
                return new SelfUpdateExecutionResult(true, "ready-to-swap");
            }
            catch (Win32Exception e)
            {
                // ERROR_CANCELLED (1223) is the expected path when the user
                // declines the UAC prompt; keep the old app alive.
                SimpleLogHelper.Warning($"Elevated updater was not started: {e.Message}");
                var cancelled = e.NativeErrorCode == 1223;
                var message = cancelled ? "Administrator permission was not granted." : e.Message;
                progress?.Report((cancelled ? "uac-cancelled" : "error", 0));
                MarkUpdateFailed(package.Version, message);
                return new SelfUpdateExecutionResult(false, cancelled ? "uac-cancelled" : "failed", message, cancelled);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                MarkUpdateFailed(package.Version, e.Message);
                return new SelfUpdateExecutionResult(false, "failed", e.Message);
            }
            finally
            {
                proc?.Dispose();
            }
        }

        private static async Task<bool> WaitForReadyStateAsync(Process proc, string targetVersion, IProgress<(string stage, double pct)>? progress)
        {
            var deadline = DateTime.UtcNow + ElevatedReadyTimeout;
            while (DateTime.UtcNow < deadline)
            {
                var state = TryReadPendingUpdateState();
                if (state != null && state.TargetVersion == targetVersion)
                {
                    progress?.Report((state.Phase, state.Progress));
                    if (state.Phase.Equals("ready-to-swap", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (state.IsFailure)
                        return false;
                }

                try
                {
                    if (proc.HasExited)
                        return false;
                }
                catch
                {
                    return false;
                }

                await Task.Delay(250);
            }

            try
            {
                if (!proc.HasExited)
                    proc.Kill();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug($"Unable to stop timed-out updater: {e.Message}");
            }

            MarkUpdateFailed(targetVersion, "The updater did not finish preparing the package in time.");
            return false;
        }

        private static string BuildUpdaterArguments(SelfUpdatePackage package, string targetExe, string statePath)
        {
            var values = new List<string>
            {
                package.AssetUrl,
                targetExe,
                "--pid",
                Environment.ProcessId.ToString(),
                "--target-version",
                package.Version,
                "--state-file",
                statePath,
                "--restart",
            };
            if (!string.IsNullOrWhiteSpace(package.Sha256))
            {
                values.Add("--sha256");
                values.Add(package.Sha256!);
            }

            return string.Join(" ", values.Select(QuoteArgument));
        }

        private static string QuoteArgument(string value)
        {
            // The values are URLs, paths and version strings. Windows paths
            // cannot contain a quote, so this safely handles spaces while also
            // protecting against malformed metadata.
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool IsReleaseAssetForVersion(string? assetName, string targetVersion)
        {
            if (string.IsNullOrWhiteSpace(assetName)
                || !assetName.StartsWith("RemoteX-", StringComparison.OrdinalIgnoreCase)
                || !assetName.EndsWith(AppVersion.ReleaseAssetNameSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var versionLength = assetName.Length - "RemoteX-".Length - AppVersion.ReleaseAssetNameSuffix.Length;
            if (versionLength <= 0)
                return false;

            var assetVersion = assetName.Substring("RemoteX-".Length, versionLength);
            return TryNormalizeVersion(assetVersion, out var normalizedAssetVersion)
                && string.Equals(normalizedAssetVersion, targetVersion, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNormalizeVersion(string? value, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var candidate = value.Trim();
            if (candidate.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(1);
            if (!Regex.IsMatch(candidate, @"^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?$"))
                return false;

            normalized = VersionHelper.Version.FromString(candidate).ToString();
            return true;
        }

        private static bool TryParseVersion(string value, out VersionHelper.Version version)
        {
            if (TryNormalizeVersion(value, out var normalized))
            {
                version = VersionHelper.Version.FromString(normalized);
                return true;
            }

            version = VersionHelper.Version.FromString("0.0.0");
            return false;
        }
    }
}
