using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.InitialsMatch
{
    /// <summary>
    /// Initials matcher for string, e.g. keyword \"HW\" will matched the string \"Hello World\"
    /// </summary>
    public class InitialsMatchProvider : MatchProviderBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isCaseSensitive"></param>
        public InitialsMatchProvider(bool isCaseSensitive) : base(isCaseSensitive)
        {
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(InitialsMatchProvider);
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
            return "Initials matcher for string";
        }


        /// <summary>
        /// Get provider description in English
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescriptionEn()
        {
            return "e.g. keyword \"HW\" will matched the string \"Hello World\"";
        }

        /// <summary>
        /// append cache for this match provider to MatchCache
        /// </summary>
        /// <returns></returns>
        /// <param name="matchCache"></param>
        public override void AppendDescriptions(ref MatchCache matchCache)
        {
            if (matchCache.SpellCaches.ContainsKey(nameof(InitialsMatchProvider)))
                matchCache.SpellCaches.TryRemove(nameof(InitialsMatchProvider), out _);

            var sp = new SpellCache(GetProviderName(), matchCache.StringLength);

            // set PinYin chart cache
            for (var i = 0; i < matchCache.OriginalString.Length; i++)
            {
                var c = matchCache.OriginalString[i];
                if (i == 0 && c != ' '
                    || i > 0 && matchCache.OriginalString[i - 1] == ' ' && c != ' ')
                {
                    sp.Units[i].Add(base.IsCaseSensitive ? c.ToString() : c.ToString().ToLower());
                }
                else
                {
                    sp.SkipFlag[i] = true;
                }

            }
            matchCache.SpellCaches.AddOrUpdate(nameof(InitialsMatchProvider), sp, (key, value) => value);
        }

        protected override MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase)
        {
            if (!matchCache.SpellCaches.ContainsKey(nameof(InitialsMatchProvider)))
                AppendDescriptions(ref matchCache);

            if (matchCache.SpellCaches[nameof(InitialsMatchProvider)] is SpellCache spell)
            {
                if (spell.Units.Count == 0 || spell.SkipFlag.All(x => x))
                    return MatchResult.CreateUnmatchedResult(matchCache.OriginalString, keywords, IsCaseSensitive);

                Debug.Assert(!string.IsNullOrEmpty(matchCache.OriginalString));
                var keywordList = keywordsInTrueCase?.ToList();
                Debug.Assert(keywordList?.Any() == true);

                var orgStringInTrueCase = ConvertStringToTrueCase(matchCache.OriginalString, base.IsCaseSensitive);
                var hitFlags = CreateBoolFlags(matchCache.StringLength);
                var keywordsMatchedFlags = CreateBoolFlags(keywordList.Count());

                for (var i = 0; i < keywordList.Count; i++)
                {
                    var keyword = keywordList[i];
                    keywordsMatchedFlags[i] = DoMatch(orgStringInTrueCase, spell, keyword, ref hitFlags);
                }

                var ret = new MatchResult(matchCache.OriginalString, keywords, base.IsCaseSensitive,
                    keywordsMatchedFlags, hitFlags);
                return ret;
            }
            else
            {
                throw new Exception(
                    $"{typeof(ISpellCache).Name} Type error, should be {typeof(SpellCache).Name}, but now is {matchCache.SpellCaches[nameof(InitialsMatchProvider)].GetType().Name}");
            }
        }

        private bool DoMatch(string orgStringInTrueCase, SpellCache spellCache, string keywordInTrueCase, ref List<bool> hitFlags)
        {
            bool isKeywordMatched = false;
            for (int i = 0; i <= orgStringInTrueCase.Length - keywordInTrueCase.Length; i++)
            {
                if (spellCache.SkipFlag[i])
                    continue;

                var tmpHitFlags = CreateBoolFlags(hitFlags.Count);
                bool flag = true;

                int j = 0;
                int k = i;
                for (; j < keywordInTrueCase.Length; j++)
                {
                    if (k >= orgStringInTrueCase.Length || j >= keywordInTrueCase.Length)
                    {
                        break;
                    }

                    if (orgStringInTrueCase[k] == keywordInTrueCase[j])
                    {
                        tmpHitFlags[k] = true;
                        ++k;
                        while (k < orgStringInTrueCase.Length && spellCache.SkipFlag[k])
                        {
                            ++k;
                        }
                        continue;
                    }
                    else
                    {
                        flag = false;
                        break;
                    }
                }

                if (flag && tmpHitFlags.Count(x => x) == keywordInTrueCase.Length)
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