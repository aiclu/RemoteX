using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Demo.Annotations;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;

namespace Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region properties
        private ObservableCollection<VmDisplayItem> _displayItems = new ObservableCollection<VmDisplayItem>();
        public ObservableCollection<VmDisplayItem> DisplayItems
        {
            get => _displayItems;
            set
            {
                _displayItems = value;
                OnPropertyChanged(nameof(DisplayItems));
            }
        }

        private ObservableCollection<MatchProviderInfo> _availableMatcherProviders = new ObservableCollection<MatchProviderInfo>();
        public ObservableCollection<MatchProviderInfo> AvailableMatcherProviders
        {
            get => _availableMatcherProviders;
            set
            {
                _availableMatcherProviders = value;
                OnPropertyChanged(nameof(AvailableMatcherProviders));
            }
        }


        private string _keywords;
        public string Keywords
        {
            get => _keywords;
            set
            {
                _keywords = value;
                OnPropertyChanged(nameof(Keywords));
            }
        }
        #endregion



        void MatchTest(Matcher matcher, string keyword, string word)
        {
            Console.Write($"\r\nThe `{keyword}` on `{word}`: ");
            // 4. Build cache for original strings
            var matchCache = matcher.CreateStringCache(word);
            // 5. Match with the keywords
            var result = matcher.Match(matchCache, keyword);
            // 6. print result
            if (result.IsMatchAllKeywords == true)
            {
                // print where should be high-light
                for (int i = 0; i < result.HitFlags.Count; i++)
                {
                    if (result.HitFlags[i] == true)
                    {
                        // high light
                        Console.Write($"[{result.OriginalString[i]}]");
                    }
                    else
                    {
                        // normal
                        Console.Write($"{result.OriginalString[i]}");
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Not matched");
                for (int i = 0; i < result.KeywordsMatchedFlags.Count; i++)
                {
                    if (result.KeywordsMatchedFlags[i] == false)
                    {
                        Console.WriteLine($"{result.Keywords[i]} was not matched");
                    }
                }
            }
        }


        private void Test()
        {
            // 1. Get the names of available matchers
            var availableProviderNames = VariableKeywordMatcherIn1.Builder.GetAvailableProviderNames().ToList();
            // 2. Make a list of which providers you want to use.
            var enabledProviderNames = new List<string>();
            enabledProviderNames.Add(availableProviderNames[0]);
            enabledProviderNames.Add(availableProviderNames[1]);
            // 3. Create the matcher(case sensitive = false)
            var matcher = VariableKeywordMatcherIn1.Builder.Build(enabledProviderNames, false);
            // 4. Build cache for original strings
            var matchCache = matcher.CreateStringCache("Hello World");
            // 5. Match with the keywords
            var result = matcher.Match(matchCache, new List<string>() { "he", "wo" });
            // 6. print result
            if (result.IsMatchAllKeywords == true)
            {
                // print where should be high-light
                for (int i = 0; i < result.HitFlags.Count; i++)
                {
                    if (result.HitFlags[i] == true)
                    {
                        // high light
                        Console.Write($"[{result.OriginalString[i]}]");
                    }
                    else
                    {
                        // normal
                        Console.Write($"{result.OriginalString[i]}");
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Not matched");
                for (int i = 0; i < result.KeywordsMatchedFlags.Count; i++)
                {
                    if (result.KeywordsMatchedFlags[i] == false)
                    {
                        Console.WriteLine($"{result.Keywords[i]} was not matched");
                    }
                }
            }
            MatchTest(matcher, "he wo", "Hello World");
            MatchTest(matcher, "nihao", "你好世界");
            MatchTest(matcher, "sj", "你好世界");
        }

        private VariableKeywordMatcher.Matcher _matcher;
        private readonly List<VariableKeywordMatcher.Model.MatchCache> _matchCaches = new List<VariableKeywordMatcher.Model.MatchCache>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            Test();

            // get all available match providers, and show them on list view by binding AvailableMatcherProviders
            var availableProviderNames = VariableKeywordMatcherIn1.Builder.GetAvailableProviderNames();
            foreach (var enumProviderType in availableProviderNames)
            {
                AvailableMatcherProviders.Add(new MatchProviderInfo()
                {
                    Name = enumProviderType,
                    Title1 = VariableKeywordMatcherIn1.Builder.GetProviderDescription(enumProviderType),
                    Title2 = VariableKeywordMatcherIn1.Builder.GetProviderDescriptionEn(enumProviderType),
                    Enabled = true,
                });
            }
            _matcher = VariableKeywordMatcherIn1.Builder.Build(availableProviderNames);


            // init all original strings
            var allStrings = new List<string>()
            {
                "Hello World",
                "你好哇，世界",
                "こんにちは, 世界",
                "大夫 = doctor",
                "士大夫 = literati",
                "华为云edge",
            };

            // init match cache for original strings
            foreach (var str in allStrings)
            {
                var matchCache = _matcher.CreateStringCache(str);
                _matchCaches.Add(matchCache);
            }

            // strings shown on list view
            DisplayItems = new ObservableCollection<VmDisplayItem>(allStrings.Select(x => new VmDisplayItem(x)));
        }

        private void TextBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            // if keyword is null, show all strings.
            if (string.IsNullOrWhiteSpace(Keywords))
            {
                foreach (var item in DisplayItems)
                {
                    item.ObjectVisibility = Visibility.Visible;
                    item.DispNameControl = item.OrgDispNameControl;
                }
                return;
            }
            else
            {
                // check enabled match providers, if enabled providers changed then rebuild the matcher.
                var enabledMatcher = AvailableMatcherProviders.Where(x => x.Enabled);
                if (_matcher.ProviderTypes.Count != enabledMatcher.Count()
                    || enabledMatcher.Any(x => _matcher.ProviderTypes.Contains(x.Name) == false))
                {
                    // rebuild
                    _matcher = VariableKeywordMatcherIn1.Builder.Build(enabledMatcher.Select(x => x.Name));
                }

                // do the match
                for (var i = 0; i < _matchCaches.Count; i++)
                {
                    var cache = _matchCaches[i];
                    var item = DisplayItems[i];
                    var result = _matcher.Match(cache, Keywords.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries));
                    if (result.IsMatchAllKeywords)
                    {
                        item.ObjectVisibility = Visibility.Visible;

                        // if all keywords are matched, highline the matched chars on OriginalString
                        var sp = new StackPanel()
                        { Orientation = System.Windows.Controls.Orientation.Horizontal };
                        for (int j = 0; j < result.HitFlags.Count; j++)
                        {
                            if (result.HitFlags[j])
                                sp.Children.Add(new TextBlock()
                                {
                                    Text = result.OriginalString[j].ToString(),
                                    Background = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132)),
                                });
                            else
                                sp.Children.Add(new TextBlock()
                                {
                                    Text = result.OriginalString[j].ToString(),
                                });
                        }

                        item.DispNameControl = sp;
                    }
                    else
                    {
                        item.ObjectVisibility = Visibility.Collapsed;
                    }
                }
            }
        }
    }
}
