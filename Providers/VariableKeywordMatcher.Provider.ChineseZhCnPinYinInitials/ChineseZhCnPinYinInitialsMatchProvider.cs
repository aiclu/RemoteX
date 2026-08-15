using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials
{
    /// <summary>
    /// IsCaseSensitive = false
    /// </summary>
    public class ChineseZhCnPinYinInitialsMatchProvider : MatchProviderBase
    {
        public ChineseZhCnPinYinInitialsMatchProvider(bool isCaseSensitive) : base(isCaseSensitive)
        {
        }


        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(ChineseZhCnPinYinInitialsMatchProvider);
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
            return "汉语(zh-cn)拼音首字母匹配器";
        }

        /// <summary>
        /// Get provider description in English
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescriptionEn()
        {
            return "Chinese(zh-cn) PinYin initials matcher; e.g. keyword \"NHSJ\" will matched the string \"你好世界\"";
        }

        /// <summary>
        /// append cache for this match provider to MatchCache
        /// </summary>
        /// <returns></returns>
        public override void AppendDescriptions(ref MatchCache matchCache)
        {
            var sp = new SpellCache(GetProviderName(), matchCache.StringLength);

            // set PinYin chart cache
            for (var i = 0; i < matchCache.OriginalString.Length; i++)
            {
                var tmp = ToolGood.Words.FirstPinyin.WordsHelper.GetAllFirstPinyin(matchCache.OriginalString[i]);
                if (tmp?.Any() == true)
                {
                    foreach (var t in tmp)
                    {
                        // PinYin ignore case
                        sp.Units[i].Add(t.ToLower());
                    }
                    sp.ConvertFlag[i] = true;
                }
                else
                {
                    sp.ConvertFlag[i] = false;
                }
            }

            // no Chinese character.
            if (sp.Units.All(x => x.Count == 0))
            {
                matchCache.SpellCaches.AddOrUpdate(nameof(ChineseZhCnPinYinInitialsMatchProvider), new SpellCache(GetProviderName(), matchCache.StringLength), (key, value) => value);
                return;
            }

            // set original char cache
            for (var i = 0; i < matchCache.OriginalString.Length; i++)
            {
                var c = matchCache.OriginalString[i];
                // PinYin ignore case
                sp.Units[i].Add(base.IsCaseSensitive ? c.ToString() : c.ToString().ToLower());
            }

            matchCache.SpellCaches.AddOrUpdate(nameof(ChineseZhCnPinYinInitialsMatchProvider), sp, (key, value) => value);
        }

        protected override MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase)
        {
            if (!matchCache.SpellCaches.ContainsKey(nameof(ChineseZhCnPinYinInitialsMatchProvider)))
                AppendDescriptions(ref matchCache);


            if (matchCache.SpellCaches[nameof(ChineseZhCnPinYinInitialsMatchProvider)] is SpellCache spell)
            {
                if (spell.Units.Count == 0 || spell.Units.Any(x => x.Count == 0))
                    return MatchResult.CreateUnmatchedResult(matchCache.OriginalString, keywords, IsCaseSensitive);

                Debug.Assert(!string.IsNullOrEmpty(matchCache.OriginalString));
                var keywordList = keywordsInTrueCase?.ToList();
                Debug.Assert(keywordList?.Any() == true);

                var hitFlags = CreateBoolFlags(matchCache.StringLength);
                var keywordsMatchedFlags = CreateBoolFlags(keywordList.Count());

                for (var i = 0; i < keywordList.Count; i++)
                {
                    var keyword = keywordList[i];
                    keywordsMatchedFlags[i] = DoMatch(spell, keyword, base.IsCaseSensitive, ref hitFlags);
                }

                var ret = new MatchResult(matchCache.OriginalString, keywords, base.IsCaseSensitive, keywordsMatchedFlags, hitFlags);
                return ret;
            }
            else
            {
                throw new Exception(
                    $"{typeof(ISpellCache).Name} Type error, should be {typeof(SpellCache).Name}, but now is {matchCache.SpellCaches[nameof(ChineseZhCnPinYinInitialsMatchProvider)].GetType().Name}");
            }
        }

        private bool DoMatch(SpellCache spellsCache, string keywordInTrueCase, bool isCaseSensitive, ref List<bool> hitFlags)
        {
            Debug.Assert(!string.IsNullOrEmpty(keywordInTrueCase));
            Debug.Assert(spellsCache.Units.Count > 0 && spellsCache.Units.All(x => x.Count > 0));

            bool isKeywordMatched = false;
            for (int i = 0; i <= spellsCache.Units.Count - keywordInTrueCase.Length; i++)
            {
                bool flag = true;
                // check any matched to keyword
                for (int j = 0; j < keywordInTrueCase.Length; j++)
                {
                    if (spellsCache.ConvertFlag[i + j] == true && spellsCache.Units[i + j].All(x => x != keywordInTrueCase[j].ToString().ToLower())
                    || spellsCache.ConvertFlag[i + j] == false && spellsCache.Units[i + j].All(x => x != keywordInTrueCase[j].ToString()))
                    {
                        // no match for this turn.
                        flag = false;
                        break;
                    }
                }

                if (flag)
                {
                    // matched then update hitFlags.
                    isKeywordMatched = true;
                    for (int j = 0; j < keywordInTrueCase.Length; j++)
                    {
                        hitFlags[i + j] = true;
                    }
                }
            }
            return isKeywordMatched;
        }
    }
}
