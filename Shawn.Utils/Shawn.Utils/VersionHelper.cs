using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#pragma warning disable CS0659, CS0660, CS0661
namespace Shawn.Utils
{
    public class VersionHelper
    {
        public class Version
        {
            protected bool Equals(Version? other)
            {
                if (other is null)
                    return false;
                return Major == other.Major && Minor == other.Minor && Patch == other.Patch && Build == other.Build && PreRelease == other.PreRelease;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Version)obj);
            }

            public readonly uint Major;
            public readonly uint Minor;
            public readonly uint Patch;
            public readonly uint Build;
            public readonly string PreRelease;

            public Version(uint major, uint minor, uint patch, uint build, string preRelease = "")
            {
                this.Major = major;
                this.Minor = minor;
                this.Patch = patch;
                this.Build = build;
                this.PreRelease = preRelease;
            }

            public override string ToString()
            {
                var sb = new StringBuilder($"{Major}.{Minor}.{Patch}");
                if (Build > 0)
                    sb.Append($".{Build}");

                if (!string.IsNullOrEmpty(PreRelease))
                    sb.Append($"-{PreRelease}");

                return sb.ToString();
            }

            public static Version FromString(string versionString)
            {
                bool isPreRelease = versionString.IndexOf("-", StringComparison.Ordinal) > 0;
                string preRelease = "";
                var splits = versionString.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                if (isPreRelease)
                {
                    var i = versionString.IndexOf("-", StringComparison.Ordinal);
                    preRelease = versionString.Substring(i + 1);
                    versionString = versionString.Substring(0, i);
                    splits = versionString.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                }
                uint major = 0;
                uint minor = 0;
                uint patch = 0;
                uint build = 0;
                if (splits.Length >= 3)
                {
                    if (uint.TryParse(splits[0], out var majorTmp)
                        && uint.TryParse(splits[1], out var minorTmp)
                        && uint.TryParse(splits[2], out var patchTmp)
                    )
                    {
                        major = majorTmp;
                        minor = minorTmp;
                        patch = patchTmp;
                    }
                }

                if (splits.Length >= 4
                     && uint.TryParse(splits[3], out var tmp))
                {
                    build = tmp;
                }
                return new Version(major, minor, patch, build, preRelease);
            }

            public static bool operator >(Version a, Version b)
            {
                return Compare(b, a);
            }

            public static bool operator <(Version a, Version b)
            {
                return Compare(a, b);
            }

            public static bool operator >=(Version a, Version b)
            {
                return a.Equals(b) || Compare(b, a);
            }

            public static bool operator <=(Version a, Version b)
            {
                return a.Equals(b) || Compare(a, b);
            }

            public static bool operator ==(Version? a, Version? b)
            {
                if (a is null && b is null)
                    return true;
                if (a is null || b is null)
                    return false;
                return a.Equals(b);
            }

            public static bool operator !=(Version? a, Version? b)
            {
                return !(a == b);
            }

