using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Utils;

namespace Tests.Service
{
    [TestClass]
    public class DataServiceTests
    {
        private static void Success(Result result) => Assert.IsTrue(result.IsSuccess, result.ErrorInfo);

        [TestMethod]
        public void DataServiceTest()
        {
            TestInit.Init();
            UnSafeStringEncipher.Init("RemoteX.UnitTests.NonProduction");
            var path = Path.Combine(TestInit.CreateDirectory(), "test.db");
            var database = new DapperDatabase("Unit test", DatabaseType.Sqlite);
            try
            {
                Success(database.OpenNewConnection(DbExtensions.GetSqliteConnectionString(path)));
                Success(database.InitTables());
                ProtocolBase[] servers = {
                    new RDP { DisplayName = "RDP", Address = "192.0.2.1", UserName = "demo", Password = "rdp-password", GatewayPassword = "gateway-password" },
                    new SSH { DisplayName = "SSH", Address = "192.0.2.2", UserName = "demo", PrivateKey = "test-private-key" },
                    new VNC { DisplayName = "VNC", Password = "vnc-password" },
                    new LocalApp { DisplayName = "App", ExePath = "example.exe" }
                };
                foreach (var original in servers)
                {
                    var server = original;
                    server.EncryptToDatabaseLevel();
                    Success(database.AddServer(ref server));
                    Assert.IsFalse(server.IsTmpSession());
                }
                var loaded = database.GetServers();
                Success(loaded);
                Assert.AreEqual(4, loaded.Items.Count);
                var ssh = loaded.Items.OfType<SSH>().Single();
                Assert.AreEqual("SSH", ssh.DisplayName);
                Assert.AreEqual("192.0.2.2", ssh.Address);
                Assert.AreEqual("", ssh.Password, "Private-key and password authentication are mutually exclusive.");
                Assert.AreNotEqual("test-private-key", ssh.PrivateKey);
                ssh.DecryptToConnectLevel();
                Assert.AreEqual("test-private-key", ssh.PrivateKey);
                ssh.EncryptToDatabaseLevel();
                var encryptedKey = ssh.PrivateKey;
                ssh.EncryptToDatabaseLevel();
                Assert.AreEqual(encryptedKey, ssh.PrivateKey, "Encryption must be idempotent.");
                ssh.DecryptToConnectLevel();
                ssh.DisplayName = "Updated SSH";
                ssh.Password = "updated-password";
                Assert.AreEqual("", ssh.PrivateKey);
                ssh.EncryptToDatabaseLevel();
                Assert.AreNotEqual("updated-password", ssh.Password);
                Success(database.UpdateServer(ssh));
                var rdp = loaded.Items.OfType<RDP>().Single();
                rdp.DecryptToConnectLevel();
                Assert.AreEqual("rdp-password", rdp.Password);
                Assert.AreEqual("gateway-password", rdp.GatewayPassword);
                rdp.Address = "192.0.2.10";
                rdp.EncryptToDatabaseLevel();
                Success(database.UpdateServer(new ProtocolBase[] { rdp, ssh }));
                database.CloseConnection();
                Success(database.OpenNewConnection(DbExtensions.GetSqliteConnectionString(path)));
                var reloaded = database.GetServers();
                Success(reloaded);
                Assert.AreEqual(4, reloaded.Items.Count);
                var updated = reloaded.Items.OfType<SSH>().Single();
                updated.DecryptToConnectLevel();
                Assert.AreEqual("Updated SSH", updated.DisplayName);
                Assert.AreEqual("updated-password", updated.Password);
                Assert.AreEqual("192.0.2.10", reloaded.Items.OfType<RDP>().Single().Address);
                Success(database.DeleteServer(new[] { rdp.Id }));
                Assert.AreEqual(3, database.GetServers().Items.Count);
                Success(database.DeleteServer(new[] { rdp.Id, ssh.Id }));
                Assert.AreEqual(2, database.GetServers().Items.Count);
                Success(database.AddServer(new ProtocolBase[] { rdp, ssh }));
                Assert.AreEqual(4, database.GetServers().Items.Count);
            }
            finally { database.CloseConnection(); }
        }
    }
}
