using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher.Provider.ChineseZhCnPinYin
{
    /// <summary>
    /// IsCaseSensitive = false
    /// </summary>
    public class ChineseZhCnPinYinMatchProvider : MatchProviderBase
    {
        public ChineseZhCnPinYinMatchProvider(bool isCaseSensitive) : base(isCaseSensitive)
        {
        }

        /// <summary>
        /// return match provider name
        /// </summary>
        /// <returns></returns>
        public static string GetName()
        {
            return nameof(ChineseZhCnPinYinMatchProvider);
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
            return "Chinese(zh-cn) PinYin matcher; e.g. keyword \"nihaoshij\" will matched the string \"你好世界\"";
        }

        /// <summary>
        /// Get match provider description
        /// </summary>
        /// <returns></returns>
        public override string GetProviderDescription()
        {
            return "汉语(zh-cn)拼音全拼匹配器";
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
                var tmp = ToolGood.Words.Pinyin.WordsHelper.GetAllPinyin(matchCache.OriginalString[i]);
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
                matchCache.SpellCaches.AddOrUpdate(nameof(ChineseZhCnPinYinMatchProvider), new SpellCache(GetProviderName(), matchCache.StringLength), (key, value) => value);
                return;
            }

            // set original char cache
            for (var i = 0; i < matchCache.OriginalString.Length; i++)
            {
                if (sp.Units[i].Count == 0)
                {
                    var c = matchCache.OriginalString[i];
                    // PinYin ignore case
                    sp.Units[i].Add(base.IsCaseSensitive ? c.ToString() : c.ToString().ToLower());
                }
            }

            matchCache.SpellCaches.AddOrUpdate(nameof(ChineseZhCnPinYinMatchProvider), sp, (key, value) => value);
        }

        protected override MatchResult DoFindMatches(MatchCache matchCache, IEnumerable<string> keywords, IEnumerable<string> keywordsInTrueCase)
        {
            if (!matchCache.SpellCaches.ContainsKey(nameof(ChineseZhCnPinYinMatchProvider)))
                AppendDescriptions(ref matchCache);

            if (matchCache.SpellCaches[nameof(ChineseZhCnPinYinMatchProvider)] is SpellCache spell)
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
                    $"{nameof(ISpellCache)} Type error, should be {nameof(SpellCache)}, but now is {matchCache.SpellCaches[nameof(ChineseZhCnPinYinMatchProvider)].GetType().Name}");
            }
        }

        private bool DoMatch(SpellCache spellsCache, string keywordInTrueCase, bool isCaseSensitive, ref List<bool> hitFlags)
        {
            Debug.Assert(!string.IsNullOrEmpty(keywordInTrueCase));
            Debug.Assert(spellsCache.Units.Count > 0 && spellsCache.Units.All(x => x.Count > 0));

            bool isKeywordMatched = false;
            for (int i = 0; i < spellsCache.Units.Count; i++)
            {
                var tmpHitFlags = CreateBoolFlags(hitFlags.Count);
                var flag = MatchSpellUnits(spellsCache, i, keywordInTrueCase, 0, tmpHitFlags);
                if (flag == true)
                {
                    // matched then update hitFlags.
                    isKeywordMatched = true;
                    for (int j = 0; j < hitFlags.Count; j++)
                    {
                        hitFlags[j] |= tmpHitFlags[j];
                    }
                }
            }

            return isKeywordMatched;
        }

        private bool MatchSpellUnits(SpellCache spellsCache, int index, string keyword, int keywordIndex, List<bool> hitFlags)
        {
            for (int i = 0; i < spellsCache.Units[index].Count; i++)
            {
                if (MatchOneSpell(spellsCache.Units[index][i], spellsCache.ConvertFlag[index], keyword, keywordIndex))
                {
                    if (keywordIndex + spellsCache.Units[index][i].Length >= keyword.Length)
                    {
                        hitFlags[index] = true;
                        return true;
                    }

                    if (index + 1 < spellsCache.Units.Count
                        && MatchSpellUnits(spellsCache, index + 1, keyword, keywordIndex + spellsCache.Units[index][i].Length, hitFlags))
                    {
                        hitFlags[index] = true;
                        return true;
                    }
                    else
                    {
                        hitFlags[index] = false;
                    }
                }
            }
            hitFlags[index] = false;
            return false;
        }


        private bool MatchOneSpell(string spell, bool convertFlag, string keyword, int keywordIndex)
        {
            if (keyword.Length - keywordIndex <= 0)
                return false;

            if (spell != keyword[keywordIndex].ToString())
            {
                if (convertFlag)
                {
                    // convertFlag == true, then spell is PinYin string.
                    for (int i = 0; i < Math.Min(spell.Length, keyword.Length - keywordIndex); i++)
                    {
                        if (!string.Equals(spell[i].ToString(), keyword[keywordIndex + i].ToString(), StringComparison.CurrentCultureIgnoreCase))
                            return false;
                    }
                }
                else
                {
                    // convertFlag == false, then spell is the original char of the string.
                    return false;
                }
            }

            return true;
        }
    }
}
