using System;
using System.IO;
using _1RM;
using _1RM.Model;
using _1RM.Service;
using _1RM.Service.DataSource;
using Shawn.Utils.Interface;

namespace Tests
{
    public static class TestInit
    {
        public static string CreateDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "RemoteX.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void Init()
        {
            var root = CreateDirectory();
            AppPathHelper.Instance = new AppPathHelper(root, root);
            var configuration = new Configuration { SqliteDatabasePath = Path.Combine(root, "test.db") };
            var configurationService = new ConfigurationService(new KeywordMatchService(), configuration);
            var dataSource = new DataSourceService();
            IoC.GetByType = (type, key) =>
            {
                if (type == typeof(ILanguageService) || type == typeof(LanguageService) || type == typeof(MockLanguageService))
                    return new MockLanguageService();
                if (type == typeof(_1RM.Service.Configuration))
                    return configuration;
                if (type == typeof(_1RM.Service.ConfigurationService))
                    return configurationService;
                if (type == typeof(DataSourceService))
                    return dataSource;
                return null;
            };
        }
    }
}
