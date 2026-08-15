using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AlgorithmForce.Searching;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.DirectMatch
{
    /// <summary>
    /// Default keyword matcher for original string, e.g. keyword \"Hello Wor\" will matched the string \"Hello World\"
    /// </summary>
    public class DirectMatchProvider : MatchProviderBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isCaseSensitive"></param>
        public DirectMatchProvider(bool isCaseSensitive) : base(isCaseSensitive)
        {
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(DirectMatchProvider);
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
        /// Get match provider description
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescription()
        {
            return "Default keyword matcher for original string";
        }

        /// <summary>
        /// Get provider description in English
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescriptionEn()
        {
            return "e.g. keyword \"Hello Wor\" will matched the string \"Hello World\"";
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

        private bool DoMatch(string orgStringInTrueCase, string keywordInTrueCase, ref List<bool> hitFlags)
        {
            var indexes = orgStringInTrueCase.IndexesOf(keywordInTrueCase.ToCharArray());
            if (!indexes.Any()) return false;
            foreach (var i in indexes)
            {
                MarkMatched(ref hitFlags, i, keywordInTrueCase.Length);
            }
            return true;
        }

        private void MarkMatched(ref List<bool> hitFlags, int startIndex, int length)
        {
            for (int i = 0; i < length; i++)
            {
                hitFlags[i + startIndex] = true;
            }
        }
    }
}