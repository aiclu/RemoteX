using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Shawn.Utils.Wpf.PageHost
{
    public static class StoryboardHelpers
    {
        public static void AddSlideFromRight(
            this Storyboard storyboard,
            double seconds, double parentWidth,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(parentWidth, 0, -parentWidth, 0);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideFromLeft(
            this Storyboard storyboard,
            double seconds, double parentWidth,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(-parentWidth, 0, parentWidth, 0);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideToRight(
            this Storyboard storyboard,
            double seconds, double parentWidth,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(parentWidth, 0, -parentWidth, 0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideToLeft(
            this Storyboard storyboard, 
            double seconds, double parentWidth,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(-parentWidth, 0, parentWidth, 0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideFromTop(
            this Storyboard storyboard,
            double seconds,
            double parentHeight,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0, -parentHeight, 0, parentHeight);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideFromBottom(
            this Storyboard storyboard,
            double seconds,
            double parentHeight,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0, parentHeight, 0, -parentHeight);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideToTop(
            this Storyboard storyboard, 
            double seconds, 
            double parentHeight,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(0, -parentHeight, 0, parentHeight);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddSlideToBottom(
            this Storyboard storyboard, 
            double seconds, 
            double parentHeight,
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(0, parentHeight, 0, -parentHeight);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", delaySeconds, decelerationRatio);
        }

        public static void AddFadeIn(this Storyboard storyboard, 
            double seconds, 
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            AddDoubleAnimation(storyboard, seconds, null, 1, "Opacity", delaySeconds, decelerationRatio);
        }

        public static void AddFadeOut(this Storyboard storyboard, 
            double seconds, 
            double delaySeconds = 0,
            float decelerationRatio = 0.9f)
        {
            AddDoubleAnimation(storyboard, seconds, null, 0, "Opacity", delaySeconds, decelerationRatio);
        }

        public static void AddDoubleAnimation(
            this Storyboard storyboard, 
            double seconds,
            double? from, double to,
            string propertyName,
            double delaySeconds,
            float decelerationRatio)
        {
            var animation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                From = from,
                To = to,
                DecelerationRatio = decelerationRatio,
            };
            if (delaySeconds > 0)
                animation.BeginTime = new TimeSpan(0, 0, 0, 0, (int)(delaySeconds * 1000));
            Storyboard.SetTargetProperty(animation, new PropertyPath(propertyName));
            storyboard.Children.Add(animation);
        }

        /// <summary>
        /// 为 storyboard 添加一个针对 Margin 属性的线性插值动画
        /// </summary>
        /// <param name="storyboard"></param>
        /// <param name="seconds"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="propertyName">动画将被应用到哪个属性</param>
        /// <param name="delaySeconds">延迟启动</param>
        /// <param name="decelerationRatio">动画加速度，一般0.9</param>
        public static void AddThicknessAnimation(
            this Storyboard storyboard, 
            double seconds,
            Thickness from, Thickness to,
            string propertyName,
            double delaySeconds,
            float decelerationRatio)
        {
            var animation = new ThicknessAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                From = from,
                To = to,
                DecelerationRatio = decelerationRatio
            };
            if (delaySeconds > 0)
                animation.BeginTime = new TimeSpan(0, 0, 0, 0, (int)(delaySeconds * 1000));
            Storyboard.SetTargetProperty(animation, new PropertyPath(propertyName));
            storyboard.Children.Add(animation);
        }
    }
}