using System.Collections;
using System.Runtime.InteropServices;

namespace Shawn.Utils.Wpf.Native
{
    public class ShlwapiStrCmpLogicalW : IComparer
    {
        [System.Runtime.InteropServices.DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string param1, string param2);

        public int Compare(object? name1, object? name2)
        {
            if (name1 == null || name2 == null)
            {
                return -1;
            }
            else
            {
                return StrCmpLogicalW(name1.ToString()!, name2.ToString()!);
            }
        }
    }
}
