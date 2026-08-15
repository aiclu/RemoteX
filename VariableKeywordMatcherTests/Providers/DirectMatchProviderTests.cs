using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace VariableKeywordMatcherTests.Providers
{
    [TestClass()]
    public class DirectMatchProviderTests
    {
        [TestMethod()]
        public void DoMatchesTest()
        {
            var matcher = new DirectMatchProvider(false);
            var matcherCase = new DirectMatchProvider(true);
            Assert.IsTrue(matcher.GetProviderName() == nameof(DirectMatchProvider));
            var dspen = matcher.GetProviderDescriptionEn();
            var dsp = matcher.GetProviderDescription();

            {
                var ret = matcher.DoMatches(new MatchCache(""), new[] { "" });
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abcdefg";
                var str = new MatchCache(org);
                matcher.AppendDescriptions(ref str);
                matcherCase.AppendDescriptions(ref str);
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
                string org = "abcdefg";
                var kws = new[] { "a" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "a");
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false);
            }

            {
                string org = "abcdefg";
                var kws = new[] { "A" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "A");
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                var m = ret.HitFlags;
                Assert.IsTrue(m.All(x => x == false));
            }

            {
                string org = "abcdefgabcdefg";
                var kws = new[] { "defg", "abc" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m.All(x => x == true));
            }

            {
                string org = "abcDefgabcdefg";
                var kws = new[] { "Defg", "abc" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && !m.All(x => x == true));
            }

            {
                string org = "abaaa";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true);
            }
            {
                string org = "abaa a";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == true
                              && m[3] == true
                              && m[4] == false
                              && m[5] == false);
            }
            {
                string org = "abaa a";
                var kws = new[] { " " };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == false
                              && m[3] == false
                              && m[4] == true
                              && m[5] == false);
            }
            {
                string org = "abAAa";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == true
                              && m[3] == true
                              && m[4] == true);
            }

            {
                string org = "abAA a";
                var kws = new[] { "aa" };
                var ret = matcher.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == true
                              && m[3] == true
                              && m[4] == false
                              && m[5] == false);
            }

            {
                string org = "abAA a";
                var kws = new[] { "aA" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
            }

            {
                string org = "abAA a";
                var kws = new[] { "AA" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == false
                              && m[1] == false
                              && m[2] == true
                              && m[3] == true
                              && m[4] == false
                              && m[5] == false);
            }



            {
                string org = "abAA a";
                var kws = new[] { "ab", "AB" };
                var ret = matcherCase.DoMatches(new MatchCache(org), kws);
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                var m = ret.HitFlags;
                Assert.IsTrue(m.Count == org.Length
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false);
            }
        }
    }
}