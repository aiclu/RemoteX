using _1RM.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.ViewModel
{
    [TestClass]
    public class FluentPreferencesTests
    {
        [TestMethod]
        public void MissingPreferencesDefaultToClassicSystemAndList()
        {
            var options = new FluentPreferences();
            Assert.IsFalse(options.Enabled);
            Assert.AreEqual("System", options.Theme);
            Assert.AreEqual("List", options.View);
        }

        [DataTestMethod]
        [DataRow("Light", false, false)]
        [DataRow("Light", true, false)]
        [DataRow("Dark", true, true)]
        [DataRow("Dark", false, true)]
        [DataRow("System", true, false)]
        [DataRow("System", false, true)]
        [DataRow(null, false, true)]
        [DataRow("unknown", true, false)]
        public void ThemeResolution(string? mode, bool systemLight, bool expected)
        {
            Assert.AreEqual(expected, FluentPreferences.UseDark(mode, systemLight));
        }

        [DataTestMethod]
        [DataRow(null, "List")]
        [DataRow("invalid", "List")]
        [DataRow("List", "List")]
        [DataRow("Card", "Card")]
        [DataRow("Tree", "Tree")]
        public void ViewNormalization(string? value, string expected)
        {
            Assert.AreEqual(expected, FluentPreferences.NormalizeView(value));
        }

        [TestMethod]
        public void DisablingPreviewPreservesItsIndependentPreferences()
        {
            var options = new FluentPreferences { Enabled = true, Theme = "Dark", View = "Tree" };
            options.Enabled = false;
            options.Enabled = true;
            Assert.AreEqual("Dark", options.Theme);
            Assert.AreEqual("Tree", options.View);
        }

        [TestMethod]
        public void PreviewChangesNeverOverwriteClassicView()
        {
            var classic = "Card";
            var options = new FluentPreferences { Enabled = true };
            Assert.AreEqual("List", options.ResolveView(classic));
            options.RememberView("Tree", value => classic = value);
            Assert.AreEqual("Card", classic);
            options.Enabled = false;
            Assert.AreEqual("Card", options.ResolveView(classic));
            options.RememberView("List", value => classic = value);
            Assert.AreEqual("List", classic);
            options.Enabled = true;
            Assert.AreEqual("Tree", options.ResolveView(classic));
        }
    }
}
