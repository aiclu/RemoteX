namespace VariableKeywordMatcher.Interface
{
    /// <summary>
    /// Cache for matcher
    /// </summary>
    public interface ISpellCache
    {
        /// <summary>
        /// return the target provider name of this spell cache
        /// </summary>
        /// <returns></returns>
        string GetProviderName();
    }
}