using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Dragablz.Core;

namespace Dragablz.Dockablz
{ 
    public class BranchAccessor
    {
        private readonly Branch _branch;

        public BranchAccessor(Branch branch)
        {
            if (branch == null) throw new ArgumentNullException("branch");

            _branch = branch;

            if (branch.FirstItem is Branch firstChildBranch)
                FirstItemBranchAccessor = new BranchAccessor(firstChildBranch);
            else
            {
                Debug.Assert(branch.FirstContentPresenter != null);
                FirstItemTabablzControl = FindTabablzControl(branch.FirstItem, branch.FirstContentPresenter);
            }

            if (branch.SecondItem is Branch secondChildBranch)
                SecondItemBranchAccessor = new BranchAccessor(secondChildBranch);
            else
            {
                Debug.Assert(branch.SecondContentPresenter != null);
                SecondItemTabablzControl = FindTabablzControl(branch.SecondItem, branch.SecondContentPresenter);
            }
        }

        private static TabablzControl? FindTabablzControl(object item, DependencyObject contentPresenter)
        {
            var result = item as TabablzControl;
            return result ?? contentPresenter.VisualTreeDepthFirstTraversal().OfType<TabablzControl>().FirstOrDefault();
        }

        public Branch Branch => _branch;

        public BranchAccessor? FirstItemBranchAccessor { get; }

        public BranchAccessor? SecondItemBranchAccessor { get; }

        public TabablzControl? FirstItemTabablzControl { get; }

        public TabablzControl? SecondItemTabablzControl { get; }

        /// <summary>
        /// Visits the content of the first or second side of a branch, according to its content type.  No more than one of the provided <see cref="Action"/>
        /// callbacks will be called.  
        /// </summary>
        /// <param name="childItem"></param>
        /// <param name="childBranchVisitor"></param>
        /// <param name="childTabablzControlVisitor"></param>
        /// <param name="childContentVisitor"></param>
        /// <returns></returns>
        public BranchAccessor Visit(BranchItem childItem,
            Action<BranchAccessor>? childBranchVisitor = null,
            Action<TabablzControl>? childTabablzControlVisitor = null,
            Action<object>? childContentVisitor = null)
        {
            Func<BranchAccessor> branchGetter;
            Func<TabablzControl> tabGetter;
            Func<object> contentGetter;

            switch (childItem)
            {
                case BranchItem.First:
                    Debug.Assert(FirstItemBranchAccessor != null);
                    Debug.Assert(FirstItemTabablzControl != null);
                    branchGetter = () => FirstItemBranchAccessor!;
                    tabGetter = () => FirstItemTabablzControl!;
                    contentGetter = () => _branch.FirstItem;
                    break;
                case BranchItem.Second:
                    Debug.Assert(SecondItemBranchAccessor != null);
                    Debug.Assert(SecondItemTabablzControl != null);
                    branchGetter = () => SecondItemBranchAccessor;
                    tabGetter = () => SecondItemTabablzControl;
                    contentGetter = () => _branch.SecondItem;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("childItem");
            }

            var branchDescription = branchGetter();
            if (branchDescription != null)
            {
                if (childBranchVisitor != null)
                    childBranchVisitor(branchDescription);
                return this;
            }
            
            var tabablzControl = tabGetter();
            if (tabablzControl != null)
            {
                if (childTabablzControlVisitor != null)
                    childTabablzControlVisitor(tabablzControl);

                return this;
            }

            if (childContentVisitor == null) return this;

            var content = contentGetter();
            if (content != null)
                childContentVisitor(content);

            return this;
        }
    }    
}
