using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.InitialsMatch;

namespace VariableKeywordMatcherTests.Providers
{
    [TestClass()]
    public class InitialsMatchProviderTests
    {
        [TestMethod()]
        public void FindMatchesTest()
        {
            var matcher = new InitialsMatchProvider(false);
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();
            Assert.IsTrue(matcher.GetProviderName() == nameof(InitialsMatchProvider));

            {
                var ret = matcher.DoMatches(new MatchCache(""), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "   ";
                var str = new MatchCache(org);
                matcher.AppendDescriptions(ref str);
                Assert.IsTrue(str.SpellCaches[matcher.GetProviderName()].GetProviderName() == matcher.GetProviderName());
                matcher.DoMatches(str, new[] { " " });
                Assert.IsTrue(
                    str.SpellCaches[nameof(InitialsMatchProvider)] is SpellCache sc
                    && sc.SkipFlag.All(x => x));
            }

            {
                string org = "abcdefg";
                var str = new MatchCache(org);
                matcher.AppendDescriptions(ref str);
                matcher.AppendDescriptions(ref str);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "a" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "A" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false);
            }

            {
                string org = "abaa a";
                var kws = new[] { " " };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abAAa";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abAA A";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true);
            }

            {
                string org = "abAA A";
                var kws = new[] { "Aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true);
            }

            {
                string org = "a b c d e";
                var kws = new[] { "ab", "cd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == true
                              && m[5] == false
                              && m[6] == true
                              && m[7] == false
                              && m[8] == false);
            }

            {
                string org = "a BC de";
                var kws = new[] { "ab", "cd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a BC de";
                var kws = new[] { "ab", "bc" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a Bc dE ";
                var kws = new[] { "abd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false);
            }


            {
                string org = "a Bc dE ";
                var kws = new[] { "AB", "bd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false);
            }
        }









        [TestMethod()]
        public void FindMatchesTestCase()
        {
            var matcher = new InitialsMatchProvider(true);
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();


            {
                string org = "abcdefg";
                var str = new MatchCache(org);
                matcher.AppendDescriptions(ref str);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "a" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "A" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abaa a";
                var kws = new[] { " " };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abAAa";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abAA A";
                var kws = new[] { "Aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
            }

            {
                string org = "abAA A";
                var kws = new[] { "aA" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true);
            }

            {
                string org = "a b c d e";
                var kws = new[] { "ab", "cd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == true
                              && m[5] == false
                              && m[6] == true
                              && m[7] == false
                              && m[8] == false);
            }

            {
                string org = "a BC de";
                var kws = new[] { "ab", "cd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a BC de";
                var kws = new[] { "ab", "bc" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a Bc dE ";
                var kws = new[] { "abd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a Bc dE ";
                var kws = new[] { "aBd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false);
            }

            {
                string org = "a Bc dE ";
                var kws = new[] { "AB", "bd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "a Bc dE ";
                var kws = new[] { "aB", "Bd" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false);
            }



            {
                string org = "a Bc dE ";
                var kws = new[] { "aB", "Bd", "K" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == false
                              && m[2] == true
                              && m[3] == false
                              && m[4] == false
                              && m[5] == true
                              && m[6] == false
                              && m[7] == false);
            }
        }
    }
}