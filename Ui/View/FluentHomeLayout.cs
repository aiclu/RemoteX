using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _1RM.View;

// Presentation-only layout: no view-model state, collection changes or LayoutUpdated subscriptions.
internal sealed class FluentHomeToolbarPanel : Panel
{
    private const double Gap = 8;
    private Rect[] _slots = Array.Empty<Rect>();
    internal bool IsSingleRow { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 5) return new Size();
        var width = double.IsInfinity(availableSize.Width) ? 800 : Math.Max(0, availableSize.Width);
        var sizes = new Size[5];
        for (var i = 1; i < 5; i++)
        {
            InternalChildren[i].Measure(new Size(width, double.PositiveInfinity));
            sizes[i] = InternalChildren[i].DesiredSize;
        }
        IsSingleRow = width >= 240 + sizes.Skip(1).Sum(s => s.Width) + Gap * 4;
        var addOnFirstRow = IsSingleRow || width >= sizes[4].Width + Gap + 96;
        var searchWidth = IsSingleRow ? width - sizes.Skip(1).Sum(s => s.Width) - Gap * 4
            : addOnFirstRow ? width - sizes[4].Width - Gap : width;
        InternalChildren[0].Measure(new Size(Math.Max(0, searchWidth), double.PositiveInfinity));
        sizes[0] = InternalChildren[0].DesiredSize;
        _slots = new Rect[5];
        var firstHeight = IsSingleRow ? sizes.Max(s => s.Height)
            : Math.Max(sizes[0].Height, addOnFirstRow ? sizes[4].Height : 0);
        _slots[0] = new Rect(0, 0, searchWidth, firstHeight);
        if (IsSingleRow)
        {
            var x = searchWidth + Gap;
            for (var i = 1; i < 5; i++)
            {
                _slots[i] = new Rect(x, 0, sizes[i].Width, firstHeight);
                x += sizes[i].Width + Gap;
                KeyboardNavigation.SetTabIndex(InternalChildren[i], i);
            }
            return new Size(width, firstHeight);
        }
        if (addOnFirstRow)
        {
            _slots[4] = new Rect(width - sizes[4].Width, 0, sizes[4].Width, firstHeight);
            KeyboardNavigation.SetTabIndex(InternalChildren[4], 1);
        }
        double left = 0, top = firstHeight + Gap, rowHeight = 0;
        for (var i = 1; i < (addOnFirstRow ? 4 : 5); i++)
        {
            if (left > 0 && left + sizes[i].Width > width)
            {
                left = 0; top += rowHeight + Gap; rowHeight = 0;
            }
            _slots[i] = new Rect(left, top, sizes[i].Width, sizes[i].Height);
            left += sizes[i].Width + Gap;
            rowHeight = Math.Max(rowHeight, sizes[i].Height);
            KeyboardNavigation.SetTabIndex(InternalChildren[i], addOnFirstRow ? i + 1 : i);
        }
        return new Size(width, top + rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Math.Min(_slots.Length, InternalChildren.Count); i++) InternalChildren[i].Arrange(_slots[i]);
        return finalSize;
    }
}

internal sealed class FluentHomeRowGrid : Grid
{
    internal const double LeadingWidth = 24, SelectionWidth = 28, IconWidth = 32, ProtocolWidth = 60, ActionWidth = 64;
    public static readonly DependencyProperty IsSourceHeaderProperty = DependencyProperty.Register(nameof(IsSourceHeader), typeof(bool),
        typeof(FluentHomeRowGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool IsSourceHeader { get => (bool)GetValue(IsSourceHeaderProperty); set => SetValue(IsSourceHeaderProperty, value); }
    private static readonly DependencyPropertyKey CompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Compact), typeof(bool),
        typeof(FluentHomeRowGrid), new PropertyMetadata(false));
    public static readonly DependencyProperty CompactProperty = CompactPropertyKey.DependencyProperty;
    public bool Compact => (bool)GetValue(CompactProperty);

    public FluentHomeRowGrid()
    {
        foreach (var width in new[] { LeadingWidth, SelectionWidth, IconWidth }) ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ProtocolWidth) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ActionWidth) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var compact = IsSourceHeader ? constraint.Width < 320 : constraint.Width < 420 || FluentWorkspace.GetIsCompact(this);
        if (Compact != compact) SetValue(CompactPropertyKey, compact);
        var actionWidth = ActionWidth;
        if (!IsSourceHeader && Children.Count > 0 && Children[Children.Count - 1] is Button button)
        {
            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // Leave one spacing unit for fractional-DPI text rounding before enabling ellipsis.
            actionWidth = Math.Max(ActionWidth, Math.Ceiling(button.DesiredSize.Width / 4) * 4 + 4);
            // A long localized action must not force a minimum-width window's grid past its card.
            // Keep a small name slot and let the local button template trim with a full tooltip.
            actionWidth = Math.Min(actionWidth, Math.Max(ActionWidth,
                constraint.Width - LeadingWidth - SelectionWidth - IconWidth - 24));
        }
        ColumnDefinitions[4].Width = new GridLength(compact ? 0 : ProtocolWidth);
        ColumnDefinitions[5].Width = new GridLength(IsSourceHeader && compact ? 0 : actionWidth);
        if (IsSourceHeader && Children.Count > 0 && Children[Children.Count - 1] is FrameworkElement actions)
        {
            SetRow(actions, compact ? 1 : 0);
            SetColumn(actions, compact ? 0 : 4);
            SetColumnSpan(actions, compact ? 6 : 2);
            actions.Margin = new Thickness(0, compact ? 4 : 0, 0, 0);
        }
        return base.MeasureOverride(constraint);
    }
}
