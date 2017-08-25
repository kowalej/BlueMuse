using LSLBridge.Helpers;
using LSLBridge.LSLManagement;

namespace LSLBridge.ViewModels
{
    public class MainWindowVM : ObservableObject
    {
        private MuseLSLStreamManager museLSLStreamManager;
        public ObservableCollection<MuseLSLStream> MuseStreams { get; set; }
        private int museStreamCount;
        public int MuseStreamCount { get { return museStreamCount; } set { SetProperty(ref museStreamCount, value); } }

        public MainWindowVM()
        {
            MuseStreamCount = 0;
            MuseStreams = new ObservableCollection<MuseLSLStream>();
            museLSLStreamManager = new MuseLSLStreamManager(MuseStreams, (s) => MuseStreamCount = s);
        }
    }
}
