using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Service;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests.ViewModel.Configuration
{
    [TestClass]
    public class ConfigurationViewModelTests
    {
        [TestMethod]
        public void ConfigurationViewModelTest()
        {
            TestInit.Init();
            var databasePath = Path.Combine(AppPathHelper.Instance.BaseDirPath, "isolated.db");
            var configuration = new _1RM.Service.Configuration {
                SqliteDatabasePath = databasePath, DatabaseCheckPeriod = 73, DatabaseReconnectPeriod = 121
            };
            configuration.PinnedTags.Add("unit-test");
            configuration.Theme.Fluent.Enabled = true;
            var service = new ConfigurationService(new KeywordMatchService(), configuration);
            service.Save();
            Assert.IsTrue(File.Exists(AppPathHelper.Instance.ProfileJsonPath));
            var loaded = _1RM.Service.Configuration.Load(AppPathHelper.Instance.ProfileJsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(databasePath, loaded!.SqliteDatabasePath);
            Assert.AreEqual(73, loaded.DatabaseCheckPeriod);
            Assert.AreEqual(121, loaded.DatabaseReconnectPeriod);
            CollectionAssert.AreEqual(configuration.PinnedTags, loaded.PinnedTags);
            Assert.IsTrue(loaded.Theme.Fluent.Enabled);
            var reloadedService = ConfigurationService.LoadFromAppPath(new KeywordMatchService());
            Assert.AreEqual(databasePath, reloadedService.LocalDataSource.Path);
            Assert.IsFalse(File.Exists(databasePath), "Saving configuration must not create/open the database.");
        }
    }
}
