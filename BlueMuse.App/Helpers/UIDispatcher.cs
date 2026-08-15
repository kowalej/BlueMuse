using Microsoft.UI.Dispatching;

namespace BlueMuse.Helpers
{
    /// <summary>
    /// Provides access to the application's UI thread DispatcherQueue from any thread.
    /// Must be initialized once from the UI thread (e.g. in App's constructor) before use,
    /// since background threads (e.g. DeviceWatcher callbacks, timers) cannot obtain a
    /// DispatcherQueue for themselves via DispatcherQueue.GetForCurrentThread().
    /// </summary>
    public static class UIDispatcher
    {
        public static DispatcherQueue Queue { get; private set; }

        public static void Initialize()
        {
            if (Queue == null)
                Queue = DispatcherQueue.GetForCurrentThread();
        }
    }
}
