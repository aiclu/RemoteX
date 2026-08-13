using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace Shawn.Utils.Wpf
{
    public static class Executor
    {
        private static readonly object Locker = new object();
        private static Action<System.Action>? _executor = null;

        private static void InitExecutor()
        {
            lock (Locker)
            {
                if (_executor == null)
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    _executor = action =>
                    {
                        if (dispatcher.CheckAccess())
                            action();
                        else
                            dispatcher.BeginInvoke(action);
                    };
                }
            }
        }

        public static void OnUIThread(this System.Action action)
        {
            InitExecutor();
            Debug.Assert(_executor != null);
            if (_executor != null)
                _executor(action);
        }

        public static Exception? TryCatch(this System.Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                return e;
            }
            return null;
        }

        public static Exception? TryCatchOnUIThread(this System.Action action)
        {
            InitExecutor();
            Debug.Assert(_executor != null);
            try
            {
                if (_executor != null)
                    _executor(action);
            }
            catch (Exception e)
            {
                return e;
            }
            return null;
        }

        public static void WriteToLog(this Exception? e)
        {
            if (e != null)
                SimpleLogHelper.Error(e);
        }
    }
}
