using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;

namespace VariableKeywordMatcherTests.Providers
{
    [TestClass()]
    public class ChineseZhCnPinYinMatchProviderTests
    {
        [TestMethod()]
        public void Test1()
        {
            var matcher = new ChineseZhCnPinYinMatchProvider(false);


            {
                string org = "华为云edge";
                var kws = new[] { "eg" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

        }


        [TestMethod()]
        public void FindMatchesTest()
        {
            var matcher = new ChineseZhCnPinYinMatchProvider(false);
            var matcherCase = new ChineseZhCnPinYinMatchProvider(true);
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();
            Assert.IsTrue(matcher.GetProviderName() == nameof(ChineseZhCnPinYinMatchProvider));

            {
                var ret = matcher.DoMatches(new MatchCache(""), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var ret = matcher.DoMatches(new MatchCache("a"), new[] { "" });
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

            {
                var ret = matcher.DoMatches(new MatchCache("大夫"), new[] { "Daif" });
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.HitFlags.All(x => x));
            }

            {
                var ret = matcher.DoMatches(new MatchCache("大夫"), new[] { "Daf" });
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.HitFlags.All(x => x));
            }

            {
                var ret = matcher.DoMatches(new MatchCache("大夫A"), new[] { "DaifUa" });
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.HitFlags.All(x => x));
            }

            {
                var ret = matcher.DoMatches(new MatchCache("大夫B"), new[] { "Dafb" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var ret = matcher.DoMatches(new MatchCache("大夫C"), new[] { "DaifUa" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {

                var sd = new MatchCache("这东西重量有多重？");
                matcher.AppendDescriptions(ref sd);
                Assert.IsTrue(sd.SpellCaches[matcher.GetProviderName()].GetProviderName() == matcher.GetProviderName());
            }

            {
                var kws = new[] { "a" };
                var sd = new MatchCache("abcdefg");
                matcher.AppendDescriptions(ref sd);
                // no Chinese
                Assert.IsTrue(
                    sd.SpellCaches[nameof(ChineseZhCnPinYinMatchProvider)] is SpellCache sc &&
                    sc.Units.All(x => x.Count == 0));
                var ret = matcher.DoMatches(sd, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            var testNoCase = new MatchCache("多重多次，行中Ab, ");
            matcher.AppendDescriptions(ref testNoCase);

            {
                var kws = new[] { "A" };
                var ret = matcher.DoMatches(new MatchCache("啊a"), kws);
                var m = ret.HitFlags;
                Assert.IsTrue(m[0] == true
                              && m[1] == true);
            }
            {
                var kws = new[] { "a" };
                var ret = matcher.DoMatches(new MatchCache("啊a"), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m[0] == true
                              && m[1] == true);
            }
            {
                var kws = new[] { "DUOC" };
                var ret = matcher.DoMatches(new MatchCache(testNoCase.OriginalString), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "DUOCHONG" };
                matcher.AppendDescriptions(ref testNoCase);
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "DUOZHONG", "Hangzhong" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "XingZhongA" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "XingZhonga" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "HangZhongA" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "HangZhonga" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "XINGZhongAb" };
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var ret = matcher.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "A" };
                var ret = matcherCase.DoMatches(new MatchCache("啊a"), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m[0] == true
                              && m[1] == false);
            }
            {
                var kws = new[] { "a" };
                var ret = matcherCase.DoMatches(new MatchCache("啊a"), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m[0] == true
                              && m[1] == true);
            }

            {
                var kws = new[] { "DuoZ" };
                var ret = matcherCase.DoMatches(new MatchCache("多重多次，行中Ab, "), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "DuoC" };
                matcherCase.AppendDescriptions(ref testNoCase);
                var ret = matcherCase.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "Duoz", "HangZ" };
                var ret = matcherCase.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "Duoz", "XingZ" };
                var ret = matcherCase.DoMatches(testNoCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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


            var testCase = new MatchCache("多重多次，行中Ab, ");
            matcherCase.AppendDescriptions(ref testCase);
            {
                var kws = new[] { "XingZhongA" };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "XingZhonga" };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "HangZhongA" };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var kws = new[] { "HangZhonga" };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "xingzhongab" };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "aB", "," };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }



            {
                var kws = new[] { "，", ", " };
                var ret = matcherCase.DoMatches(testCase, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == testNoCase.StringLength
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
                var ret = matcher.DoMatches(new MatchCache("大夫A"), new[] { "Daf", "B" });
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