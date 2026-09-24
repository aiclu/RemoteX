using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Utils;
using static Shawn.Utils.VersionHelper;

namespace Tests.Utils
{
    [TestClass()]
    public class VersionHelperTests
    {

        [TestMethod()]
        public void FromStringTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = Version.FromString(v1.ToString());
            Assert.IsTrue(v1 == v2);
        }

        [TestMethod()]
        public void CompareTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = new Version(0, 6, 1, 0);
            var v3 = new Version(0, 6, 1, 1);
            var v4 = new Version(0, 6, 2, 0);
            var v5 = new Version(0, 7, 1, 0);
            var v6 = new Version(1, 6, 1, 0);
            var v7 = new Version(0, 6, 1, 0, "alpha");
            var v8 = new Version(0, 6, 1, 0, "beta");
            var v9 = new Version(0, 6, 1, 0, "beta2");
            Assert.IsTrue(v1 == v2);
            Assert.IsTrue(v1 >= v2);
            Assert.IsTrue(v3 > v2);
            Assert.IsTrue(v3 != v2);
            Assert.IsTrue(v2 < v3);
            Assert.IsTrue(v3 >= v2);
            Assert.IsTrue(v4 > v3);
            Assert.IsTrue(v3 < v4);
            Assert.IsTrue(v3 <= v4);
            Assert.IsTrue(v5 > v4);
            Assert.IsTrue(v6 > v5);
            Assert.IsTrue(v6 > v7);
            Assert.IsTrue(v8 > v7);
            Assert.IsTrue(v9 > v8);
            Assert.IsTrue(v1 > v9);
            Assert.IsTrue(v9 != v8);
            Assert.IsTrue(Shawn.Utils.VersionHelper.Version.Compare(v1, v3) == true);
            Assert.IsTrue(Shawn.Utils.VersionHelper.Version.Compare(v9, v1) == true);
        }


        [TestMethod()]
        public void VersionHelperTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = new Version(0, 6, 2, 0);
            var v3 = new Version(0, 7, 1, 0);
            const string url = "https://example.invalid/releases";
            var result = DefaultCheckMethod($"latest version: {v2}", url, v1, null);
            Assert.IsTrue(result.NewerPublished);
            Assert.AreEqual(v2.ToString(), result.NewerVersion);
            Assert.AreEqual(url, result.NewerUrl);
            Assert.IsFalse(DefaultCheckMethod($"latest version: {v2}", url, v1, v3).NewerPublished);
            Assert.IsFalse(DefaultCheckMethod($"latest version: {v2}", url, v1, v2).NewerPublished);
            Assert.IsTrue(DefaultCheckMethod($"latest version: {v3}", url, v1, v2).NewerPublished);
            CheckAsync(v1, v2, true);
            CheckAsync(v3, v2, false);
        }

        private static void CheckAsync(Version current, Version published, bool expected)
        {
            // Serve deterministic release metadata on loopback only; no real update endpoint is contacted.
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                var response = System.Threading.Tasks.Task.Run(async () =>
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                    var body = System.Text.Encoding.UTF8.GetBytes($"latest version: {published}\n");
                    var header = System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header);
                    await stream.WriteAsync(body);
                });
                var checker = new VersionHelper(current, new[] { $"http://127.0.0.1:{port}/" }, new[] { "https://example.invalid/releases" });
                using var received = new ManualResetEventSlim();
                CheckUpdateResult? reported = null;
                checker.OnNewVersionRelease += r => { reported = r; received.Set(); };
                checker.CheckUpdateAsync();
                Assert.IsTrue(response.Wait(5000), "Loopback metadata request did not finish.");
                Assert.AreEqual(expected, received.Wait(expected ? 5000 : 300));
                if (expected)
                {
                    Assert.AreEqual(published.ToString(), reported!.Value.NewerVersion);
                    Assert.AreEqual("https://example.invalid/releases", reported.Value.NewerUrl);
                }
            }
            finally { listener.Stop(); }
        }
    }
}
