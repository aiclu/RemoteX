using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils.Wpf
{
    public static class ResourceUriHelper
    {
        public static string GetUriPathFromCurrentAssembly(string resourcePath)
        {
            return $"pack://application:,,,/{resourcePath.TrimStart('/')}";
        }


        public static string GetUriPathFromAssembly(string nameSpace, string resourcePath)
        {
            return $"pack://application:,,,/{nameSpace};component/{resourcePath.TrimStart('/')}";
        }

        public static Uri GetUriFromCurrentAssembly(string resourcePath)
        {
            return new Uri(GetUriPathFromCurrentAssembly(resourcePath));
        }


        public static Uri GetUriFromAssembly(string nameSpace, string resourcePath)
        {
            return new Uri(GetUriPathFromAssembly(nameSpace, resourcePath));
        }
    }
}
