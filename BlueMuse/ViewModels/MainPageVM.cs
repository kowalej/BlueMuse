using BlueMuse.Helpers;
using BlueMuse.MuseManagement;
using System.Linq;
using System.Windows.Input;
using BlueMuse.Bluetooth;
using System.Threading;
using Windows.ApplicationModel;
using System.Collections.Generic;
using BlueMuse.Misc;

namespace BlueMuse.ViewModels
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public class MainPageVM : ObservableObject
    {
        private BluetoothManager museManager;
        public ObservableCollection<Muse> Muses { get; set; }
        private Muse selectedMuse; // Tracks user selection from list.
        public Muse SelectedMuse { get { return selectedMuse; } set { selectedMuse = value; if (value != null) SetSelectedMuse(value); } }
        private string searchText = string.Empty;
        public string SearchText { get { return searchText; } set { SetProperty(ref searchText, value); } }
        private Timer searchTextAnimateTimer;
        private string timestampFormat;
        public string TimestampFormat { get { return timestampFormat; } set { SetProperty(ref timestampFormat, value); } }
        private bool sendSecondaryTimestamp;
        public bool SendSecondaryTimestamp { get { return sendSecondaryTimestamp; } set { SetProperty(ref sendSecondaryTimestamp, value); } }
        public List<string> TimestampOptions = new List<string>()
        {
            new BlueMuseUnixTimestampFormat().DisplayName,
            new LSLTimestampFormat().DisplayName,
        };
        public string AppVersion { get {
                var pv = Package.Current.Id.Version;
                return $"BlueMuse Version {pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";
            }
        }

        public MainPageVM()
        {
            museManager = BluetoothManager.Instance;
            Muses = museManager.Muses;
            museManager.FindMuses();
            searchTextAnimateTimer = new Timer(SearchTextAnimate, null, 0, 600); // Start the Searching for Muses... animation.
        }

        private void SearchTextAnimate(object state)
        {
            string baseText = "Searching for Muses";
            if (searchText.Count(x => x == '.') == 0)
                SearchText = baseText + ".";
            else if (searchText.Count(x => x == '.') == 1)
                SearchText = baseText + "..";
            else if (searchText.Count(x => x == '.') == 2)
                SearchText = baseText + "...";
            else SearchText = baseText;
        }

        private ICommand forceRefresh;
        public ICommand ForceRefresh
        {
            get
            {
                return forceRefresh ?? (forceRefresh = new CommandHandler(() =>
                {
                    museManager.ForceRefresh();
                }, true));
            }
        }

        private ICommand startStreaming;
        public ICommand StartStreaming
        {
            get
            {
                return startStreaming ?? (startStreaming = new CommandHandler((param) =>
                {
                    museManager.StartStreaming(param);
                }, true));
            }
        }

        private ICommand stopStreaming;
        public ICommand StopStreaming
        {
            get
            {
                return stopStreaming ?? (stopStreaming = new CommandHandler((param) =>
                {
                    museManager.StopStreaming(param);
                }, true));
            }
        }

        private void SetSelectedMuse(Muse muse)
        {
            var selectedMuses = Muses.Where(x => x.IsSelected);
            foreach (var selectedMuse in selectedMuses)
            {
                selectedMuse.IsSelected = false;
            }
            muse.IsSelected = true;
        }
    }
}
