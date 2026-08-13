using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils
{
    public static class RetryHelper
    {
        /// <summary>
        /// RUN action with retry count, make a sleep between each retry, return true if success, false if failed, run actionOnError if failed
        /// </summary>
        /// <returns></returns>
        public static bool Try(Action action, int retryCount = 3, int sleepMilliseconds = 100, Action<Exception>? actionOnError = null)
        {
            var success = false;
            Exception? e = null;
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    action();
                    success = true;
                    break;
                }
                catch(Exception ex)
                {
                    e = ex;
                }
                if (i < retryCount - 1)
                    System.Threading.Thread.Sleep(sleepMilliseconds);
            }

            if (!success && actionOnError != null && e != null)
                actionOnError(e);
            return success;
        }
    }
}
