using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Model;
#if !NET45
using VariableKeywordMatcher.Provider.JapaneseRomaji;

namespace KeywordsShootingTests.Providers
{
    [TestClass()]
    public class JapaneseRomajiProviderTests
    {
        [TestMethod()]
        public void FindMatchesTest()
        {
            var matcher = new JapaneseRomajiProvider(false);
            Assert.IsTrue(matcher.GetProviderName() == nameof(JapaneseRomajiProvider));
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();

            {
                {
                    string str = "abcdefg";
                    var kws = new[] { "a" };
                    var ret = matcher.DoMatches(new MatchCache(str), kws);
                    Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                    Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                }

                {
                    string str = "abcdefg";
                    var kws = new[] { "A" };
                    var ret = matcher.DoMatches(new MatchCache(str), kws);
                    Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                    Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
                }
            }


            var org = new MatchCache("こんにちは, 世界A");
            matcher.AppendDescriptions(ref org);
            Assert.IsTrue(org.SpellCaches[matcher.GetProviderName()].GetProviderName() == matcher.GetProviderName());

            {
                var kws = new[] { "kon" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }

            {
                var kws = new[] { "konaa" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == true);
            }

            {
                var kws = new[] { "konaA" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == true);
            }


            {
                var kws = new[] { "sk" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "se" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "skai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "skaia" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == true);
            }

            {
                var kws = new[] { "," };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }


            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "konse" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "kon", "se" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "kon", "," };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }

            {
                var kws = new[] { "chi" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var kws = new[] { "kai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "konkai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "kon", "kai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }
        }



        [TestMethod()]
        public void FindMatchesTestCase()
        {
            var matcher = new JapaneseRomajiProvider(true);
            Assert.IsTrue(matcher.GetProviderName() == nameof(JapaneseRomajiProvider));
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();

            {
                {
                    string str = "abcdefg";
                    var kws = new[] { "a" };
                    var ret = matcher.DoMatches(new MatchCache(str), kws);
                    Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                    Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                }

                {
                    string str = "abcdefg";
                    var kws = new[] { "A" };
                    var ret = matcher.DoMatches(new MatchCache(str), kws);
                    Assert.IsTrue(ret.IsMatchAllKeywords == false); // false by no Chinese char
                    Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
                }
            }


            var org = new MatchCache("こんにちは, 世界A");
            matcher.AppendDescriptions(ref org);

            Assert.IsTrue(org.SpellCaches.First().Value.GetProviderName() == matcher.GetProviderName());


            {
                var kws = new[] { "kon" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }

            {
                var kws = new[] { "konaa" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "konaA" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == true);
            }


            {
                var kws = new[] { "sk" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "se" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "skai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            {
                var kws = new[] { "skaia" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "skaiA" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == true);
            }

            {
                var kws = new[] { "," };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }


            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "konse" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "kon", "se" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == false
                              && m[6] == false
                              && m[7] == true
                              && m[8] == true
                              && m[9] == false);
            }

            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "kon", "," };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }

            {
                var kws = new[] { "chi" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }


            {
                var kws = new[] { "kai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "konkai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                var kws = new[] { "kon", "kai" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            matcher.AppendDescriptions(ref org);
            {
                var kws = new[] { "kon", "," , "ABC" };
                var ret = matcher.DoMatches(org, kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false
                              && m[8] == false
                              && m[9] == false);
            }
        }
    }
}
#endif