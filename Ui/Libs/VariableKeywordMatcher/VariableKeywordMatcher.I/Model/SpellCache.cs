using System.Collections.Generic;
using System.Diagnostics;
using VariableKeywordMatcher.Interface;

namespace VariableKeywordMatcher.Model
{
    /// <summary>
    /// Cache for match, storage the spell of original string.
    /// </summary>
    public class SpellCache : ISpellCache
    {
        private readonly string _providerName;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="providerName">cache target provider name</param>
        /// <param name="stringLength">original string length</param>
        public SpellCache(string providerName, int stringLength)
        {
            _providerName = providerName;
            Debug.Assert(stringLength > 0);
            Units = new List<List<string>>(stringLength);
            SkipFlag = new List<bool>(stringLength);
            ConvertFlag = new List<bool>(stringLength);
            for (int i = 0; i < stringLength; i++)
            {
                Units.Add(new List<string>());
                SkipFlag.Add(false);
                ConvertFlag.Add(false);
            }
        }

        /// <summary>
        /// alphabetical of original string.
        /// Unit[i] include all its pronunciations(if existed), else it include originalString[i] only
        /// </summary>
        public List<List<string>> Units { get; private set; }

        /// <summary>
        /// if SkipFlag[i] is true, then Unit[i] should not join keyword compare.
        /// </summary>
        public List<bool> SkipFlag { get; private set; }

        /// <summary>
        /// if ConvertFlag[i] is false, then Unit[i] is original char without any PinYin convert.
        /// </summary>
        public List<bool> ConvertFlag { get; private set; }

        /// <summary>
        /// return the target provider name of this spell cache
        /// </summary>
        /// <returns></returns>
        public string GetProviderName()
        {
            return _providerName;
        }
    }
}