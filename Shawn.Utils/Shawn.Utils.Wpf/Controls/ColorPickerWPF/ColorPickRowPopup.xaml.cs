using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ColorPickerWPF.Code;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Brushes = System.Drawing.Brushes;
using Color = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for ColorPickRowPopup.xaml
    /// </summary>
    public partial class ColorPickRowPopup : UserControl
    {
        #region Color
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color?), typeof(ColorPickRowPopup),
                    new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorPropertyChanged));

        private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is Color color)
            {
                var c = (Color)color;
                ((ColorPickRowPopup)d).HexColor = ColorAndBrushHelper.ArgbToHexColor(c.A, c.R, c.G, c.B);
            }
        }

        public Color? Color
        {
            get => (Color?)GetValue(ColorProperty);
            set
            {
                if (value == Color)
                    return;
                if (value == null)
                {
                    SetValue(ColorProperty, null);
                    SetValue(HexColorProperty, null);
                    ColorDisplayGrid.Background = ColorPickerControl4Popup.ChessboardBrush(2);
                }
                else if(value is { } color)
                {
                    var hexColor = color.ToHexString();
                    if (color != Color)
                        SetValue(ColorProperty, color);
                    if (HexColor != hexColor)
                        SetValue(HexColorProperty, hexColor);
                    if (color.ColorIsTransparent())
                    {
                        ColorDisplayGrid.Background = ColorPickerControl4Popup.ChessboardBrush(4);
                    }
                    else
                    {
                        ColorDisplayGrid.Background = new SolidColorBrush(color);
                    }
                    ColorPicker.SetColor(color);
                }
            }
        }
        #endregion


        #region HexColor
        public static readonly DependencyProperty HexColorProperty = DependencyProperty.Register("HexColor", typeof(string), typeof(ColorPickRowPopup),
                new FrameworkPropertyMetadata("#FFFFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexColorPropertyChanged));
        private static void OnHexColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (string)e.NewValue;
            try
            {
                var c = ColorAndBrushHelper.HexColorToMediaColor(value);
                ((ColorPickRowPopup)d).Color = c;
                if (ColorAndBrushHelper.ColorIsTransparent(value))
                {
                    ((ColorPickRowPopup)d).ColorDisplayGrid.Background = ColorPickerControl4Popup.ChessboardBrush(4);
                }
                else
                {
                    ((ColorPickRowPopup)d).ColorDisplayGrid.Background = new SolidColorBrush(c);
                }
            }
            catch
            {
                ((ColorPickRowPopup)d).Color = null;
                ((ColorPickRowPopup)d).ColorDisplayGrid.Background = ColorPickerControl4Popup.ChessboardBrush(2);
            }
        }
        public string HexColor
        {
            get => (string)GetValue(HexColorProperty);
            set
            {
                if (value == HexColor)
                    return;
                try
                {
                    var c = ColorAndBrushHelper.HexColorToMediaColor(value);
                    var hexColor = value;
                    if (HexColor != hexColor)
                        SetValue(HexColorProperty, hexColor);
                    if (c != Color)
                        SetValue(ColorProperty, c);
                    if (ColorAndBrushHelper.ColorIsTransparent(c))
                    {
                        ColorPickerControl4Popup.ChessboardBrush(4);
                    }
                    else
                    {
                        ColorDisplayGrid.Background = new SolidColorBrush(c);
                    }
                    ColorPicker.SetColor(c);
                }
                catch (Exception e)
                {
                    ColorDisplayGrid.Background = ColorPickerControl4Popup.ChessboardBrush(2);
                    SetValue(ColorProperty, null);
                    SetValue(HexColorProperty, null);
                    SimpleLogHelper.Error(e);
                }
            }
        }
        #endregion


        #region HexVisible
        public static readonly DependencyProperty HexVisibleProperty = DependencyProperty.Register("HexVisible", typeof(Visibility), typeof(ColorPickRowPopup), new PropertyMetadata(System.Windows.Visibility.Visible, null));
        public Visibility HexVisible
        {
            get => (Visibility)GetValue(HexVisibleProperty);
            set => SetValue(HexVisibleProperty, value);
        }
        #endregion


        public ColorPickRowPopup()
        {
            InitializeComponent();
            ColorPicker.OnPickColor += color => { HexColor = color.ToHexString(); };
        }

        private void PickColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopupPicker.Focus();
            PopupPicker.IsOpen = true;
        }
    }
}
