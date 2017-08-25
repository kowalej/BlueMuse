using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace LSLBridge.Helpers
{
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        protected async override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(
            () =>
                {
                    base.OnCollectionChanged(e);
                }
            );
        }

        protected async override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(
            () =>
                {
                    base.OnPropertyChanged(e);
                }
            ); 
        }
    }
}
