using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace BlueMuse.Helpers
{
    /// <summary>
    /// An ObservableCollection that is safe to mutate (Add/Remove/etc.) from background threads.
    ///
    /// The base ObservableCollection`1 raises CollectionChanged synchronously as part of the mutation
    /// call (InsertItem/RemoveItem/etc.) and tracks "reentrancy" via a shared counter that isn't
    /// thread-scoped. Previously only the *notification* was marshaled to the UI thread (asynchronously),
    /// while the actual mutation still ran synchronously on whichever background thread called it (e.g.
    /// a Bluetooth DeviceWatcher callback). If a second mutation happened on a background thread while a
    /// previously-queued notification was still executing on the UI thread, CheckReentrancy() incorrectly
    /// treated it as a reentrant modification and threw InvalidOperationException, even though the two
    /// calls were on different threads and not truly reentrant.
    ///
    /// To fix this, we marshal the entire mutation (which raises the event as part of the same call) onto
    /// the UI thread and block the calling thread until it completes. This serializes all mutations and
    /// their notifications on a single thread, so reentrancy tracking works correctly, while callers on
    /// background threads still observe the mutation as complete by the time the call returns.
    /// </summary>
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        private static void RunOnUIThread(Action action)
        {
            var queue = UIDispatcher.Queue;
            if (queue == null || queue.HasThreadAccess)
            {
                try { action(); } catch { }
                return;
            }

            using (var completed = new ManualResetEventSlim(false))
            {
                bool enqueued = queue.TryEnqueue(DispatcherQueuePriority.High, () =>
                {
                    try { action(); }
                    catch { }
                    finally { completed.Set(); }
                });

                if (!enqueued)
                    return;

                completed.Wait();
            }
        }

        protected override void InsertItem(int index, T item)
        {
            RunOnUIThread(() => base.InsertItem(index, item));
        }

        protected override void RemoveItem(int index)
        {
            RunOnUIThread(() => base.RemoveItem(index));
        }

        protected override void SetItem(int index, T item)
        {
            RunOnUIThread(() => base.SetItem(index, item));
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            RunOnUIThread(() => base.MoveItem(oldIndex, newIndex));
        }

        protected override void ClearItems()
        {
            RunOnUIThread(() => base.ClearItems());
        }
    }
}
