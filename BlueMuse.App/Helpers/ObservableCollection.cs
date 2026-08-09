using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace BlueMuse.Helpers
{
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                dispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, () =>
                    {
                        try
                        {
                            base.OnCollectionChanged(e);
                        }
                        catch { }
                    }
                );
            }
            catch { }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            try
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                dispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, () =>
                    {
                        try
                        {
                            base.OnPropertyChanged(e);
                        }
                        catch { }
                    }
                );
            }
            catch { }
        }
    }
}
