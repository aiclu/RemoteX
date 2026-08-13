using System;

namespace Dragablz.Dockablz
{
    internal class FloatTransfer
    {
        public FloatTransfer(double width, double height, object content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            Content = content;
        }

        public static FloatTransfer TakeSnapshot(DragablzItem dragablzItem, TabablzControl sourceTabControl)
        {
            if (dragablzItem == null) throw new ArgumentNullException(nameof(dragablzItem));

            return new FloatTransfer(sourceTabControl.ActualWidth, sourceTabControl.ActualHeight, dragablzItem.UnderlyingContent ?? dragablzItem.Content ?? dragablzItem);
        }

        public object Content { get; }
    }
}