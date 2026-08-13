using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Shawn.Utils.Wpf.PageHost
{
    public partial class PageHost : UserControl
    {
        public static readonly DependencyProperty NewPageProperty = DependencyProperty.Register("NewPage",
            typeof(AnimationPage),
            typeof(PageHost),
            new PropertyMetadata(null, new PropertyChangedCallback(OnNewPagePropertyChanged)));

        private static void OnNewPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PageHost)d).Show(e.NewValue as AnimationPage);
        }

        public AnimationPage NewPage
        {
            get => (AnimationPage)GetValue(NewPageProperty);
            set => SetValue(NewPageProperty, value);
        }

        public static readonly DependencyProperty ContentWidthProperty = DependencyProperty.Register("ContentWidth", typeof(double), typeof(PageHost), new PropertyMetadata(double.NaN, ContentWidthPropertyPropertyChangedCallback));

        private static void ContentWidthPropertyPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PageHost)d).GridContent.Width = (double)e.NewValue;
        }

        public double ContentWidth
        {
            get => (double)GetValue(ContentWidthProperty);
            set => SetValue(ContentWidthProperty, value);
        }

        public static readonly DependencyProperty ContentHeightProperty = DependencyProperty.Register("ContentHeight", typeof(double), typeof(PageHost), new PropertyMetadata(double.NaN, ContentHeightPropertyChangedCallback));

        private static void ContentHeightPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PageHost)d).GridContent.Height = (double)e.NewValue;
        }

        public double ContentHeight
        {
            get => (double)GetValue(ContentHeightProperty);
            set => SetValue(ContentHeightProperty, value);
        }

        public PageHost()
        {
            InitializeComponent();
        }

        private AnimationPage? _oldPage = null;
        private AnimationPage? _newPage = null;
        private Storyboard? _animationForLoad = null;
        private Storyboard? _animationForUnload = null;

        public void Show(AnimationPage? newPage)
        {
            _oldPage = _newPage;
            _newPage = newPage;

            Dispatcher.Invoke(delegate
            {
                NewPageHost.Content = null;
                OldPageHost.Content = null;
                int w = (int)(this.ActualWidth * 1.2);
                int h = (int)(this.ActualHeight * 1.2);

                if (_oldPage?.Content != null)
                {
                    try
                    {
                        _oldPage.Content.Loaded -= OnNewPageLoaded;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    _animationForUnload = _oldPage.GetOutAnimationStoryboard(w, h);
                    if (_animationForUnload != null)
                    {
                        OldPageHost.Visibility = Visibility.Visible;
                        OldPageHost.Content = _oldPage.Content;
                        _animationForUnload.Completed += OnForUnloadStoryboardCompleted;
                        OldPageHost.BeginAnimation(ContentControl.MarginProperty, null);
                        OldPageHost.BeginAnimation(ContentControl.OpacityProperty, null);
                        _animationForUnload.Begin(OldPageHost);
                    }
                    else
                    {
                        OldPageHost.Visibility = Visibility.Collapsed;
                        OldPageHost.Content = null;
                        _oldPage = null;
                    }
                }

                if (_newPage?.Content != null)
                {
                    _animationForLoad = _newPage.GetInAnimationStoryboard(w, h);
                    _newPage.Content.Loaded += OnNewPageLoaded;
                    NewPageHost.Content = _newPage.Content;
                }
            });
        }

        /// <summary>
        /// release old content after story board ended
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnForUnloadStoryboardCompleted(object? sender, EventArgs e)
        {
            OldPageHost.Visibility = Visibility.Collapsed;
            OldPageHost.Content = null;
            _oldPage = null;
            if (_animationForUnload != null)
            {
                _animationForUnload.Completed -= OnForUnloadStoryboardCompleted;
                _animationForUnload = null;
            }
        }

        private void OnNewPageLoaded(object sender, RoutedEventArgs e)
        {
            _animationForLoad?.Begin(NewPageHost);
        }
    }
}