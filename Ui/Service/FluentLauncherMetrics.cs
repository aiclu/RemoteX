using System;

namespace _1RM.Service
{
    internal sealed class FluentLauncherMetrics
    {
        public double Width { get; }
        public double SearchHeight { get; }
        public double RowHeight { get; }
        public double ActionHeight { get; }
        public double MaxHeight { get; }
        public FluentLauncherMetrics(bool fluent, double fontSize, double workWidth, double workHeight)
        {
            var font = double.IsNaN(fontSize) || double.IsInfinity(fontSize) ? 12 : Math.Max(12, fontSize);
            Width = fluent ? Math.Min(400, Math.Max(1, workWidth - 40)) : 400;
            SearchHeight = fluent ? Math.Max(46, font * 1.6 + 20) : 46;
            RowHeight = fluent ? Math.Max(40, font * 2.4 + 10) : 40;
            ActionHeight = fluent ? Math.Max(34, font * 1.6 + 12) : 34;
            MaxHeight = fluent ? Math.Max(1, workHeight - 100) : double.PositiveInfinity;
        }
        public double HeightFor(int count, bool actions = false) =>
            Math.Min(MaxHeight, SearchHeight + (actions ? ActionHeight : RowHeight) * Math.Min(8, Math.Max(0, count)));
    }
}
