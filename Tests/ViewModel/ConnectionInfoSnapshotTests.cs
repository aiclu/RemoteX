using System;
using _1RM.View.Host;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.ViewModel
{
    [TestClass]
    public class ConnectionInfoSnapshotTests
    {
        [TestMethod]
        public void CopyTextContainsSnapshotRowsWithoutAddingCredentialFields()
        {
            var snapshot = new ConnectionInfoSnapshot(
                "Connection information - demo",
                "Connection status: Connected",
                DateTimeOffset.UtcNow,
                new[]
                {
                    new ConnectionInfoSection(
                        "Session details",
                        new[]
                        {
                            new ConnectionInfoRow("Hostname", "server.example"),
                            new ConnectionInfoRow("User", "alice"),
                        }),
                });

            Assert.IsTrue(snapshot.CopyText.Contains("server.example", StringComparison.Ordinal));
            Assert.IsTrue(snapshot.CopyText.Contains("alice", StringComparison.Ordinal));
            Assert.IsFalse(snapshot.CopyText.Contains("Password", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(snapshot.CopyText.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ConnectionInfoSnapshotKeepsUnavailableValuesVisible()
        {
            var snapshot = new ConnectionInfoSnapshot(
                "Connection information",
                "Unavailable",
                DateTimeOffset.UtcNow,
                new[]
                {
                    new ConnectionInfoSection(
                        "Network details",
                        new[] { new ConnectionInfoRow("Round-trip time", "Unavailable") }),
                });

            Assert.AreEqual("Unavailable", snapshot.Sections[0].Rows[0].Value);
            Assert.IsTrue(snapshot.CopyText.Contains("Round-trip time: Unavailable", StringComparison.Ordinal));
        }
    }
}
