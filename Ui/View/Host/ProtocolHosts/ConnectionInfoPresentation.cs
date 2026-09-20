using System;

namespace _1RM.View.Host.ProtocolHosts
{
    internal static class ConnectionInfoPresentation
    {
        // Native errors must still reach the snapshot, not the last-resort message box.
        public static void Show(Func<bool> tryShowNative, Action showSnapshot, Action<Exception> logFailure)
        {
            bool shown;
            try
            {
                shown = tryShowNative();
            }
            catch (Exception e)
            {
                logFailure(e);
                shown = false;
            }

            if (!shown)
                showSnapshot();
        }

        public static bool TryShowRdp(Func<bool> isControlAvailable, Func<int> getConnected, Func<bool> show)
        {
            // Only read COM connection state after checking the captured control's lifetime.
            return isControlAvailable() && getConnected() == 1 && show();
        }
    }
}
