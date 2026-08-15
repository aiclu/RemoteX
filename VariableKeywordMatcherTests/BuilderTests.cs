using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;
using VariableKeywordMatcher.Provider.DiscreteMatch;
using VariableKeywordMatcher.Provider.InitialsMatch;

#if !NET45
using VariableKeywordMatcher.Provider.JapaneseRomaji;
#endif

namespace VariableKeywordMatcherTests
{
    [TestClass()]
    public class BuilderTests
    {
        [TestMethod()]
        public void GetAvailableProviderTypesTest()
        {
            var ts = Builder.GetAvailableProviderNames();
            Assert.IsTrue(ts.Any());
        }

        [TestMethod()]
        public void GetProviderDescriptionTest()
        {
            {
                var dsp1 = Builder.GetProviderDescription(DirectMatchProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(DirectMatchProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
            {
                var dsp1 = Builder.GetProviderDescription(InitialsMatchProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(InitialsMatchProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
            {
                var dsp1 = Builder.GetProviderDescription(DiscreteMatchProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(DiscreteMatchProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
            {
                var dsp1 = Builder.GetProviderDescription(ChineseZhCnPinYinMatchProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(ChineseZhCnPinYinMatchProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
            {
                var dsp1 = Builder.GetProviderDescription(ChineseZhCnPinYinInitialsMatchProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(ChineseZhCnPinYinInitialsMatchProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
#if !NET45
            {
                var dsp1 = Builder.GetProviderDescription(JapaneseRomajiProvider.GetName());
                var dsp2 = Builder.GetProviderDescriptionEn(JapaneseRomajiProvider.GetName());
                Assert.IsTrue(!string.IsNullOrEmpty(dsp1));
                Assert.IsTrue(!string.IsNullOrEmpty(dsp2));
            }
#endif
        }

        [TestMethod()]
        public void BuildTest()
        {
            {
                var matcher = Builder.Build(new[] { DiscreteMatchProvider.GetName() }, true);
                Assert.IsTrue(matcher.IsCaseSensitive);
                Assert.IsTrue(matcher.ProviderTypes.Count == 1);
                Assert.IsTrue(matcher.ProviderTypes[0] == DiscreteMatchProvider.GetName());
            }
            {
                var matcher = Builder.Build(new[] { DiscreteMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, false);
                Assert.IsTrue(matcher.IsCaseSensitive == false);
                Assert.IsTrue(matcher.ProviderTypes.Count == 2);
                Assert.IsTrue(matcher.ProviderTypes.Any(x => x == DiscreteMatchProvider.GetName()));
                Assert.IsTrue(matcher.ProviderTypes.Any(x => x == ChineseZhCnPinYinMatchProvider.GetName()));
            }
#if !NET45
            {
                var matcher = Builder.Build(new[] { JapaneseRomajiProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, true);
                Assert.IsTrue(matcher.IsCaseSensitive == true);
                Assert.IsTrue(matcher.ProviderTypes.Count == 2);
                Assert.IsTrue(matcher.ProviderTypes.Any(x => x == JapaneseRomajiProvider.GetName()));
                Assert.IsTrue(matcher.ProviderTypes.Any(x => x == ChineseZhCnPinYinInitialsMatchProvider.GetName()));
            }
#endif
        }
    }
}