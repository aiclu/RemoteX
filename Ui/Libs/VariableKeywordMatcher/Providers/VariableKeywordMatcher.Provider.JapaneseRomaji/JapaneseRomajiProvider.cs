using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kawazu;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.JapaneseRomaji
{
    public class JapaneseRomajiProvider : MatchProviderBase
    {
        private static readonly KawazuConverter Converter = new KawazuConverter();
        public JapaneseRomajiProvider(bool isCaseSensitive = false) : base(isCaseSensitive)
        {
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(JapaneseRomajiProvider);
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
        public override string GetProviderDescriptionEn()
        {
            return "Japanese romaji(Hepburn) matcher; e.g. keyword \"konichi\" will matched the string \"こんにちは\"";
        }

        /// <summary>
        /// Get match provider description
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescription()
        {
            return "日本のローマピンインマッチャー";
        }

        /// <summary>
        /// append cache for this match provider to MatchCache
        /// </summary>
        /// <returns></returns>
        private List<Division> GetRomaji(string str)
        {
            var t = Task.Factory.StartNew(() =>
            {
                var ret = Converter.GetDivisions(str, To.Romaji);
                if (ret.Wait(1000))
                    return ret.Result;
                return new List<Division>();
            });

            t.Wait();

            return t.Result.ToList();
        }

        public override void AppendDescriptions(ref MatchCache matchCache)
        {
            var ps = GetRomaji(matchCache.OriginalString);
            if (ps?.Count == 0)
            {
                matchCache.SpellCaches.AddOrUpdate(nameof(JapaneseRomajiProvider), new SpellCache4JapaneseRomaji(GetName(), "", new List<List<int>>(), new List<bool>() { false }, new List<bool>() { false }), (key, value) => value);
                return;
            }

            int i = 0;
            var indexes = new List<List<int>>();
            var convertFlag = new List<bool>();
            var beginFlag = new List<bool>();
            var sb = new StringBuilder();
            foreach (var division in ps)
            {
                var orgElement = "";
                foreach (var d in division)
                {
                    orgElement += d.Element;
                }
                i = matchCache.OriginalString.IndexOf(orgElement, i, StringComparison.Ordinal);
                Debug.Assert(i >= 0);


                if (string.IsNullOrEmpty(division.RomaReading) || division.RomaReading == orgElement)
                {
                    for (var j = 0; j < orgElement.Length; j++)
                    {
                        var c = orgElement[j];
                        sb.Append(base.IsCaseSensitive ? c.ToString() : c.ToString().ToLower());
                        convertFlag.Add(false);
                        beginFlag.Add(true);
                        indexes.Add(new List<int>() { i + j });
                        Debug.Assert(sb.Length == indexes.Count);
                        Debug.Assert(sb.Length == convertFlag.Count);
                    }
                }
                else
                {
                    var ls = new List<int>();
                    for (var j = 0; j < orgElement.Length; j++)
                        ls.Add(i + j);

                    foreach (var c in division.RomaReading)
                    {
                        sb.Append(c.ToString().ToLower());
                        beginFlag.Add(false);
                        convertFlag.Add(true);
                        indexes.Add(ls);
                        Debug.Assert(sb.Length == indexes.Count);
                        Debug.Assert(sb.Length == convertFlag.Count);
                    }
                    beginFlag[beginFlag.Count - division.RomaReading.Length] = true;
                }

                i += orgElement.Length;
                Debug.Assert(i <= matchCache.StringLength);
            }

            Debug.Assert(i == matchCache.StringLength);
            Debug.Assert(sb.Length == indexes.Count);
            Debug.Assert(sb.Length == convertFlag.Count);
            Debug.Assert(sb.Length == beginFlag.Count);
            var sp = new SpellCache4JapaneseRomaji(GetName(), sb.ToString(), indexes, convertFlag, beginFlag);
            matchCache.SpellCaches.AddOrUpdate(nameof(JapaneseRomajiProvider), sp, (key, value) => value);
        }

        protected override MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase)
        {
            if (!matchCache.SpellCaches.ContainsKey(nameof(JapaneseRomajiProvider)))
                AppendDescriptions(ref matchCache);

            Debug.Assert(!string.IsNullOrEmpty(matchCache.OriginalString));

            if (matchCache.SpellCaches[nameof(JapaneseRomajiProvider)] is SpellCache4JapaneseRomaji spell)
            {
                if (string.IsNullOrEmpty(spell.SpellString) || spell.ConvertFlag.All(x => x == false))
                    return MatchResult.CreateUnmatchedResult(matchCache.OriginalString, keywords, IsCaseSensitive);

                var keywordList = keywordsInTrueCase?.ToList();
                Debug.Assert(keywordList?.Any() == true);

                var orgStringInTrueCase = spell.SpellString;
                var spellHitFlags = CreateBoolFlags(orgStringInTrueCase.Length);
                var keywordsMatchedFlags = CreateBoolFlags(keywordList.Count());

                for (var i = 0; i < keywordList.Count; i++)
                {
                    var keyword = keywordList[i];
                    keywordsMatchedFlags[i] = DoMatch(orgStringInTrueCase, spell.BeginFlags, keyword, ref spellHitFlags);
                }

                // gent hit flag
                var hitFlags = CreateBoolFlags(matchCache.StringLength);
                for (int i = 0; i < spellHitFlags.Count; i++)
                {
                    if (spellHitFlags[i])
                    {
                        for (int j = 0; j < spell.SpellCharToOrgIndexes[i].Count; j++)
                        {
                            hitFlags[spell.SpellCharToOrgIndexes[i][j]] = true;
                        }
                    }
                }

                var ret = new MatchResult(matchCache.OriginalString, keywords, base.IsCaseSensitive, keywordsMatchedFlags, hitFlags);
                return ret;
            }
            else
            {
                throw new Exception(
                    $"{nameof(ISpellCache)} Type error, should be {nameof(SpellCache4JapaneseRomaji)}, but now is {matchCache.SpellCaches[nameof(JapaneseRomajiProvider)].GetType().Name}");
            }
        }

        private bool DoMatch(string orgStringInTrueCase, List<bool> beginFlags, string keywordInTrueCase, ref List<bool> hitFlags)
        {
            bool isKeywordMatched = false;
            for (int i = 0; i <= orgStringInTrueCase.Length - keywordInTrueCase.Length; i++)
            {
                if (beginFlags[i] == false
                    || orgStringInTrueCase[i] != keywordInTrueCase[0])
                    continue;

                var tmpHitFlags = CreateBoolFlags(hitFlags.Count);

                int j = 0;
                int k = i;
                bool lastBeginFlag = true;
                for (; j < keywordInTrueCase.Length; j++)
                {
                    while (k < orgStringInTrueCase.Length)
                    {
                        if ((lastBeginFlag || beginFlags[k] == true)
                            && orgStringInTrueCase[k] == keywordInTrueCase[j])
                        {
                            if (beginFlags[k] == true)
                                lastBeginFlag = true;
                            tmpHitFlags[k] = true;
                            ++k;
                            break;
                        }
                        else
                        {
                            if (beginFlags[k] == true)
                                lastBeginFlag = false;
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
