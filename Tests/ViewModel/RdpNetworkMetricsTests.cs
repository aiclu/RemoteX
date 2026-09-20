using System;
using _1RM.View.Host.ProtocolHosts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.ViewModel
{
    [TestClass]
    public class RdpNetworkMetricsTests
    {
        [TestMethod]
        public void NetworkStatusEventStoresQualityBandwidthAndRoundTripTime()
        {
            var collector = new RdpNetworkMetricsCollector();

            collector.RecordNetworkStatus(4, 7900, 16);
            var snapshot = collector.Snapshot();

            Assert.AreEqual((uint?)4, snapshot.QualityLevel);
            Assert.AreEqual((int?)7900, snapshot.BandwidthKbps);
            Assert.AreEqual((int?)16, snapshot.RoundTripTimeMilliseconds);
            Assert.AreEqual("7.90 Mbps", RdpNetworkMetricsFormatter.FormatBandwidth(snapshot.BandwidthKbps));
            Assert.AreEqual("16 ms", RdpNetworkMetricsFormatter.FormatRoundTripTime(snapshot.RoundTripTimeMilliseconds));
        }

        [TestMethod]
        public void InvalidNetworkEventValuesRemainUnavailable()
        {
            var collector = new RdpNetworkMetricsCollector();

            collector.RecordNetworkStatus(0, 0, -1);
            var snapshot = collector.Snapshot();

            Assert.IsNull(snapshot.QualityLevel);
            Assert.IsNull(snapshot.BandwidthKbps);
            Assert.IsNull(snapshot.RoundTripTimeMilliseconds);
            Assert.IsNull(RdpNetworkMetricsFormatter.FormatBandwidth(snapshot.BandwidthKbps));
            Assert.IsNull(RdpNetworkMetricsFormatter.FormatRoundTripTime(snapshot.RoundTripTimeMilliseconds));
        }

        [TestMethod]
        public void QualityLevelsUseLocalizedTranslationKeysOnlyForKnownValues()
        {
            Assert.AreEqual("Connection quality - Poor", RdpNetworkMetricsFormatter.GetQualityTranslationKey(1));
            Assert.AreEqual("Connection quality - Excellent", RdpNetworkMetricsFormatter.GetQualityTranslationKey(4));
            Assert.IsNull(RdpNetworkMetricsFormatter.GetQualityTranslationKey(0));
            Assert.IsNull(RdpNetworkMetricsFormatter.GetQualityTranslationKey(5));
        }
        [TestMethod]
        public void ResetClearsMetricsWithoutChangingExistingSnapshot()
        {
            var collector = new RdpNetworkMetricsCollector();
            collector.RecordNetworkStatus(4, 7900, 16);
            var previous = collector.Snapshot();
            collector.Reset();
            var current = collector.Snapshot();
            Assert.IsNull(current.QualityLevel);
            Assert.IsNull(current.BandwidthKbps);
            Assert.IsNull(current.RoundTripTimeMilliseconds);
            Assert.AreEqual((int?)7900, previous.BandwidthKbps);
        }

        [DataTestMethod]
        [DataRow(1, "Less than 100 Kbps")]
        [DataRow(99, "Less than 100 Kbps")]
        [DataRow(100, "100 Kbps")]
        [DataRow(999, "999 Kbps")]
        [DataRow(1000, "1.00 Mbps")]
        [DataRow(27550, "27.55 Mbps")]
        [DataRow(0, null)]
        [DataRow(-1, null)]
        public void BandwidthFormatting(int value, string? expected)
        {
            Assert.AreEqual(expected, RdpNetworkMetricsFormatter.FormatBandwidth(value));
        }

        [TestMethod]
        public void MissingMetricsAreNotInvented()
        {
            Assert.IsNull(RdpNetworkMetricsFormatter.FormatBandwidth(null));
            Assert.IsNull(RdpNetworkMetricsFormatter.FormatRoundTripTime(null));
            Assert.AreEqual("0 ms", RdpNetworkMetricsFormatter.FormatRoundTripTime(0));
            Assert.IsNull(RdpNetworkMetricsFormatter.FormatRoundTripTime(-1));
        }
    }
}
