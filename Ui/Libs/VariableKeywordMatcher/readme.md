![](./doc/icons/icon-64.png)

# [Variable-Keyword-Matcher](https://github.com/VShawn/VariableKeywordMatcher)

VariableKeywordMatcher is a C# lib for:
- English initials matching
- English discrete matching
- Chinese PinYin matching
- Japanese Romajin matching
- highlight multiple keywords in search
- pluggable for more spell matching for future. (e.g. [Tongyong Pinyin](https://en.wikipedia.org/wiki/Tongyong_Pinyin), [Korean Hangul](https://en.wikipedia.org/wiki/Hangul) ... )

![](./doc/GifDemo/demo.gif)


## Usage

### Install

Install from nuget:

[VariableKeywordMatcher](https://www.nuget.org/packages/VariableKeywordMatcher/)

[(optional)MatcherProvider: ChineseZhCnPinYin](https://www.nuget.org/packages/VariableKeywordMatcher.Provider.ChineseZhCnPinYin/)

[(optional)MatcherProvider: ChineseZhCnPinYinInitials](https://www.nuget.org/packages/VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials/)

[(optional)MatcherProvider: JapaneseRomaji](https://www.nuget.org/packages/VariableKeywordMatcher.Provider.JapaneseRomaji/)


### QuickStart

1. Get the names of available matchers

```C#
var availableProviderNames = VariableKeywordMatcher.Builder.GetAvailableProviderNames().ToList();
```

2. Make a list of which providers you want to use.

```C#
var enabledProviderNames = new ist<string>();
enabledProviderNames.AddavailableProviderNames[0]);
enabledProviderNames.AddavailableProviderNames[1]);
```

3. Create the matcher(case sensitive = false)

```C#
var matcher = VariableKeywordMatcher.Builder.Build(enabledProviderNames);
```

4. Build cache for original strings

```C#
var matchCache = matcher.CreateStringCache("Hello World");
```

5. Match with the keywords

```C#
var result = matcher.Match(matchCache, new List<string>() { "he", "wo" });
```

6. print result

```C#
if (result.IsMatchAllKeywords == true)
{
    // print where should be high-light
    for (int i = 0; i < result.HitFlags.Count; i++)
    {
        if (result.HitFlags[i] == true)
        {
            // highlight
            Console.Write($"[{result.OriginalString[i]}]");
        }
        else
        {
            // normal
            Console.Write($"{result.OriginalString[i]}");
        }
        Console.WriteLine();
    }
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
```

### Demo project [PRemoteM](https://github.com/VShawn/PRemoteM)
[![](https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/quickstart.gif)](https://github.com/VShawn/PRemoteM)


## Included Components

- https://github.com/rvhuang/kmp-algorithm (MIT)
- https://github.com/toolgood/ToolGood.Words.Pinyin (MIT)
- https://github.com/Cutano/Kawazu (MIT)
- https://github.com/komutan/NMeCab (GPL 2.0 / LGPL 2.0)