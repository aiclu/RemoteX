using static Shawn.Utils.VersionHelper;

namespace _1RM
{
    public static class AppVersion
    {
        public const uint Major = 1;
        public const uint Minor = 0;
        public const uint Patch = 3;
        public const uint Build = 0;
        public const string BuildDate = "";
        public const string PreRelease = ""; // e.g. "alpha" "beta.2"

        public static readonly Version VersionData = new Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();


        public static string[] UpdateCheckUrls =>
            string.IsNullOrEmpty(PreRelease)
                ? new[]
                {
                    "https://github.com/aiclu/RemoteX/releases",
                    "https://github.com/aiclu/RemoteX",
                }
                : new[]
                {
                    "https://github.com/aiclu/RemoteX/releases/expanded_assets/Nightly",
                    "https://github.com/aiclu/RemoteX/releases",
                    "https://github.com/aiclu/RemoteX",
                };

        public static string[] UpdatePublishUrls =>
            string.IsNullOrEmpty(PreRelease)
                ? new[]
                {
                    "https://github.com/aiclu/RemoteX/releases/latest",
                    "https://github.com/aiclu/RemoteX",
                }
                : new[]
                {
                    "https://github.com/aiclu/RemoteX/releases/tag/Nightly",
                    "https://github.com/aiclu/RemoteX/releases/latest",
                    "https://github.com/aiclu/RemoteX",
                };

        /// <summary>
        /// GitHub repo for the self-updater (API + asset naming).
        /// </summary>
        public static string GitHubApiReleasesLatest => "https://api.github.com/repos/aiclu/RemoteX/releases/latest";

        /// <summary>
        /// The self-contained artifact is the one the updater downloads: it needs no
        /// pre-installed .NET runtime. CI asset name: RemoteX-{ver}-net9-x64-self-contained.zip
        /// </summary>
        public static string ReleaseAssetNameSuffix => "-net9-x64-self-contained.zip";
    }
}
