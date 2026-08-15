using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace VariableKeywordMatcherTests
{
    [TestClass()]
    public partial class MatcherTests
    {
        [TestMethod()]
        public void MatchTest()
        {
            bool isException = false;
            try
            {
                var m = Builder.Build(new string[] { });
                var c = m.CreateStringCache("test");
            }
            catch (Exception e)
            {
                isException = true;
            }
            Assert.IsTrue(isException);
        }

        [TestMethod()]
        public void MatchTest_String_SingleKeyword()
        {
            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, "llow").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "llOW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "X").IsMatchAllKeywords == false);
            }

            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, "llow").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "llOW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " nH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "X").IsMatchAllKeywords == false);
            }


            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, true);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, "llow").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "lloW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " nH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "lD nh").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "lD NH").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "X").IsMatchAllKeywords == false);
            }


            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, "llow").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "llOW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "SHIJIE").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, " nH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld NIHA").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "ld NIHAo").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, "X").IsMatchAllKeywords == false);
            }
        }

        [TestMethod()]
        public void MatchTest_String_MultiKeywords()
        {
            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, new[] { "llow", "NH" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "NH" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "nh", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
            }


            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, true);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, new[] { "llow", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "世界" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "nh", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
            }

            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";

                Assert.IsTrue(matcher.Match(str, new[] { "llow", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "llOW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "SHIJIE", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "NI", "hao", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "NI", "hao", "w" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "a", " " }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { "a" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(str, new[] { " " }).IsMatchAllKeywords == true);
            }
        }

        [TestMethod()]
        public void MatchTest_MatchCache_SingleKeyword()
        {
            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";
                var cache = matcher.CreateStringCache(str);

                Assert.IsTrue(matcher.Match(cache, "llow").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "llOW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, " NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, " nH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "ld nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "ld NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "a").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "X").IsMatchAllKeywords == false);
            }


            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, true);
                string str = "HelloWorld 你好世界！";
                var cache = matcher.CreateStringCache(str);

                Assert.IsTrue(matcher.Match(cache, "llow").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "lloW").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "世界").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "！").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, " NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, " nH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "ld nh").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "ld NH").IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, "lD nh").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "lD NH").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "!").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "He l").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "  ").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "啊").IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, "X").IsMatchAllKeywords == false);
            }
        }

        [TestMethod()]
        public void MatchTest_Cache_MultiKeywords()
        {
            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";
                var cache = matcher.CreateStringCache(str);

                Assert.IsTrue(matcher.Match(cache, new[] { "llow", "NH" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "NH" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "nh", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
            }


            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinInitialsMatchProvider.GetName() }, true);
                string str = "HelloWorld 你好世界！";
                var cache = matcher.CreateStringCache(str);

                Assert.IsTrue(matcher.Match(cache, new[] { "llow", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "世界" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "nh", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
            }

            {
                var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, false);
                string str = "HelloWorld 你好世界！";
                var cache = matcher.CreateStringCache(str);

                Assert.IsTrue(matcher.Match(cache, new[] { "llow", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "NH" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "llOW", "世界" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "SHIJIE", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "NI", "hao", "！" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "NI", "hao", "w" }).IsMatchAllKeywords == true);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "世界", "!" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "X" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "lloW", "  " }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "a", " " }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { "a" }).IsMatchAllKeywords == false);
                Assert.IsTrue(matcher.Match(cache, new[] { " " }).IsMatchAllKeywords == true);
            }
        }


        [TestMethod()]
        public void MatchsTest()
        {
            List<MatchCache> caches = new List<MatchCache>();
            caches.Add(new MatchCache("HelloWorld 你好世界！"));
            caches.Add(new MatchCache("NH 1234"));
            caches.Add(new MatchCache("测试测试测试测试"));
            caches.Add(new MatchCache("zxvasrw 123123 fdgadg 123 hell"));
            caches.Add(new MatchCache("aaabbb123123 大顶堆"));
            caches.Add(new MatchCache("Florida attorney general, against criticism, seeks to keep abortion rights amendment off 2024 ballot \r\n"));
            caches.Add(new MatchCache("In a reversal, Schumer says he will move forward to confirm hundreds military promotions previously held up by Tuberville"));

            var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, false);
            matcher.Matchs(caches, new string[] { "hell", "123" });

            {
                DateTime start = DateTime.Now;
                var r = matcher.Matchs(caches, new string[] { "hell", "123" });
                Assert.IsTrue(r.IsMatchAllKeywords);
                Assert.IsTrue(r.HitFlags[0][0] == true
                              && r.HitFlags[0][1] == true
                              && r.HitFlags[0][2] == true
                              && r.HitFlags[0][3] == true
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
                DateTime end = DateTime.Now;
                Console.WriteLine($"MatchsTest: {end - start}");
            }

            {
                DateTime start = DateTime.Now;
                var r = matcher.Matchs(caches, new string[] { "hell", "123" }, 2);
                Assert.IsTrue(r.IsMatchAllKeywords);
                Assert.IsTrue(r.HitFlags[0][0] == true
                              && r.HitFlags[0][1] == true
                              && r.HitFlags[0][2] == true
                              && r.HitFlags[0][3] == true
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
                DateTime end = DateTime.Now;
                Console.WriteLine($"MatchsTest tasks: {end - start}");
            }

            {
                DateTime start = DateTime.Now;
                var r = matcher.Matchs(caches, new string[] { "hell", "123", "xx1X" });
                Assert.IsTrue(r.IsMatchAllKeywords == false);
                Assert.IsTrue(r.HitFlags[0][0] == true
                              && r.HitFlags[0][1] == true
                              && r.HitFlags[0][2] == true
                              && r.HitFlags[0][3] == true
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
                DateTime end = DateTime.Now;
                Console.WriteLine($"MatchsTest: {end - start}");
            }

            {
                DateTime start = DateTime.Now;
                var r = matcher.Matchs(caches, new string[] { "hell", "123", "xx1X" }, 2);
                Assert.IsTrue(r.IsMatchAllKeywords == false);
                Assert.IsTrue(r.HitFlags[0][0] == true
                              && r.HitFlags[0][1] == true
                              && r.HitFlags[0][2] == true
                              && r.HitFlags[0][3] == true
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
                DateTime end = DateTime.Now;
                Console.WriteLine($"MatchsTest tasks: {end - start}");
            }
        }


        [TestMethod()]
        public void MatchsTestCase()
        {
            List<MatchCache> caches = new List<MatchCache>();
            caches.Add(new MatchCache("HelloWorld 你好世界！"));
            caches.Add(new MatchCache("NH 1234"));

            var matcher = Builder.Build(new[] { DirectMatchProvider.GetName(), ChineseZhCnPinYinMatchProvider.GetName() }, true);

            {
                var r = matcher.Matchs(caches, new string[] { "Hell", "123" });
                Assert.IsTrue(r.IsMatchAllKeywords);
                Assert.IsTrue(r.HitFlags[0][0] == true
                              && r.HitFlags[0][1] == true
                              && r.HitFlags[0][2] == true
                              && r.HitFlags[0][3] == true
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
            }

            {
                var r = matcher.Matchs(caches, new string[] { "hell", "123"});
                Assert.IsTrue(r.IsMatchAllKeywords == false);
                Assert.IsTrue(r.HitFlags[0][0] == false
                              && r.HitFlags[0][1] == false
                              && r.HitFlags[0][2] == false
                              && r.HitFlags[0][3] == false
                              && r.HitFlags[0][4] == false
                              && r.HitFlags[0][5] == false
                              && r.HitFlags[1][0] == false
                              && r.HitFlags[1][1] == false
                              && r.HitFlags[1][2] == false
                              && r.HitFlags[1][3] == true
                              && r.HitFlags[1][4] == true
                              && r.HitFlags[1][5] == true
                              && r.HitFlags[1][6] == false
                );
            }
        }
    }
}