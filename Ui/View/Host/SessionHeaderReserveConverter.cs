using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace _1RM.View.Host;

// Local template sizing only. A star column can report a clipped DesiredSize while tabs
// overflow; reserve the existing fixed chrome slots before measuring the scrollable tabs.
public sealed class SessionHeaderReserveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 5 || values[0] is not true) return 0.0;
        var launcher = values[2] is true ? 24.0 + 8.0 : 0.0;
        if (values[1] is true) return launcher; // Window buttons are already reserved by the prefix.
        // Match the existing 185 / 45 / 45 DIP placeholder slots; do not resize window buttons.
        var fullScreen = values[3] is true ? 45.0 : 0.0;
        var closeAll = values[4] is Visibility visibility && visibility != Visibility.Collapsed ? 45.0 : 0.0;
        return 185.0 + fullScreen + closeAll + launcher;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
