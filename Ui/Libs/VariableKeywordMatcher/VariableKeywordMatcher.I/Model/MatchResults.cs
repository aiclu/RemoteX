using System;
using System.Collections.Generic;
using System.Linq;

namespace VariableKeywordMatcher.Model
{
    /// <summary>
    /// result of the match
    /// </summary>
    public class MatchResults
    {
        /// <summary>
        /// to show the match result
        /// </summary>
        /// <param name="originalStrings"></param>
        /// <param name="keywords"></param>
        /// <param name="isCaseSensitive"></param>
        /// <param name="matchResults">MatchResult Of Every Original String</param>
        public MatchResults(IEnumerable<string> originalStrings, IEnumerable<string> keywords, bool isCaseSensitive, List<MatchResult> matchResults)
        {
            OriginalString = new List<string>(originalStrings);
            Keywords = new List<string>(keywords);
            IsCaseSensitive = isCaseSensitive;
            MatchResultOfEveryString = matchResults;
            HitFlags = matchResults.Select(x => x.HitFlags).ToList();
            KeywordsMatchedFlags = MergeFlags(matchResults.Select(x => x.KeywordsMatchedFlags).ToList()); ;
        }

        /// <summary>
        /// original string of this match
        /// </summary>
        public List<string> OriginalString { get; protected set; }

        /// <summary>
        /// keywords of this match
        /// </summary>
        public List<string> Keywords { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsCaseSensitive { get; protected set; }

        /// <summary>
        /// identify which keyword are matched
        /// </summary>
        public List<bool> KeywordsMatchedFlags { get; protected set; }

        /// <summary>
        /// return true if all keywords are found in the original string match cache.
        /// </summary>
        public bool IsMatchAllKeywords => KeywordsMatchedFlags?.All(x => x == true) == true && KeywordsMatchedFlags.Count > 0;

        /// <summary>
        /// identify which keyword are matched
        /// </summary>
        public List<MatchResult> MatchResultOfEveryString { get; protected set; }


        /// <summary>
        /// identify a place is matched or not. e.g. find "aa" on "aabaa" will return {true, true, false, true, true}
        /// </summary>
        public List<List<bool>> HitFlags { get; protected set; }


        private static List<bool> MergeFlags(List<List<bool>> flagss)
        {
            var mergedFlags = new List<bool>();
            for (var i = 0; i < flagss.First().Count; i++)
                mergedFlags.Add(false);

            foreach (var flags in flagss)
            {
                for (int j = 0; j < flags.Count; j++)
                    mergedFlags[j] |= flags[j];
            }
            return mergedFlags;
        }
    }
}