using LSLBridge.Helpers;
using LSLBridge.LSLManagement;
using System.Windows;

namespace LSLBridge.ViewModels
{
    public class MainWindowVM : ObservableObject
    {
        private MuseLSLStreamManager museLSLStreamManager;
        public ObservableCollection<MuseLSLStream> MuseStreams { get; set; }
        private int museStreamCount;
        public int MuseStreamCount
        {
            get { return museStreamCount;
        }
            set {
                SetProperty(ref museStreamCount, value);
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
            MuseStreamCount = 0;
            MuseStreams = new ObservableCollection<MuseLSLStream>();
            museLSLStreamManager = new MuseLSLStreamManager(MuseStreams, (s) => MuseStreamCount = s);
        }
    }
}
