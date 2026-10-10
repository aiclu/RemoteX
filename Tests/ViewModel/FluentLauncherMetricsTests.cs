using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Service;

namespace Tests.ViewModel
{
    [TestClass]
    public class FluentLauncherMetricsTests
    {
        [TestMethod]
        public void ClassicSizesRemainFixed()
        {
            var metrics = new FluentLauncherMetrics(false, 24, 320, 240);
            Assert.AreEqual(400, metrics.Width);
            Assert.AreEqual(40, metrics.RowHeight);
            Assert.AreEqual(46, metrics.SearchHeight);
            Assert.AreEqual(34, metrics.ActionHeight);
        }
        [DataTestMethod]
        [DataRow(-1, 46.0)]
        [DataRow(0, 46.0)]
        [DataRow(1, 98.0)]
        [DataRow(8, 406.0)]
        [DataRow(10000, 406.0)]
        public void FluentShowsAtMostEightRows(int count, double height)
        {
            Assert.AreEqual(height, new FluentLauncherMetrics(true, 12, 1920, 1080).HeightFor(count));
        }
        [TestMethod]
        public void LargerFontsIncreaseBothRowTypesAndSearchArea()
        {
            var metrics = new FluentLauncherMetrics(true, 24, 1920, 1080);
            Assert.IsTrue(metrics.RowHeight > 40);
            Assert.IsTrue(metrics.ActionHeight > 34);
            Assert.IsTrue(metrics.SearchHeight > 46);
            Assert.AreEqual(metrics.SearchHeight + metrics.ListPadding + metrics.ActionHeight * 3, metrics.HeightFor(3, true));
        }
        [TestMethod]
        public void SmallWorkAreaBoundsLauncher()
        {
            var metrics = new FluentLauncherMetrics(true, 24, 320, 300);
            Assert.AreEqual(280, metrics.Width);
            Assert.AreEqual(200, metrics.HeightFor(100));
        }
        [TestMethod]
        public void InvalidFontFallsBackToDefault()
        {
            Assert.AreEqual(44, new FluentLauncherMetrics(true, double.NaN, 1920, 1080).RowHeight);
        }
        [TestMethod]
        public void FiltersUseOneLineOnlyAndNeverChangeActionOrClassicHeight()
        {
            var metrics = new FluentLauncherMetrics(true, 13, 1920, 1080);
            Assert.AreEqual(metrics.HeightFor(8) + metrics.FilterHeight, metrics.HeightFor(8, hasFilters: true));
            Assert.AreEqual(metrics.HeightFor(6, true), metrics.HeightFor(6, true, true));
            var classic = new FluentLauncherMetrics(false, 20, 1920, 1080);
            Assert.AreEqual(classic.HeightFor(8), classic.HeightFor(8, hasFilters: true));
            Assert.AreEqual(366.0, classic.HeightFor(8));
        }
        [TestMethod]
        public void MetricsRespectFontLineSpacingAndWorkingAreaWithFilters()
        {
            var tall = new FluentLauncherMetrics(true, 20, 340, 340, 1.8);
            Assert.AreEqual(300.0, tall.Width);
            Assert.IsTrue(tall.RowHeight >= 36 + 33 + 12);
            Assert.AreEqual(240.0, tall.HeightFor(5000, hasFilters: true));
        }
        [TestMethod]
        public void DialogKeepsWorkAreaMarginAndSpaceForActions()
        {
            var metrics = new FluentDialogMetrics(560, 400, 360);
            Assert.AreEqual(376, metrics.Width);
            Assert.AreEqual(336, metrics.HeightLimit);
            Assert.AreEqual(192, metrics.BodyMaxHeight);
            Assert.IsTrue(metrics.FieldMaxWidth < metrics.Width);
        }
        [TestMethod]
        public void DialogHasPositiveBoundsOnTinyWorkArea()
        {
            var metrics = new FluentDialogMetrics(double.NaN, 10, 10);
            Assert.AreEqual(1, metrics.Width);
            Assert.AreEqual(1, metrics.BodyMaxHeight);
        }
    }
}
