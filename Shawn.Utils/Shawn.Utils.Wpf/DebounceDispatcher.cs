using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Shawn.Utils.Wpf
{
    /*

    debounceTimer.Debounce(500, parm =>
    {
        Model.AppModel.Window.ShowStatus("Searching topics...");
        Model.TopicsFilter = TextSearchText.Text;
        Model.AppModel.Window.ShowStatus();
    });

     */
    public class DebounceDispatcher
    {
        private DispatcherTimer? _timer;

        public void Debounce(int interval, Action<object?> action,
            object? actionParameter = null,
            DispatcherPriority priority = DispatcherPriority.ApplicationIdle,
            Dispatcher? dispatcher = null)
        {
            // kill pending timer and pending ticks
            _timer?.Stop();
            _timer = null;

            dispatcher ??= Dispatcher.CurrentDispatcher;

            // timer is recreated for each event and effectively
            // resets the timeout. Action only fires after timeout has fully
            // elapsed without other events firing in between
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(interval), priority, (s, e) =>
            {
                if (_timer == null)
                    return;

                _timer?.Stop();
                _timer = null;
                action.Invoke(actionParameter);
            }, dispatcher);

            _timer.Start();
        }
    }
}
