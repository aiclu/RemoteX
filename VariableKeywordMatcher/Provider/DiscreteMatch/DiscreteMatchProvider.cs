using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.DiscreteMatch
{
    /// <summary>
    /// Discrete matcher for string, e.g. keyword \"HLWD\" will matched the string \"Hello World\"
    /// </summary>
    public class DiscreteMatchProvider : MatchProviderBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isCaseSensitive"></param>
        public DiscreteMatchProvider(bool isCaseSensitive) : base(isCaseSensitive)
        {
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(DiscreteMatchProvider);
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public override string GetProviderName()
        {
            return GetName();
        }

        /// <summary>
        /// Get provider description in English
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescription()
        {
            return "Discrete matcher for string";
        }

        /// <summary>
        /// Get provider description in English
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescriptionEn()
        {
            return "e.g. keyword \"HLWD\" will matched the string \"Hello World\"";
        }

        /// <summary>
        /// append cache for this match provider to MatchCache
        /// </summary>
        /// <returns></returns>
        public override void AppendDescriptions(ref MatchCache matchCache)
        {
            // do nothing
        }

        protected override MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase)
        {
            Debug.Assert(!string.IsNullOrEmpty(matchCache.OriginalString));
            var keywordList = keywordsInTrueCase?.ToList();
            Debug.Assert(keywordList?.Any() == true);

            var orgStringInTrueCase = ConvertStringToTrueCase(matchCache.OriginalString, base.IsCaseSensitive);
            var hitFlags = CreateBoolFlags(matchCache.StringLength);
            var keywordsMatchedFlags = CreateBoolFlags(keywordList.Count());

            for (var i = 0; i < keywordList.Count; i++)
            {
                var keyword = keywordList[i];
                keywordsMatchedFlags[i] = DoMatch(orgStringInTrueCase, keyword, ref hitFlags);
            }

            var ret = new MatchResult(matchCache.OriginalString, keywords, base.IsCaseSensitive, keywordsMatchedFlags, hitFlags);
            return ret;
        }

        private static bool DoMatch(string orgStringInTrueCase, string keywordInTrueCase, ref List<bool> hitFlags)
        {
            bool isKeywordMatched = false;
            for (int i = 0; i <= orgStringInTrueCase.Length - keywordInTrueCase.Length; i++)
            {
                if(orgStringInTrueCase[i] != keywordInTrueCase[0])
                    continue;

                var tmpHitFlags = CreateBoolFlags(hitFlags.Count);

                int j = 0;
                int k = i;
                for (; j < keywordInTrueCase.Length; j++)
                {
                    while (k < orgStringInTrueCase.Length)
                    {
                        if (orgStringInTrueCase[k] == keywordInTrueCase[j])
                        {
                            tmpHitFlags[k] = true;
                            ++k;
                            break;
                        }
                        else
                        {
                            ++k;
                            continue;
                        }
                    }
                }

                if (tmpHitFlags.Count(x => x) == keywordInTrueCase.Length)
                {
                    // matched then update hitFlags.
                    isKeywordMatched = true;
                    for (int l = 0; l < hitFlags.Count; l++)
                    {
                        hitFlags[l] |= tmpHitFlags[l];
                    }
                }
            }
            return isKeywordMatched;
        }
    }
}