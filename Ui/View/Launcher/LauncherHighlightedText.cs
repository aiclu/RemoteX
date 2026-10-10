using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace _1RM.View.Launcher;

// Launcher-only text view: one trimming boundary, and no view objects shared by recycled rows.
public sealed class LauncherHighlightedText : TextBlock
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title),
        typeof(ServerTitleViewModel), typeof(LauncherHighlightedText), new PropertyMetadata(null, Changed));
    public ServerTitleViewModel? Title
    {
        get => (ServerTitleViewModel?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    private ServerTitleViewModel? _subscribed;
    public LauncherHighlightedText()
    {
        Loaded += (_, _) => { Subscribe(); Render(); };
        Unloaded += (_, _) => Unsubscribe();
        TextTrimming = TextTrimming.CharacterEllipsis;
        TextWrapping = TextWrapping.NoWrap;
    }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var text = (LauncherHighlightedText)target;
        text.Unsubscribe();
        if (text.IsLoaded) text.Subscribe();
        text.Render();
    }
    private void Subscribe()
    {
        if (_subscribed == Title) return;
        Unsubscribe(); _subscribed = Title;
        if (_subscribed != null) PropertyChangedEventManager.AddHandler(_subscribed, TitleChanged, nameof(ServerTitleViewModel.HighlightFlags));
    }
    private void Unsubscribe()
    {
        if (_subscribed != null) PropertyChangedEventManager.RemoveHandler(_subscribed, TitleChanged, nameof(ServerTitleViewModel.HighlightFlags));
        _subscribed = null;
    }
    private void TitleChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private void Render()
    {
        Inlines.Clear();
        var title = Title;
        if (title == null) return;
        var flags = title.HighlightFlags;
        if (flags.Count != title.Text.Length) { Inlines.Add(new Run(title.Text)); return; }
        for (var start = 0; start < flags.Count;)
        {
            var end = start + 1;
            while (end < flags.Count && flags[start] == flags[end]) end++;
            var run = new Run(title.Text.Substring(start, end - start));
            if (flags[start]) { run.FontWeight = FontWeights.Bold; run.TextDecorations = System.Windows.TextDecorations.Underline; }
            Inlines.Add(run); start = end;
        }
    }
}
