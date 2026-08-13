using System;

namespace Dragablz.Dockablz
{
    /// <summary>
    /// Provides information about where a tab control is withing a layout structure.
    /// </summary>
    public class LocationReport
    {
        //TODO I've internalised constructor for now, so I can come back and add Window without breaking.

        internal LocationReport(TabablzControl tabablzControl, Layout rootLayout)
            : this(tabablzControl, rootLayout, null, false)
        { }

        internal LocationReport(TabablzControl tabablzControl, Layout rootLayout, Branch? parentBranch, bool isSecondLeaf)
        {
            if (tabablzControl == null) throw new ArgumentNullException("tabablzControl");
            if (rootLayout == null) throw new ArgumentNullException("rootLayout");

            TabablzControl = tabablzControl;
            RootLayout = rootLayout;
            ParentBranch = parentBranch;
            //IsLeaf = ParentBranch != null;
            IsSecondLeaf = isSecondLeaf;
        }

        public TabablzControl TabablzControl { get; }

        public Layout RootLayout { get; }

        /// <summary>
        /// Gets the parent branch if this is a leaf. If the <see cref="TabablzControl"/> is directly under the <see cref="RootLayout"/> will be <c>null</c>.
        /// </summary>
        public Branch? ParentBranch { get; }

        ///// <summary>
        ///// Idicates if this is a leaf in a branch. <c>True</c> if <see cref="ParentBranch"/> is not null.
        ///// </summary>
        //[Obsolete("use: ParentBranch != null")]
        //public bool IsLeaf { get; }

        public bool IsSecondLeaf { get; }
    }
}