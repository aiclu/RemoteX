using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;

namespace VariableKeywordMatcherTests.Providers
{
    [TestClass()]
    public class ChineseZhCnPinYinInitialMatchProviderTests
    {
        [TestMethod()]
        public void FindMatchesTest()
        {
            var matcher = new ChineseZhCnPinYinInitialsMatchProvider(false);
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();
            Assert.IsTrue(matcher.GetProviderName() == nameof(ChineseZhCnPinYinInitialsMatchProvider));

            {
                var ret = matcher.DoMatches(new MatchCache(""), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var ret = matcher.DoMatches(new MatchCache("a"), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var kws = new[] { "a" };
                var sd = new MatchCache("abcdefg");
                matcher.AppendDescriptions(ref sd);
                // no Chinese
                Assert.IsTrue(
                    sd.SpellCaches[nameof(ChineseZhCnPinYinInitialsMatchProvider)] is SpellCache sc
                    && sc.Units.All(x => x.Count == 0));
                var ret = matcher.DoMatches(sd, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "a" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
            }

            {
                string org = "abcdefg";
                var kws = new[] { "A" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
            }

            var test = new MatchCache("多重多次，行中Ab, ");
            matcher.AppendDescriptions(ref test);
            Assert.IsTrue(test.SpellCaches[matcher.GetProviderName()].GetProviderName() == matcher.GetProviderName());


            {
                var ret = matcher.DoMatches(new MatchCache("大夫"), new[] { "df" });
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.HitFlags.All(x => x));
            }

            {
                var ret = matcher.DoMatches(new MatchCache("大夫"), new[] { "tf" });
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.HitFlags.All(x => x));
            }

            {
                var kws = new[] { "DZ" };
                var ret = matcher.DoMatches(new MatchCache("多重多次，行中Ab, "), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "DC" };
                matcher.AppendDescriptions(ref test);
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "Dz", "HZ" };
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "XZA" };
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == true
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "XZAb" };
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "aB", "," };
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == true
                              && m[10] == false);
            }



            {
                var kws = new[] { "，", ", " };
                var ret = matcher.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == true
                              && m[10] == true);
            }
        }


        [TestMethod()]
        public void FindMatchesTestCase()
        {
            var matcherCase = new ChineseZhCnPinYinInitialsMatchProvider(true);
            var dspen = matcherCase.GetProviderDescriptionEn();
            var dsp = matcherCase.GetProviderDescription();

            {
                var ret = matcherCase.DoMatches(new MatchCache(""), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var ret = matcherCase.DoMatches(new MatchCache("a"), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "a" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
            }

            {
                string org = "abcdefg";
                var kws = new[] { "A" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
                Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
            }

            var test = new MatchCache("多重多次，行中Ab, ");
            matcherCase.AppendDescriptions(ref test);

            {
                var kws = new[] { "DZ" };
                var ret = matcherCase.DoMatches(new MatchCache("多重多次，行中Ab, "), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "DC" };
                matcherCase.AppendDescriptions(ref test);
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "Dz", "HZ" };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "XZA" };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == true
                              && m[8] == false
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "XZAb" };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == true
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false
                              && m[10] == false);
            }

            {
                var kws = new[] { "XZab" };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "aB", "," };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }



            {
                var kws = new[] { "，", ", " };
                var ret = matcherCase.DoMatches(test, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == true
                              && m[10] == true);
            }


            {
                var ret = matcherCase.DoMatches(new MatchCache("大夫A"), new[] { "DF", "B" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == 3
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false);
            }
        }
    }
}