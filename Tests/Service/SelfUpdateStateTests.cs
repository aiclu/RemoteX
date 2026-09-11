using System;
using System.IO;
using _1RM;
using _1RM.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Service
{
    [TestClass]
    [DoNotParallelize]
    public class SelfUpdateStateTests
    {
        [TestMethod]
        public void PendingUpdateState_RecognizesRollbackFailure()
        {
            var state = new PendingUpdateState(
                "9.9.9",
                "rollback-failed",
                "rollback failed",
                "C:\\backup",
                0);

            Assert.IsTrue(state.IsFailure);
            Assert.AreEqual("9.9.9", state.TargetVersion);
            Assert.AreEqual("C:\\backup", state.BackupPath);
        }

        [TestMethod]
        public void PendingUpdateState_RoundTripsAndClearsAfterInstalledVersionMatches()
        {
            var root = Path.Combine(Path.GetTempPath(), $"RemoteX-self-update-test-{Guid.NewGuid():N}");
            var previousPaths = AppPathHelper.Instance;
            try
            {
                AppPathHelper.Instance = new AppPathHelper(root, root);
                var backup = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $".remotex-update-backup-{Guid.NewGuid():N}");
                Directory.CreateDirectory(backup);
                File.WriteAllText(Path.Combine(backup, "RemoteX.exe"), "old");

                SelfUpdateService.WritePendingUpdateState(
                    "9.9.9",
                    "rolled-back",
                    "copy failed",
                    backup,
                    25);

                var pending = SelfUpdateService.TryReadPendingUpdateState();
                Assert.IsNotNull(pending);
                Assert.AreEqual("9.9.9", pending!.TargetVersion);
                Assert.AreEqual("rolled-back", pending.Phase);
                Assert.AreEqual("copy failed", pending.Message);
                Assert.AreEqual(25, pending.Progress);
                Assert.IsNotNull(SelfUpdateService.CheckPendingUpdate());

                SelfUpdateService.WritePendingUpdateState(AppVersion.Version, "swapped", backupPath: backup, progress: 100);
                Assert.IsNull(SelfUpdateService.CheckPendingUpdate());
                Assert.IsFalse(File.Exists(SelfUpdateService.PendingUpdateStatePath));
                Assert.IsFalse(Directory.Exists(backup));
            }
            finally
            {
                AppPathHelper.Instance = previousPaths;
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
