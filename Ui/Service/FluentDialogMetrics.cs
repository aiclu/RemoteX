using System;

namespace _1RM.Service
{
    internal sealed class FluentDialogMetrics
    {
        public double WidthLimit { get; }
        public double HeightLimit { get; }
        public double Width { get; }
        public double BodyMaxHeight => Math.Max(1, HeightLimit - 144);
        public double FieldMaxWidth => Math.Max(1, Width - 64);
        public FluentDialogMetrics(double preferredWidth, double workWidth, double workHeight)
        {
            WidthLimit = Math.Max(1, workWidth - 24);
            HeightLimit = Math.Max(1, workHeight - 24);
            Width = Math.Min(double.IsNaN(preferredWidth) ? 560 : Math.Max(1, preferredWidth), WidthLimit);
        }
    }
}
