using System.Globalization;

namespace _1RM.View.Host.ProtocolHosts
{
    /// <summary>
    /// The network information captured from one RDP ActiveX instance.
    /// The snapshot contains only value types and strings so it can safely be
    /// consumed by the connection-information window.
    /// </summary>
    internal sealed class RdpNetworkMetricsSnapshot
    {
        public RdpNetworkMetricsSnapshot(
            uint? qualityLevel,
            int? bandwidthKbps,
            int? roundTripTimeMilliseconds)
        {
            QualityLevel = qualityLevel;
            BandwidthKbps = bandwidthKbps;
            RoundTripTimeMilliseconds = roundTripTimeMilliseconds;

        }

        public uint? QualityLevel { get; }
        public int? BandwidthKbps { get; }
        public int? RoundTripTimeMilliseconds { get; }

    }

    /// <summary>
    /// Thread-safe accumulator for one RDP control instance.
    /// </summary>
    internal sealed class RdpNetworkMetricsCollector
    {
        private readonly object _sync = new object();

        private uint? _qualityLevel;
        private int? _bandwidthKbps;
        private int? _roundTripTimeMilliseconds;

        public void Reset()
        {
            lock (_sync)
            {
                _qualityLevel = null;
                _bandwidthKbps = null;
                _roundTripTimeMilliseconds = null;
            }
        }

        public void RecordNetworkStatus(uint qualityLevel, int bandwidthKbps, int roundTripTimeMilliseconds)
        {
            lock (_sync)
            {
                _qualityLevel = RdpNetworkMetricsFormatter.IsKnownQualityLevel(qualityLevel)
                    ? qualityLevel
                    : null;

                var bandwidthWasReportedByEvent = RdpNetworkMetricsFormatter.IsValidBandwidth(bandwidthKbps);
                _bandwidthKbps = bandwidthWasReportedByEvent ? bandwidthKbps : null;

                var roundTripTimeWasReportedByEvent = RdpNetworkMetricsFormatter.IsValidRoundTripTime(roundTripTimeMilliseconds);
                _roundTripTimeMilliseconds = roundTripTimeWasReportedByEvent
                    ? roundTripTimeMilliseconds
                    : null;
            }
        }

        public RdpNetworkMetricsSnapshot Snapshot()
        {
            lock (_sync)
            {
                return new RdpNetworkMetricsSnapshot(
                    _qualityLevel,
                    _bandwidthKbps,
                    _roundTripTimeMilliseconds);
            }
        }
    }

    internal static class RdpNetworkMetricsFormatter
    {
        public static bool IsKnownQualityLevel(uint qualityLevel)
        {
            return qualityLevel is >= 1 and <= 4;
        }

        public static string? GetQualityTranslationKey(uint? qualityLevel)
        {
            return qualityLevel switch
            {
                1 => "Connection quality - Poor",
                2 => "Connection quality - Fair",
                3 => "Connection quality - Good",
                4 => "Connection quality - Excellent",
                _ => null,
            };
        }

        public static bool IsValidBandwidth(int bandwidthKbps)
        {
            return bandwidthKbps > 0;
        }

        public static bool IsValidRoundTripTime(int roundTripTimeMilliseconds)
        {
            return roundTripTimeMilliseconds >= 0;
        }

        public static string? FormatRoundTripTime(int? roundTripTimeMilliseconds)
        {
            return roundTripTimeMilliseconds.HasValue
                   && IsValidRoundTripTime(roundTripTimeMilliseconds.Value)
                ? $"{roundTripTimeMilliseconds.Value.ToString(CultureInfo.InvariantCulture)} ms"
                : null;
        }

        public static string? FormatBandwidth(int? bandwidthKbps)
        {
            if (!bandwidthKbps.HasValue || !IsValidBandwidth(bandwidthKbps.Value))
            {
                return null;
            }

            if (bandwidthKbps.Value < 100)
            {
                return "Less than 100 Kbps";
            }

            if (bandwidthKbps.Value < 1000)
            {
                return $"{bandwidthKbps.Value.ToString(CultureInfo.InvariantCulture)} Kbps";
            }

            return $"{(bandwidthKbps.Value / 1000d).ToString("0.00", CultureInfo.InvariantCulture)} Mbps";
        }

    }
}
