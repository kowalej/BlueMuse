using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace LSLBridge.Helpers
{
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        protected async override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(
                () =>
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

        protected async override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(
                () =>
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
