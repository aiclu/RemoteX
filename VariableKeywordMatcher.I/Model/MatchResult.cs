using System;
using System.Collections.Generic;
using System.Linq;

namespace VariableKeywordMatcher.Model
{
    /// <summary>
    /// result of the match
    /// </summary>
    public class MatchResult
    {
        /// <summary>
        /// to show the match result
        /// </summary>
        /// <param name="originalString"></param>
        /// <param name="keywords"></param>
        /// <param name="isCaseSensitive"></param>
        /// <param name="keywordsMatchedFlags"></param>
        /// <param name="hitFlags"></param>
        public MatchResult(string originalString, IEnumerable<string> keywords, bool isCaseSensitive, List<bool> keywordsMatchedFlags, List<bool> hitFlags)
        {
            OriginalString = originalString;
            Keywords = new List<string>(keywords);
            IsCaseSensitive = isCaseSensitive;
            HitFlags = hitFlags;
            KeywordsMatchedFlags = keywordsMatchedFlags;
        }

        /// <summary>
        /// original string of this match
        /// </summary>
        public string OriginalString { get; protected set; }

        /// <summary>
        /// keywords of this match
        /// </summary>
        public List<string> Keywords { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsCaseSensitive { get; protected set; }

        /// <summary>
        /// return true if all keywords are found in the original string match cache.
        /// </summary>
        public bool IsMatchAllKeywords => KeywordsMatchedFlags?.All(x => x == true) == true && KeywordsMatchedFlags.Count > 0;

        /// <summary>
        /// identify which keyword are matched
        /// </summary>
        public List<bool> KeywordsMatchedFlags { get; protected set; }


        /// <summary>
        /// identify a place is matched or not. e.g. find "aa" on "aabaa" will return {true, true, false, true, true}
        /// </summary>
        public List<bool> HitFlags { get; protected set; }



        /// <summary>
        /// create a unmatched result
        /// </summary>
        /// <returns></returns>
        public static MatchResult CreateUnmatchedResult(string originalString, IEnumerable<string> keywords, bool isCaseSensitive)
        {
            var keywordMatchedFlags = new List<bool>(keywords.Count());
            for (int i = 0; i < keywords.Count(); i++)
            {
                keywordMatchedFlags.Add(false);
            }
            var hitFlags = new List<bool>(originalString.Length);
            for (int i = 0; i < originalString.Length; i++)
            {
                hitFlags.Add(false);
            }
            return new MatchResult(originalString, keywords, isCaseSensitive, keywordMatchedFlags, hitFlags);
        }



        /// <summary>
        /// merge two results who having the same OriginalString
        /// </summary>
        /// <param name="o"></param>
        /// <exception cref="Exception"></exception>
        public void Merge(MatchResult o)
        {
            if (OriginalString != o.OriginalString) throw new Exception($"can't merge two results with different {nameof(OriginalString)}");

            if (Keywords.Count() == o.Keywords.Count()
                && IsCaseSensitive == o.IsCaseSensitive
                && CompareKeywords(Keywords, o.Keywords))
            {
                for (int i = 0; i < o.KeywordsMatchedFlags.Count; i++)
                {
                    KeywordsMatchedFlags[i] |= o.KeywordsMatchedFlags[i];
                }
            }
            else
            {
                // append keywords
                foreach (var keyword in o.Keywords)
                {
                    if (this.Keywords.Contains(keyword) == false)
                    {
                        this.Keywords.Add(keyword);
                        KeywordsMatchedFlags.Add(false);
                    }
                }

                // merge matched flags
                for (var i = 0; i < o.Keywords.Count; i++)
                {
                    var keyword = o.Keywords[i];
                    var j = this.Keywords.IndexOf(keyword);
                    this.KeywordsMatchedFlags[j] |= o.KeywordsMatchedFlags[i];
                }
            }

            for (int i = 0; i < o.HitFlags.Count; i++)
            {
                HitFlags[i] |= o.HitFlags[i];
            }
        }


        private bool CompareKeywords(IEnumerable<string> k1, IEnumerable<string> k2)
        {
            return k1.Count() == k2.Count() && k1.All(item => !k2.All(x => x != item));
        }
    }
}