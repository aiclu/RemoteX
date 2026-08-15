using System;
using System.Collections.Generic;
using System.Linq;
using VariableKeywordMatcher;

namespace NugetTest
{

    class Program
    {
        static void MatchTest(Matcher matcher, string keyword, string word)
        {
            Console.Write($"\r\nThe `{keyword}` on `{word}`: ");
            // 4. Build cache for original strings
            var matchCache = matcher.CreateStringCache(word);
            // 5. Match with the keywords
            var result = matcher.Match(matchCache, keyword);
            // 6. print result
            if (result.IsMatchAllKeywords == true)
            {
                // print where should be high-light
                for (int i = 0; i < result.HitFlags.Count; i++)
                {
                    if (result.HitFlags[i] == true)
                    {
                        // high light
                        Console.Write($"[{result.OriginalString[i]}]");
                    }
                    else
                    {
                        // normal
                        Console.Write($"{result.OriginalString[i]}");
                    }
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Not matched");
                for (int i = 0; i < result.KeywordsMatchedFlags.Count; i++)
                {
                    if (result.KeywordsMatchedFlags[i] == false)
                    {
                        Console.WriteLine($"{result.Keywords[i]} was not matched");
                    }
                }
            }
        }


        static void Main(string[] args)
        {
            // 1. Get the names of available matchers
            var availableProviderNames = VariableKeywordMatcherIn1.Builder.GetAvailableProviderNames().ToList();

            string msg = "We offering:\r\n";
            foreach (var name in availableProviderNames)
            {
                msg += name + "\r\n";
            }

            Console.Write(msg);

            // 2. Make a list of which providers you want to use.
            var enabledProviderNames = new List<string>(availableProviderNames);

            // 3. Create the matcher(case sensitive = false)
            var matcher = VariableKeywordMatcherIn1.Builder.Build(enabledProviderNames, false);

            MatchTest(matcher, "he wo", "Hello World");
            MatchTest(matcher, "nihao", "你好世界");
            MatchTest(matcher, "sj", "你好世界");
        }
    }

}