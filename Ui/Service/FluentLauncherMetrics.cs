using System;

namespace _1RM.Service
{
    internal sealed class FluentLauncherMetrics
    {
        public double Width { get; }
        public double SearchHeight { get; }
        public double RowHeight { get; }
        public double ActionHeight { get; }
        public double FilterHeight { get; }
        public double ListPadding { get; }
        public double MaxHeight { get; }
        public FluentLauncherMetrics(bool fluent, double fontSize, double workWidth, double workHeight, double fontLineSpacing = 1.2)
        {
            var font = double.IsFinite(fontSize) ? Math.Clamp(fontSize, 10, 20) : 12;
            var lineSpacing = double.IsFinite(fontLineSpacing) ? Math.Max(1.2, fontLineSpacing) : 1.2;
            var line = Math.Ceiling(font * lineSpacing);
            var smallLine = Math.Ceiling(Math.Max(10, font - 2) * lineSpacing);
            Width = fluent ? Math.Min(400, Math.Max(1, workWidth - 40)) : 400;
            SearchHeight = fluent ? Math.Max(46, line + 24) : 46;
            RowHeight = fluent ? Math.Max(44, line + smallLine + 12) : 40;
            ActionHeight = fluent ? Math.Max(36, line + 16) : 34;
            FilterHeight = fluent ? smallLine + 16 : 0;
            ListPadding = fluent ? 8 : 0;
            MaxHeight = fluent ? Math.Max(1, workHeight - 100) : double.PositiveInfinity;
        }
        public double HeightFor(int count, bool actions = false, bool hasFilters = false) =>
            Math.Min(MaxHeight, SearchHeight + (hasFilters && !actions ? FilterHeight : 0)
                + (count > 0 ? ListPadding : 0) + (actions ? ActionHeight : RowHeight) * Math.Min(8, Math.Max(0, count)));
    }
}
