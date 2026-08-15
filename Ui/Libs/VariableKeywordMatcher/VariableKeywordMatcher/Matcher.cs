using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VariableKeywordMatcher.Interface;
using VariableKeywordMatcher.Model;

namespace VariableKeywordMatcher
{
    /// <summary>
    /// 
    /// </summary>
    public class Matcher
    {
        private readonly List<MatchProviderBase> _providers;

        /// <summary>
        /// 
        /// </summary>
        public readonly bool IsCaseSensitive;

        /// <summary>
        /// name of available providers
        /// </summary>
        public List<string> ProviderTypes => _providers?.Select(x => x.GetProviderName()).ToList();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="providers"></param>
        /// <param name="isCaseSensitive"></param>
        public Matcher(IEnumerable<MatchProviderBase> providers, bool isCaseSensitive)
        {
            IsCaseSensitive = isCaseSensitive;
            _providers = new List<MatchProviderBase>(providers);
        }

        /// <summary>
        /// create a matching spell cache for the string
        /// </summary>
        /// <returns></returns>
        public MatchCache CreateStringCache(string originalString)
        {
            CheckProviders();
            var sd = new MatchCache(originalString);
            foreach (var provider in _providers)
            {
                provider.AppendDescriptions(ref sd);
            }
            return sd;
        }

        /// <summary>
        /// using keywords to match the cache of the original string
        /// </summary>
        /// <param name="matchCache">cache of the original string</param>
        /// <param name="keywords">list of keywords</param>
        /// <param name="taskCount">number of tasks to run in parallel, 1 by default</param>
        /// <returns></returns>
        public MatchResult Match(MatchCache matchCache, IEnumerable<string> keywords, int taskCount = 1)
        {
            CheckProviders();
            var kws = keywords as string[] ?? keywords.ToArray();
            var result = MatchResult.CreateUnmatchedResult(matchCache.OriginalString, kws, IsCaseSensitive);

            if (taskCount > 1 && _providers.Count > 1)
            {
                var results = new ConcurrentQueue<MatchResult>();
                var providers = new ConcurrentQueue<MatchProviderBase>(_providers);
                var tasks = new List<System.Threading.Tasks.Task>();
                for (int i = 0; i < taskCount && i < _providers.Count; i++)
                {
                    tasks.Add(System.Threading.Tasks.Task.Run(() =>
                    {
                        while (providers.TryDequeue(out var provider))
                        {
                            var r = provider.DoMatches(matchCache, kws);
                            results.Enqueue(r);
                        }
                    }));
                }
                // wait for all tasks to finish
                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                foreach (var r in results)
                {
                    result.Merge(r);
                }
            }
            else
            {
                foreach (var r in _providers.Select(provider => provider.DoMatches(matchCache, keywords)))
                {
                    result.Merge(r);
                }
            }
            return result;
        }

        /// <summary>
        /// using keywords to match the cache of the original string
        /// </summary>
        /// <param name="matchCache">cache of the original string</param>
        /// <param name="keyword">single keyword</param>
        /// <param name="taskCount">number of tasks to run in parallel, 1 by default</param>
        /// <returns></returns>
        public MatchResult Match(MatchCache matchCache, string keyword, int taskCount = 1)
        {
            return Match(matchCache, new[] { keyword }, taskCount);
        }

        /// <summary>
        /// using keywords to match the string
        /// </summary>
        /// <param name="originalString">original string</param>
        /// <param name="keywords">list of keywords</param>
        /// <param name="taskCount">number of tasks to run in parallel, 1 by default</param>
        /// <returns></returns>
        public MatchResult Match(string originalString, IEnumerable<string> keywords, int taskCount = 1)
        {
            var mc = CreateStringCache(originalString);
            return Match(mc, keywords, taskCount);
        }

        /// <summary>
        /// using keyword to match the string
        /// </summary>
        /// <param name="originalString">original string</param>
        /// <param name="keyword"></param>
        /// <param name="taskCount">number of tasks to run in parallel, 1 by default</param>
        /// <returns></returns>
        public MatchResult Match(string originalString, string keyword, int taskCount = 1)
        {
            var mc = CreateStringCache(originalString);
            return Match(mc, new[] { keyword }, taskCount);
        }

        /// <summary>
        /// using keyword to match the string
        /// </summary>
        /// <param name="matchCaches">caches of the original strings</param>
        /// <param name="keywords"></param>
        /// <param name="taskCount">number of tasks to run in parallel, 1 by default</param>
        /// <returns></returns>
        public MatchResults Matchs(List<MatchCache> matchCaches, IEnumerable<string> keywords, int taskCount = 1)
        {
            var mrs = new List<MatchResult>(matchCaches.Count);
            var kws = keywords as string[] ?? keywords.ToArray();
            if (taskCount == 1)
            {
                foreach (var matchCache in matchCaches)
                {
                    var mr = Match(matchCache, kws, taskCount);
                    mrs.Add(mr);
                }
            }
            else
            {
                var results = new ConcurrentDictionary<int, MatchResult>(); // index, result to keep the order
                var caches = new ConcurrentQueue<MatchCache>(matchCaches);
                var tasks = new List<System.Threading.Tasks.Task>();
                for (int i = 0; i < taskCount && i < matchCaches.Count; i++)
                {
                    tasks.Add(System.Threading.Tasks.Task.Run(() =>
                    {
                        while (caches.TryDequeue(out var cache))
                        {
                            var r = Match(cache, kws, 2);
                            results.TryAdd(matchCaches.IndexOf(cache), r);
                        }
                    }));
                }
                // wait for all tasks to finish
                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                // get the results in order
                foreach (var r in results.OrderBy(x => x.Key))
                {
                    mrs.Add(r.Value);
                }
            }
            return new MatchResults(matchCaches.Select(x => x.OriginalString).ToList(), kws, this.IsCaseSensitive, mrs);
        }

        private void CheckProviders()
        {
            if (!(_providers?.Count > 0))
            {
                throw new Exception("Keyword match provider is not been initialed! Please create the Shooter from the ShootingString.Builder");
            }
        }
    }
}
