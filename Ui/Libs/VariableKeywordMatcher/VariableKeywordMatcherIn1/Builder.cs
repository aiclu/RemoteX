using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;
using VariableKeywordMatcher.Provider.DiscreteMatch;
using VariableKeywordMatcher.Provider.InitialsMatch;

namespace VariableKeywordMatcherIn1
{
    /// <summary>
    /// Matcher builder
    /// </summary>
    public static class Builder
    {
        private static readonly Dictionary<string, MatchProviderBase> AvailableProviders = new Dictionary<string, MatchProviderBase>();

        private static MatchProviderBase CreateProviderInstance(Type providerType, bool isCaseSensitive)
        {
            return (MatchProviderBase)Activator.CreateInstance(providerType, new object[] { isCaseSensitive });
        }

        private static void LoadProviders()
        {
            if (AvailableProviders.ContainsKey(DirectMatchProvider.GetName()) == false)
                AvailableProviders.Add(DirectMatchProvider.GetName(), new DirectMatchProvider(false));
            if (AvailableProviders.ContainsKey(InitialsMatchProvider.GetName()) == false)
                AvailableProviders.Add(InitialsMatchProvider.GetName(), new InitialsMatchProvider(false));
            if (AvailableProviders.ContainsKey(DiscreteMatchProvider.GetName()) == false)
                AvailableProviders.Add(DiscreteMatchProvider.GetName(), new DiscreteMatchProvider(false));
            if (AvailableProviders.ContainsKey(ChineseZhCnPinYinInitialsMatchProvider.GetName()) == false)
                AvailableProviders.Add(ChineseZhCnPinYinInitialsMatchProvider.GetName(), new ChineseZhCnPinYinInitialsMatchProvider(false));
            if (AvailableProviders.ContainsKey(ChineseZhCnPinYinMatchProvider.GetName()) == false)
                AvailableProviders.Add(ChineseZhCnPinYinMatchProvider.GetName(), new ChineseZhCnPinYinMatchProvider(false));

            try
            {
                var files = Directory.GetFiles(Directory.GetParent(typeof(Builder).Assembly.Location).FullName, "VariableKeywordMatcher.Provider.*.dll");
                foreach (var dllPath in files)
                {
                    var assembly = Assembly.LoadFile(dllPath);
                    var types = assembly.GetTypes();
                    var providers = types.Where(item =>
                            item.IsSubclassOf(typeof(MatchProviderBase)) && !item.IsAbstract)
                        .Select(type => CreateProviderInstance(type, false));

                    foreach (var provider in providers.OrderBy(x => x.GetProviderDescription()))
                    {
                        var t = provider.GetProviderName();
                        if (AvailableProviders.ContainsKey(t) == false)
                            AvailableProviders.Add(t, provider);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Return a list of available match providers
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetAvailableProviderNames()
        {
            LoadProviders();
            return AvailableProviders.Select(x => x.Key).ToList();
        }

        /// <summary>
        /// Get the description of match provider in its native language
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetProviderDescription(string name)
        {
            if (AvailableProviders.ContainsKey(name) == false)
                LoadProviders();
            if (AvailableProviders.ContainsKey(name))
                return AvailableProviders[name].GetProviderDescription();
            return $"provider: `{name}` not found";
        }

        /// <summary>
        /// Get the description of match provider in English
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetProviderDescriptionEn(string name)
        {
            if (AvailableProviders.ContainsKey(name) == false)
                LoadProviders();
            if (AvailableProviders.ContainsKey(name))
                return AvailableProviders[name].GetProviderDescriptionEn();
            return $"{name} provider not found";
        }

        /// <summary>
        /// Create a matcher including input match providers, CaseSensitive is false by default
        /// </summary>
        /// <param name="enabledProviderNames"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Matcher Build(IEnumerable<string> enabledProviderNames, bool isCaseSensitive = false)
        {
            LoadProviders();

            var providers = new List<MatchProviderBase>();
            foreach (var providerType in enabledProviderNames)
            {
                if (AvailableProviders.ContainsKey(providerType) == false)
                    throw new Exception($"{providerType} provider not found");
                providers.Add(CreateProviderInstance(AvailableProviders[providerType].GetType(), isCaseSensitive));
            }

            var matcher = new Matcher(providers, isCaseSensitive);
            return matcher;
        }
    }
}
