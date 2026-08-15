using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace VariableKeywordMatcherTests.Providers
{
    [TestClass()]
    public class MatchProviderBaseTests
    {
        [TestMethod()]
        public void DoMatchesTest()
        {
            var matcher1 = new DirectMatchProvider(false);
            var matcher2 = new ChineseZhCnPinYinInitialsMatchProvider(false);
            var test = new MatchCache("多重hello world");
            matcher1.AppendDescriptions(ref test);
            matcher2.AppendDescriptions(ref test);

            {
                bool throwException = false;    
                try
                {
                    var ret = matcher1.DoMatches(new MatchCache("123"), new List<string>() { "dc" });
                    var ret2 = matcher1.DoMatches(new MatchCache("abc"), new List<string>() { "dc" });
                    ret.Merge(ret2);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throwException = true;
                }

                Assert.IsTrue(throwException);
            }

            {
                var ret = matcher1.DoMatches(test, new List<string>() { "dc" });
                var ret2 = matcher2.DoMatches(test, new List<string>() { "dc" });
                ret.Merge(ret2);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "dc");
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
                              && m[10] == false
                              && m[11] == false
                              && m[12] == false);
            }

            {
                var kws = new List<string>() {"dc"};
                var ret = matcher1.DoMatches(test, kws);
                var ret2 = matcher2.DoMatches(test, kws);
                ret.Merge(ret2);
                Assert.IsTrue(ret.Keywords.Count == 1 && ret.Keywords[0] == "dc");
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
                              && m[10] == false
                              && m[11] == false
                              && m[12] == false);
            }


            {
                var ret = matcher1.DoMatches(test, new List<string>() { "wor" });
                var ret2 = matcher2.DoMatches(test, new List<string>() { "Dc" });
                ret.Merge(ret2);
                Assert.IsTrue(ret.IsMatchAllKeywords == true);
                var m = ret.HitFlags;
                Assert.IsTrue(ret.Keywords.Count == 2 && ret.Keywords.Any(x => x == "Dc") && ret.Keywords.Any(x => x == "wor"));
                Assert.IsTrue(m.Count == test.StringLength
                              && m[0] == true
                              && m[1] == true
                              && m[2] == false
                              && m[3] == false
                              && m[4] == false
                              && m[5] == false
                              && m[6] == false
                              && m[7] == false
                              && m[8] == true
                              && m[9] == true
                              && m[10] == true
                              && m[11] == false
                              && m[12] == false);
            }

            {
                var ret = matcher2.DoMatches(test, new List<string>() { "dc" });
                ret.Merge(matcher1.DoMatches(test, new List<string>() { "wor", "abs" }));
                Assert.IsTrue(ret.IsMatchAllKeywords == false);
                Assert.IsTrue(ret.Keywords.Count == 3);
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
                              && m[8] == true
                              && m[9] == true
                              && m[10] == true
                              && m[11] == false
                              && m[12] == false);
            }
        }
    }
}