            /// <summary>
            /// if v2 is newer, return true
            /// </summary>
            /// <param name="v1"></param>
            /// <param name="v2"></param>
            /// <returns></returns>
            public static bool Compare(Version? v1, Version? v2)
            {
                if (v1 is null || v2 is null)
                    return false;

                if (v2.Major > v1.Major)
                    return true;
                if (v2.Major == v1.Major
                    && v2.Minor > v1.Minor)
                    return true;
                if (v2.Major == v1.Major
                    && v2.Minor == v1.Minor
                    && v2.Patch > v1.Patch)
                    return true;
				if (v2.Major == v1.Major
					&& v2.Minor == v1.Minor
					&& v2.Patch == v1.Patch
					&& v2.Build > v1.Build)
					return true;

				if (v2.Major == v1.Major
					&& v2.Minor == v1.Minor
					&& v2.Patch == v1.Patch
					&& v2.Build == v1.Build
				   )
				{
					if ((string.IsNullOrEmpty(v2.PreRelease) && string.IsNullOrEmpty(v1.PreRelease) == false)
						|| string.CompareOrdinal(v2.PreRelease.ToLower(), v1.PreRelease.ToLower()) > 0)
						return true;
				}
				return false;
			}
        }



        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public delegate void OnNewVersionReleaseDelegate(CheckUpdateResult result);
        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public OnNewVersionReleaseDelegate? OnNewVersionRelease = null;

        public delegate CheckUpdateResult CheckMethod(string html, string publishUrl, Version currentVersion, Version? ignoreVersion = null);


        private readonly string[] _checkUrls;
        private readonly string[] _publishUrls;
        private readonly Version _currentVersion;
        public Version? IgnoreVersion;
        private readonly CheckMethod? _customCheckMethod = null;
        private readonly CheckMethod _defaultCheckMethod;
        public VersionHelper(Version version, string[] checkUrls, string[]? publishUrls = null,
            Version? ignoreVersion = null,
            CheckMethod? customCheckMethod = null)
        {
            _currentVersion = version;
            _checkUrls = checkUrls;
            _publishUrls = publishUrls?.Length != checkUrls.Length ? checkUrls : publishUrls;
            IgnoreVersion = ignoreVersion;
            _customCheckMethod = customCheckMethod;
            _defaultCheckMethod = DefaultCheckMethod;
        }


        public struct CheckUpdateResult
        {
            public readonly bool NewerPublished;
            public readonly string NewerVersion;
            public readonly string NewerUrl;
            public readonly bool NewerHasBreakChange;

            public CheckUpdateResult(bool newerPublished, string newerVersion, string newerUrl, bool newerHasBreakChange)
            {
                NewerPublished = newerPublished;
                NewerVersion = newerVersion;
                NewerUrl = newerUrl;
                NewerHasBreakChange = newerHasBreakChange;
            }

            public static CheckUpdateResult False()
            {
                return new CheckUpdateResult(false, "", "", false);
            }
        }

        public static CheckUpdateResult DefaultCheckMethod(string urlContent, string publishUrl, VersionHelper.Version currentVersion, VersionHelper.Version? ignoreVersion)
        {
            try
            {
                string html = urlContent;
                var vs = Regex.Match(html, @".?latest\sversion:\s*([\d|.]*)", RegexOptions.IgnoreCase);
                if (vs.Success)
                {
                    var tmp = vs.ToString().Trim();
                    var versionString = tmp.Substring(tmp.IndexOf("version:", StringComparison.OrdinalIgnoreCase) + "version:".Length + 1).Trim('!').Trim();
                    var releasedVersion = Version.FromString(versionString);
                    if (ignoreVersion is not null)
                    {
                        if (releasedVersion <= ignoreVersion)
                        {
                            return CheckUpdateResult.False();
                        }
                    }
                    if (releasedVersion > currentVersion)
                        return new CheckUpdateResult(true, versionString, publishUrl, tmp.FirstOrDefault() == '!' || tmp.LastOrDefault() == '!');
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
            return CheckUpdateResult.False();
        }

        private CheckUpdateResult CheckUpdateFromUrl(string checkUrl, string publishUrl)
        {
            try
            {
                var html = HttpHelper.Get(checkUrl).ToLower();
                return _customCheckMethod?.Invoke(html, publishUrl, _currentVersion, IgnoreVersion) ?? _defaultCheckMethod.Invoke(html, publishUrl, _currentVersion, IgnoreVersion);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
            return CheckUpdateResult.False();
        }



        /// <summary>
        /// Check if new release, return true + url.
        /// </summary>
        /// <returns></returns>
        public CheckUpdateResult CheckUpdate()
        {
            if (_checkUrls?.Length > 0)
            {
                for (var i = 0; i < _checkUrls.Length; i++)
                {
                    var tuple = CheckUpdateFromUrl(_checkUrls[i], _publishUrls[i]);
                    if (tuple.NewerPublished)
                        return tuple;
                }
            }
            return CheckUpdateResult.False();
        }

        /// <summary>
        /// Check if new release, invoke OnNewRelease with new version & url.
        /// </summary>
        /// <returns></returns>
        public void CheckUpdateAsync()
        {
            var t = new Task(() =>
            {
                var r = CheckUpdate();
                if (r.NewerPublished)
                {
                    OnNewVersionRelease?.Invoke(r);
                }
            });
            t.Start();
        }
    }
}