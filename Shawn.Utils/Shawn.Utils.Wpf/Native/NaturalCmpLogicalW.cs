using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

namespace Shawn.Utils.Wpf.Native
{
    /// <summary>
    /// 用于实现 Windows explorer like 的文件名排序
    /// Usage: Array.Sort(array, NaturalCmpLogicalW.Get());
    /// </summary>
    public class NaturalCmpLogicalW : IComparer
    {
        [System.Runtime.InteropServices.DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string param1, string param2);

        public int Compare(object? name1, object? name2)
        {
            if (name1 == null || name2 == null)
            {
                return -1;
            }

            return name1 switch
            {
                DirectoryInfo d1 when name2 is DirectoryInfo d2 => StrCmpLogicalW(d1.Name, d2.Name),
                FileInfo f1 when name2 is FileInfo f2 => StrCmpLogicalW(f1.Name, f2.Name),
                string s1 when name2 is string s2 => StrCmpLogicalW(s1, s2),
                _ => throw new NotSupportedException()
            };
        }

        private static readonly NaturalCmpLogicalW Obj = new NaturalCmpLogicalW();
        public static NaturalCmpLogicalW Get()
        {
            return Obj;
        }
    }
}
