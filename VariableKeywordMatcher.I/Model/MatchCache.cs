using System.Collections.Concurrent;
using System.Collections.Generic;
using VariableKeywordMatcher.Interface;

namespace VariableKeywordMatcher.Model
{
    /// <summary>
    /// Cache of an original string
    /// </summary>
    public class MatchCache
    {
        /// <summary>
        /// </summary>
        /// <param name="originalString"></param>
        public MatchCache(string originalString)
        {
            OriginalString = originalString;
        }

        /// <summary>
        /// original string
        /// </summary>
        public string OriginalString { get; }

        /// <summary>
        /// length of original string
        /// </summary>
        public int StringLength => OriginalString?.Length ?? 0;

        /// <summary>
        /// caches for matching of original string
        /// </summary>
        public ConcurrentDictionary<string, ISpellCache> SpellCaches { get; set; } = new ConcurrentDictionary<string, ISpellCache>();
    }
}
