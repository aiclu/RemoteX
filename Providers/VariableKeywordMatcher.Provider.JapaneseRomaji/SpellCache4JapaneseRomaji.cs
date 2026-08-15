using System.Collections.Generic;
using VariableKeywordMatcher.Interface;

namespace VariableKeywordMatcher.Provider.JapaneseRomaji
{
    public class SpellCache4JapaneseRomaji : ISpellCache
    {
        private readonly string _providerName;

        public SpellCache4JapaneseRomaji(string providerName, string spellString, List<List<int>> spellCharToOrgIndexes, List<bool> convertFlag, List<bool> beginFlags)
        {
            _providerName = providerName;
            SpellString = spellString;
            SpellCharToOrgIndexes = spellCharToOrgIndexes;
            ConvertFlag = convertFlag;
            BeginFlags = beginFlags;
        }

        public string SpellString { get; private set; }
        /// <summary>
        /// Count == SpellString.Length
        /// </summary>
        public List<List<int>> SpellCharToOrgIndexes { get; private set; }
        /// <summary>
        /// Count == SpellString.Length, ConvertFlag[i] == false => SpellString[i] is original char
        /// </summary>
        public List<bool> ConvertFlag { get; private set; }
        /// <summary>
        /// Count == SpellString.Length, StartFlag[i] == true => i is the begin of the japanese word spell or original char
        /// </summary>
        public List<bool> BeginFlags { get; private set; }

        public string GetProviderName()
        {
            return _providerName;
        }
    }
}