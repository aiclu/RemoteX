using System;
using System.Runtime.InteropServices;
using _1RM.View.Host.ProtocolHosts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.ViewModel
{
    [TestClass]
    public class ConnectionInfoPresentationTests
    {
        [DataTestMethod]
        [DataRow(true, 1, true, 0)]
        [DataRow(true, 1, false, 1)]
        [DataRow(true, 0, true, 1)]
        [DataRow(true, 2, true, 1)]
        [DataRow(false, 1, true, 1)]
        public void NativeRoutingHonorsLifetimeConnectionAndResult(bool available, int connected, bool accepted, int expectedFallbacks)
        {
            var fallbacks = 0;
            var reads = 0;
            var calls = 0;
            ConnectionInfoPresentation.Show(
                () => ConnectionInfoPresentation.TryShowRdp(() => available,
                    () => { reads++; return connected; },
                    () => { calls++; return accepted; }),
                () => fallbacks++, _ => Assert.Fail("Unexpected exception"));
            Assert.AreEqual(expectedFallbacks, fallbacks);
            Assert.AreEqual(available ? 1 : 0, reads);
            Assert.AreEqual(available && connected == 1 ? 1 : 0, calls);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ComFailureInStateOrPresentationFallsBack(bool failOnRead)
        {
            var errors = 0;
            var fallbacks = 0;
            ConnectionInfoPresentation.Show(
                () => ConnectionInfoPresentation.TryShowRdp(() => true,
                    () => failOnRead ? throw new COMException() : 1,
                    () => throw new COMException()),
                () => fallbacks++, _ => errors++);
            Assert.AreEqual(1, errors);
            Assert.AreEqual(1, fallbacks);
        }

        [TestMethod]
        public void DefaultNonRdpRouteUsesSnapshot()
        {
            var fallbacks = 0;
            ConnectionInfoPresentation.Show(() => false, () => fallbacks++, _ => Assert.Fail());
            Assert.AreEqual(1, fallbacks);
        }

        [TestMethod]
        public void SnapshotFailureReachesLastResortBoundary()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                ConnectionInfoPresentation.Show(() => false,
                    () => throw new InvalidOperationException(), _ => Assert.Fail()));
        }
    }
}
