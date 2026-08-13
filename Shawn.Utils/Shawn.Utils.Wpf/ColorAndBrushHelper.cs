using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using Shawn.Utils.Wpf.Image;
using Point = System.Windows.Point;

namespace Shawn.Utils.Wpf
{
    public static class ColorAndBrushHelper
    {

        private static readonly object Obj = new object();
        private static readonly Dictionary<int, ImageBrush> ChessboardBrushes = new Dictionary<int, ImageBrush>();
        public static ImageBrush ChessboardBrush(int blockPixSize = 32)
        {
            lock (Obj)
            {
                if (ChessboardBrushes.ContainsKey(blockPixSize))
                    return ChessboardBrushes[blockPixSize];
                // 绘制透明背景
                var wpen = System.Drawing.Brushes.White;
                var gpen = System.Drawing.Brushes.LightGray;
                int span = blockPixSize;
                var bg = new System.Drawing.Bitmap(span * 2, span * 2);
                using (var g = System.Drawing.Graphics.FromImage(bg))
                {
                    g.FillRectangle(wpen, new System.Drawing.Rectangle(0, 0, bg.Width, bg.Height));
                    for (var v = 0; v < span * 2; v += span)
                    {
                        for (int h = (v / (span)) % 2 == 0 ? 0 : span; h < span * 2; h += span * 2)
                        {
                            g.FillRectangle(gpen, new System.Drawing.Rectangle(h, v, span, span));
                        }
                    }
                }

                var b = new ImageBrush(NetImageProcessHelper.ToBitmapImage(bg))
                {
                    Stretch = Stretch.None,
                    TileMode = TileMode.Tile,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    Viewport = new Rect(new Point(0, 0), new Point(span * 2, span * 2)),
                    ViewportUnits = BrushMappingMode.Absolute
                };
                ChessboardBrushes.Add(blockPixSize, b);
                return b;
            }
        }

        /// <summary>
        /// color in hex string to (a,r,g,b);
        /// #FFFEFDFC   ->  Tuple(255,254,253,252),
        /// #FEFDFC     ->  Tuple(255,254,253,252)
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static Tuple<byte, byte, byte, byte> HexColorToArgb(string hexColor)
        {
            byte a = 255;
            byte r = 160;
            byte g = 160;
            byte b = 160;
            if (string.IsNullOrWhiteSpace(hexColor))
                return new Tuple<byte, byte, byte, byte>(a, r, g, b);
            hexColor = hexColor.Trim();

            //remove the # at the front
            var hex = hexColor?.Replace("#", "");

            int start = 0;

            if (hex?.Length != 8 && hex?.Length != 6)
                throw new ArgumentException("Error hex color string length.");
            //handle ARGB strings (8 characters long)
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                start = 2;
            }
            //convert RGB characters to bytes
            r = byte.Parse(hex.Substring(start, 2), System.Globalization.NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(start + 2, 2), System.Globalization.NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(start + 4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Tuple<byte, byte, byte, byte>(a, r, g, b);
        }

        /// <summary>
        /// color in (a,r,g,b) to hex string(len = 8);
        /// (255,254,253,252) -> #FFFEFDFC,
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static string ArgbToHexColor(byte a, byte r, byte g, byte b)
        {
            var arr = new[] { a, r, g, b };
            string hex = BitConverter.ToString(arr).Replace("-", string.Empty).ToUpper();
            return $"#{hex}";
        }

        /// <summary>
        /// color in (a,r,g,b) to hex string(len = 6);
        /// (255,254,253,252) -> #FFFEFDFC,
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static string ArgbToHexColor(byte r, byte g, byte b)
        {
            var arr = new[] { r, g, b };
            string hex = BitConverter.ToString(arr).Replace("-", string.Empty).ToUpper();
            return $"#{hex}";
        }

        public static System.Windows.Media.Color HexColorToMediaColor(string hexColor)
        {
            var t = HexColorToArgb(hexColor);
            return System.Windows.Media.Color.FromArgb(t.Item1, t.Item2, t.Item3, t.Item4);
        }

        public static System.Drawing.Color HexColorToDrawingColor(string hexColor)
        {
            var t = HexColorToArgb(hexColor);
            return System.Drawing.Color.FromArgb(t.Item1, t.Item2, t.Item3, t.Item4);
        }

        public static string ColorToHexColor(this System.Windows.Media.Color color, bool showAlpha = false)
        {
            if (showAlpha)
            {
                return ArgbToHexColor(color.A, color.R, color.G, color.B);
            }
            else
            {
                return ArgbToHexColor(color.R, color.G, color.B);
            }
        }

        public static string ColorToHexColor(this System.Drawing.Color color, bool showAlpha = false)
        {
            if (showAlpha)
            {
                return ArgbToHexColor(color.A, color.R, color.G, color.B);
            }
            else
            {
                return ArgbToHexColor(color.R, color.G, color.B);
            }
        }

        public static System.Drawing.Brush ColorToDrawingBrush(this System.Drawing.Color color)
        {
            var b = new System.Drawing.SolidBrush(color);
            return b;
        }

        public static System.Drawing.Brush ColorToDrawingBrush(this System.Windows.Media.Color color)
        {
            var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Drawing.Brush ColorToDrawingBrush(string hexColor)
        {
            var color = HexColorToDrawingColor(hexColor);
            var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(this System.Drawing.Color color)
        {
            var b = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(this System.Windows.Media.Color color)
        {
            var b = new System.Windows.Media.SolidColorBrush(color);
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(string hexColor)
        {
            var color = HexColorToMediaColor(hexColor);
            var b = new System.Windows.Media.SolidColorBrush(color);
            return b;
        }



        public static bool ColorIsTransparent(this System.Windows.Media.Color color)
        {
            return color.A < 20;
        }
        public static bool ColorIsTransparent(this System.Drawing.Color color)
        {
            return color.A < 20;
        }
        public static bool ColorIsTransparent(string hexColor)
        {
            try
            {
                var color = ColorAndBrushHelper.HexColorToMediaColor(hexColor);
                return color.A < 20;
            }
            catch
            {
                return true;
            }
        }
    }
}