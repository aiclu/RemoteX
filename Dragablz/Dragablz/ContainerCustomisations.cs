using System;
using System.Windows;

namespace Dragablz
{
    internal class ContainerCustomisations
    {
        public ContainerCustomisations(Func<DragablzItem>? getContainerForItemOverride = null, Action<DependencyObject, object>? prepareContainerForItemOverride = null, Action<DependencyObject, object>? clearingContainerForItemOverride = null)
        {
            GetContainerForItemOverride = getContainerForItemOverride;
            PrepareContainerForItemOverride = prepareContainerForItemOverride;
            ClearingContainerForItemOverride = clearingContainerForItemOverride;
        }

        public Func<DragablzItem>? GetContainerForItemOverride { get; }

        public Action<DependencyObject, object>? PrepareContainerForItemOverride { get; }

        public Action<DependencyObject, object>? ClearingContainerForItemOverride { get; }
    }
}