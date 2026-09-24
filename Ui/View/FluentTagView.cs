using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using _1RM.Model;

namespace _1RM.View;

// Never sort the default view: classic tag bars share the source collection.
internal sealed class FluentTagView : IDisposable
{
    internal ListCollectionView View { get; }

    internal FluentTagView(ObservableCollection<Tag> tags)
    {
        View = new ListCollectionView(tags);
        using (View.DeferRefresh())
        {
            View.SortDescriptions.Add(new SortDescription(nameof(Tag.IsPinned), ListSortDirection.Descending));
            View.SortDescriptions.Add(new SortDescription(nameof(Tag.CustomOrder), ListSortDirection.Ascending));
            View.SortDescriptions.Add(new SortDescription(nameof(Tag.Name), ListSortDirection.Ascending));
            View.LiveSortingProperties.Add(nameof(Tag.IsPinned));
            View.LiveSortingProperties.Add(nameof(Tag.CustomOrder));
            View.LiveSortingProperties.Add(nameof(Tag.Name));
            View.IsLiveSorting = true;
        }
    }

    public void Dispose()
    {
        View.IsLiveSorting = false;
        View.DetachFromSourceCollection();
    }
}
