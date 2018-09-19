using LSLBridge.Helpers;
using LSLBridge.LSL;
using System.Windows;

namespace LSLBridge.ViewModels
{
    public class MainWindowVM : ObservableObject
    {
        private LSLStreamManager LSLStreamManager;
        public ObservableCollection<LSLStream> Streams { get; set; }
        private int streamCount;
        public int StreamCount
        {
            get { return streamCount;
        }
            set {
                SetProperty(ref streamCount, value);
                if (value < 1)
                    WindowVisible = Visibility.Hidden;
                else
                    WindowVisible = Visibility.Visible;
            }
        }
        private Visibility windowVisible = Visibility.Hidden;
        public Visibility WindowVisible { get { return windowVisible; } set { SetProperty(ref windowVisible, value); } }

        public MainWindowVM()
        {
            StreamCount = 0;
            Streams = new ObservableCollection<LSLStream>();
            LSLStreamManager = new LSLStreamManager(Streams, (s) => StreamCount = s);
        }
    }
}
