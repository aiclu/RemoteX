using System.Collections.Generic;
using System.Linq;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Interface
{
    /// <summary>
    /// base class of match provider
    /// </summary>
    public abstract class MatchProviderBase
    {
        /// <summary>
        /// Is case sensitive for this provider
        /// </summary>
        public bool IsCaseSensitive { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isCaseSensitive"></param>
        protected MatchProviderBase(bool isCaseSensitive)
        {
            IsCaseSensitive = isCaseSensitive;
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public abstract string GetProviderName();

        /// <summary>
        /// Get match provider description in English
        /// </summary>
        /// <returns></returns>
        public abstract string GetProviderDescriptionEn();

        /// <summary>
        /// Get match provider description
        /// </summary>
        /// <returns></returns>
        public abstract string GetProviderDescription();

        /// <summary>
        /// append cache for this match provider to MatchCache
        /// </summary>
        /// <returns></returns>
        public abstract void AppendDescriptions(ref MatchCache matchCache);


        /// <summary>
        /// using keywords to match the cache of the original sting
        /// </summary>
        /// <param name="matchCache">cache of the original sting</param>
        /// <param name="keywords">list of keywords</param>
        /// <returns></returns>
        public MatchResult DoMatches(MatchCache matchCache, IEnumerable<string> keywords)
        {
            if (string.IsNullOrEmpty(matchCache?.OriginalString))
                return MatchResult.CreateUnmatchedResult(matchCache.OriginalString, keywords, IsCaseSensitive);
            var keywordsInTrueCase = keywords.Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (keywordsInTrueCase?.Any() != true)
                return MatchResult.CreateUnmatchedResult(matchCache.OriginalString, keywords, IsCaseSensitive);

            if (IsCaseSensitive == false)
            {
                for (var i = 0; i < keywordsInTrueCase.Count; i++)
                {
                    keywordsInTrueCase[i] = keywordsInTrueCase[i].ToLower();
                }
            }
            return DoFindMatches(matchCache, keywords, keywordsInTrueCase);
        }

        /// <summary>
        /// for subclass
        /// </summary>
        /// <param name="matchCache"></param>
        /// <param name="keywords"></param>
        /// <param name="keywordsInTrueCase"></param>
        /// <returns></returns>
        protected abstract MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase);

        /// <summary>
        /// convert string to lowercase if isCaseSensitive == true
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        protected static string ConvertStringToTrueCase(string orgString, bool isCaseSensitive)
        {
            var orgStringInTrueCase = orgString;
            if (isCaseSensitive == false)
                orgStringInTrueCase = orgStringInTrueCase.ToLower();
            return orgStringInTrueCase;
        }

        /// <summary>
        /// create a bool list
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        protected static List<bool> CreateBoolFlags(int length)
        {
            var flags = new List<bool>(length);
            for (int i = 0; i < length; i++)
            {
                flags.Add(false);
            }
            return flags;
        }
    }
}