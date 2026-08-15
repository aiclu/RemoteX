using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Shawn.Utils;

namespace Demo
{
    public class VmDisplayItem : NotifyPropertyChangedBase
    {
        private readonly string _originalString;
        public VmDisplayItem(string originalString)
        {
            _originalString = originalString;
            DispNameControl = OrgDispNameControl;
        }

        public object OrgDispNameControl =>
            new TextBlock()
            {
                Text = _originalString,
            };


        private object _dispNameControl = null;
        public object DispNameControl
        {
            get => _dispNameControl;
            set => SetAndNotifyIfChanged(nameof(DispNameControl), ref _dispNameControl, value);
        }


        private Visibility _objectVisibility = Visibility.Visible;
        public Visibility ObjectVisibility
        {
            get => _objectVisibility;
            set => SetAndNotifyIfChanged(nameof(ObjectVisibility), ref _objectVisibility, value);
        }
    }
}
