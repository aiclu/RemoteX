using System;
using System.Threading;

namespace Dragablz.Referenceless
{
    internal sealed class AnonymousDisposable : ICancelable, IDisposable
    {
        private volatile Action? _dispose;

        public bool IsDisposed => this._dispose == null;

        public AnonymousDisposable(Action dispose)
        {
            this._dispose = dispose;
        }

        public void Dispose()
        {
            var action = Interlocked.Exchange<Action?>(ref _dispose, null);
            action?.Invoke();
        }
    }
}
