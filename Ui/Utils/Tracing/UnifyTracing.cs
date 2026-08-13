using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _1RM.Utils.Tracing
{
    internal static class UnifyTracing
    {
        public static void Init()
        {
            SentryIoHelper.Init(Assert.SENTRY_IO_DEN);
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, Dictionary<string, string>? attachments = null)
        {
            SentryIoHelper.Error(e, properties, attachments);
        }

        public static void TraceSpecial(Dictionary<string, string> kys)
        {
            SentryIoHelper.TraceSpecial(kys);
        }
    }
}
