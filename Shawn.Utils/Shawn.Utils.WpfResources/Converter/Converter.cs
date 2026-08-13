using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Shawn.Utils.Wpf;

namespace Shawn.Utils.WpfResources.Converter
{
    public class ConverterIsGreaterThan : IValueConverter
    {
        public int CompareValue { get; set; } = 0;
        // Converter={StaticResource ConverterIsGreaterThan},ConverterParameter=50}

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is short s)
                return s > CompareValue;
            if (value is int i)
                return i > CompareValue;
            if (value is long l)
                return l > CompareValue;
            if (value is float f)
                return f > CompareValue;
            if (value is double d)
                return d > CompareValue;
            try
            {
                var v = ((int?)value) ?? CompareValue;
                return v > CompareValue;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterIsLowerThan : IValueConverter
    {
        public int CompareValue { get; set; } = 0;

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is short s)
                return s < CompareValue;
            if (value is int i)
                return i < CompareValue;
            if (value is long l)
                return l < CompareValue;
            if (value is float f)
                return f < CompareValue;
            if (value is double d)
                return d < CompareValue;
            try
            {
                var v = ((int?)value) ?? CompareValue + 1;
                return v < CompareValue;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class DoubleAdd : IValueConverter
    {
        public double AddValue { get; set; } = 1;
        // Converter={StaticResource ConverterIsGreaterThan},ConverterParameter=50}

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double v = (double)value;
            return v + AddValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class IntAdd : IValueConverter
    {
        public double AddValue { get; set; } = 1;

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int v = (int)value;
            return v + AddValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterBool2Visible : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return b ? "Visible" : "Collapsed";
            return "Collapsed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterBool2VisibleInv : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return !b ? "Visible" : "Collapsed";
            return "Visible";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterBoolInverse : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            else if (value is null)
                return true;
            throw new InvalidOperationException("The target must be a boolean");
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            else if (value is null)
                return true;
            return false;
        }

        #endregion
    }

    //public class ConverterDouble2Negate : IValueConverter
    //{
    //    #region IValueConverter 成员
    //    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        double ss = (double)value;
    //        return ss * -1;
    //    }
    //    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        throw new NotSupportedException();
    //    }
    //    #endregion
    //}

    public class ConverterDouble2Half : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double ss = (double)value;
            return ss * 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterTextWidthAndContent2PresentationSize : IMultiValueConverter
    {
        private static Size MeasureText(TextBlock tb, int fontSize)
        {
#pragma warning disable CS0618 // 类型或成员已过时
            var formattedText = new FormattedText(tb.Text, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                fontSize, Brushes.Black); // always uses MaxFontSize for desiredSize
#pragma warning restore CS0618 // 类型或成员已过时
            return new Size(formattedText.Width, formattedText.Height);
        }

        #region IValueConverter 成员

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                var tb = new TextBlock
                {
                    Text = values[0].ToString(),
                    Width = int.Parse(values[1]?.ToString() ?? "0"),
                    FontFamily = (FontFamily)values[2],
                    FontStyle = (FontStyle)values[3],
                    FontWeight = (FontWeight)values[4],
                    FontStretch = (FontStretch)values[5]
                };
                var size = MeasureText(tb, 20);
                double k = 1.0 * tb.Width / size.Width;
                double fs = (int)(20 * k);
                if (fs < 4)
                    fs = 4;
                return fs;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return 12;
            }
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterColorHexString2Brush : IValueConverter
    {
        #region IValueConverter

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value is string hex)
                {
                    var brush = ColorAndBrushHelper.ColorToMediaBrush(hex);
                    return brush;
                }
            }
            catch
            {
                // ignored
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter
    }

    public class ConverterColorHexString2Color : IValueConverter
    {
        #region IValueConverter

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value is string hex)
                {
                    var brush = ColorAndBrushHelper.HexColorToMediaColor(hex);
                    return brush;
                }
            }
            catch
            {
                // ignored
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Color c)
            {
                return c.ColorToHexColor(true);
            }
            return "#00000000";
        }

        #endregion IValueConverter
    }

    public class ConverterColorIsTransparent : IValueConverter
    {
        #region IValueConverter

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ColorAndBrushHelper.ColorIsTransparent(value?.ToString() ?? "#00000000");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter
    }


    public class ConverterIsTheSame : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var _1st = values.FirstOrDefault();
            return values.All(x => x == _1st);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /* USAGE: Visibility="{Binding AudioRedirectionMode, Converter={StaticResource ConverterEqual2Visible}, ConverterParameter={x:Null}}" */
    public class ConverterEqual2Visible : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value == null && parameter == null)
                {
                    return Visibility.Visible;
                }
                else if (value == null || parameter == null)
                {
                    return Visibility.Collapsed;
                }
                if (value is int i1 && int.TryParse(parameter.ToString(), out var i2))
                {
                    return (i1 != i2) ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is double d1 && double.TryParse(parameter.ToString(), out var d2))
                {
                    return Math.Abs(d1 - d2) > 0.0000001 ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is byte b1 && byte.TryParse(parameter.ToString(), out var b2))
                {
                    return (b1 != b2) ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is string s1)
                {
                    return (s1 != parameter.ToString()) ? Visibility.Collapsed : Visibility.Visible;
                }
                return (value == parameter || object.Equals(value, parameter)) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception)
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return parameter;
        }
    }

    /* USAGE: Visibility="{Binding AudioRedirectionMode, Converter={StaticResource ConverterEqual2Collapsed}, ConverterParameter={x:Null}}" */
    public class ConverterEqual2Collapsed : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value == null && parameter == null)
                {
                    return Visibility.Collapsed;
                }
                else if (value == null || parameter == null)
                {
                    return Visibility.Visible;
                }
                if (value is int i1 && int.TryParse(parameter.ToString(), out var i2))
                {
                    return (i1 == i2) ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is double d1 && double.TryParse(parameter.ToString(), out var d2))
                {
                    return Math.Abs(d1 - d2) < 0.0000001 ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is byte b1 && byte.TryParse(parameter.ToString(), out var b2))
                {
                    return (b1 == b2) ? Visibility.Collapsed : Visibility.Visible;
                }
                if (value is string s1)
                {
                    return (s1 == parameter.ToString()) ? Visibility.Collapsed : Visibility.Visible;
                }
                return (value == parameter || object.Equals(value, parameter)) ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception)
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return parameter;
        }
    }

    /* USAGE: Visibility="{Binding AudioRedirectionMode, Converter={StaticResource ConverterEqual2Collapsed}, ConverterParameter={x:Null}}" */
    public class ConverterIsEqual2Static : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value == parameter || object.Equals(value, parameter))
                    return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return parameter;
        }
    }


    public class ConverterIsEqual : IMultiValueConverter
    {
        /*****
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource ConverterIsEqual}" >
                    <Binding RelativeSource="{RelativeSource FindAncestor, AncestorType=view:ServerListPageView}" Path="DataContext.SelectedTabName" Mode="OneWay"></Binding>
                    <Binding Path="Name" Mode="OneWay"></Binding>
                </MultiBinding>
            </DataTrigger.Binding>
         */
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.Length == 2)
            {
                if (value[0] is string s1
                    && value[1] is string s2)
                {
                    return s1 == s2;
                }
                else if (value[0] is int i1
                         && value[1] is int i2)
                {
                    return i1 == i2;
                }
                else if (value[0] is bool b1
                         && value[1] is bool b2)
                {
                    return b1 == b2;
                }
                else if (value[0] is byte byte1
                         && value[1] is int byte2)
                {
                    return byte1 == byte2;
                }
                else if (value[0] is short short1
                         && value[1] is short short2)
                {
                    return short1 == short2;
                }
                else if (value[0] is double d1
                         && value[1] is double d2)
                {
                    return Math.Abs(d1 - d2) < 1e-10;
                }
                else if (value[0] is float f1
                         && value[1] is double f2)
                {
                    return Math.Abs(f1 - f2) < 1e-10;
                }
                else
                {
                    return object.Equals(value[0], value[1]);
                }
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }


    public class ConverterIsNotEqual : IMultiValueConverter
    {
        /*****
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource ConverterIsEqual}" >
                    <Binding RelativeSource="{RelativeSource FindAncestor, AncestorType=view:ServerListPageView}" Path="DataContext.SelectedTabName" Mode="OneWay"></Binding>
                    <Binding Path="Name" Mode="OneWay"></Binding>
                </MultiBinding>
            </DataTrigger.Binding>
         */
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.Length == 2)
            {
                if (value[0] is string s1
                    && value[1] is string s2)
                {
                    return s1 != s2;
                }
                else if (value[0] is int i1
                         && value[1] is int i2)
                {
                    return i1 != i2;
                }
                else if (value[0] is bool b1
                         && value[1] is bool b2)
                {
                    return b1 != b2;
                }
                else if (value[0] is byte byte1
                         && value[1] is int byte2)
                {
                    return byte1 != byte2;
                }
                else if (value[0] is short short1
                         && value[1] is short short2)
                {
                    return short1 != short2;
                }
                else if (value[0] is double d1
                         && value[1] is double d2)
                {
                    return Math.Abs(d1 - d2) > 1e-10;
                }
                else if (value[0] is float f1
                         && value[1] is double f2)
                {
                    return Math.Abs(f1 - f2) > 1e-10;
                }
                else
                {
                    return !object.Equals(value[0], value[1]);
                }
            }
            return true;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}