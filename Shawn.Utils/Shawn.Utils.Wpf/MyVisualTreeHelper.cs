using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shawn.Utils.Wpf
{
    public static class MyVisualTreeHelper
    {
        #region Static Func
        public static DependencyObject? GetParent(DependencyObject reference)
        {
            return System.Windows.Media.VisualTreeHelper.GetParent(reference);
        }

        /// <summary>
        /// 在虚拟树向上机搜索指定类型的元素
        /// </summary>
        /// <example>例如</example>
        /// <code>
        /// if (MyVisualTreeHelper.VisualUpwardSearch[ListBoxItem](e.OriginalSource as DependencyObject) is ListBoxItem lbi)
        ///     if (lbi.Content is ProtocolBaseViewModel baseViewModel)
        ///         baseViewModel.Show();
        /// </code>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? VisualUpwardSearch<T>(DependencyObject? obj) where T : DependencyObject
        {
            DependencyObject? current = obj;
            while (current != null)
            {
                if (current is T o && current != obj)
                {
                    return o;
                }
                current = GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 取得指定位置处的 ListViewItem
        /// </summary>
        /// <param name="lvSender"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static ListViewItem? GetItemOnPosition(ScrollContentPresenter lvSender, Point position)
        {
            HitTestResult r = System.Windows.Media.VisualTreeHelper.HitTest(lvSender, position);
            if (r == null)
            {
                return null;
            }
            var obj = r.VisualHit;
            while (obj is not ListView && obj != null)
            {
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
                if (obj is ListViewItem item)
                {
                    return item;
                }
            }
            return null;
        }

        //http://stackoverflow.com/questions/665719/wpf-animate-listbox-scrollviewer-horizontaloffset
        public static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            /******
            // 按住鼠标滚动滚轮时，显示选取框
            var onScrollChange = (RoutedEventHandler)delegate (object sender, RoutedEventArgs args)
            {
                ItemsPresenter ip = VisualTreeHelper.FindVisualChild<ItemsPresenter>(sender as ListView);
                ScrollContentPresenter p = VisualTreeHelper.VisualUpwardSearch<ScrollContentPresenter>(ip);
                if (GetIsDragging(p) && Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    UpdatePosition(p, true);
                }
            };
              ******/

            // Search immediate children first (breadth-first)
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T o)
                {
                    return o;
                }
                else
                {
                    T? childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }



        public static List<T> FindVisualChilds<T>(DependencyObject obj) where T : DependencyObject
        {
            /******
            // 按住鼠标滚动滚轮时，显示选取框
            var onScrollChange = (RoutedEventHandler)delegate (object sender, RoutedEventArgs args)
            {
                ItemsPresenter ip = VisualTreeHelper.FindVisualChild<ItemsPresenter>(sender as ListView);
                ScrollContentPresenter p = VisualTreeHelper.VisualUpwardSearch<ScrollContentPresenter>(ip);
                if (GetIsDragging(p) && Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    UpdatePosition(p, true);
                }
            };
              ******/

            var list = new List<T>();
            // Search immediate children first (breadth-first)
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T o)
                {
                    list.Add(o);
                }
                else
                {
                    list.AddRange(FindVisualChilds<T>(child));
                }
            }
            return list;
        }

        public static FrameworkElement? FindElementByName(this TreeViewItem treeViewItem, string name)
        {
            if (treeViewItem == null) return null;
            // 尝试在当前 TreeViewItem 的模板中查找
            FrameworkElement element = treeViewItem.Template?.FindName(name, treeViewItem) as FrameworkElement;
            if (element != null)
                return element;

            // 遍历 TreeViewItem 的视觉子元素
            int childCount = VisualTreeHelper.GetChildrenCount(treeViewItem);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(treeViewItem, i);
                // 如果子元素是 FrameworkElement 并且名称匹配
                if (child is FrameworkElement frameworkElement && frameworkElement.Name == name)
                    return frameworkElement;
                // 递归查找子元素中的目标元素
                if (child != null)
                {
                    FrameworkElement result = FindElementInVisualTree(child, name);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        private static FrameworkElement? FindElementInVisualTree(DependencyObject parent, string name)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement element && element.Name == name)
                    return element;
                var result = FindElementInVisualTree(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }



        #endregion Static Func
    }
}