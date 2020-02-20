using BlueMuse.Helpers;
using BlueMuse.MuseManagement;
using System.Linq;
using System.Windows.Input;
using BlueMuse.Bluetooth;
using System.Threading;
using Windows.ApplicationModel;
using System.Collections.Generic;
using BlueMuse.Misc;
using BlueMuse.Settings;
using System.IO;

namespace BlueMuse.ViewModels
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public class MainPageVM : ObservableObject
    {
        public AppSettings AppSettings;
        private BluetoothManager museManager;
        public ObservableCollection<Muse> Muses { get; set; }
        private Muse selectedMuse; // Tracks user selection from list.
        public Muse SelectedMuse { get { return selectedMuse; } set { selectedMuse = value; if (value != null) SetSelectedMuse(value); } }
        private string searchText = string.Empty;
        public string SearchText { get { return searchText; } set { SetProperty(ref searchText, value); } }
        private readonly Timer searchTextAnimateTimer;
        public List<BaseTimestampFormat> TimestampFormats = TimestampFormatsContainer.TimestampFormats; // Use copy in case we want view level filtering.
        public List<BaseTimestampFormat> TimestampFormats2 = TimestampFormatsContainer.TimestampFormats2; // Use copy in case we want view level filtering.
        public List<ChannelDataType> ChannelDataTypes = ChannelDataTypesContainer.ChannelDataTypes; // Use copy in case we want view level filtering.
        public string BlueMuseLogFolder;
        public string LSLBridgeLogFolder;

        public string AppVersion { get {
                var pv = Package.Current.Id.Version;
                return $"BlueMuse Version {pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";
            }
        }

        public MainPageVM()
        {
            AppSettings = AppSettings.Instance;
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            BlueMuseLogFolder = Path.Combine(localFolder, "Logs");
            LSLBridgeLogFolder = BlueMuseLogFolder.Replace("LocalState", "LocalCache\\Local\\BlueMuse-LSL");

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

        private ICommand resetMuse;
        public ICommand ResetMuse
        {
            get
            {
                return resetMuse ?? (resetMuse = new CommandHandler((param) =>
                {
                    museManager.ResetMuse(param);
                }, true));
            }
        }

        private ICommand refreshDeviceInfoAndControlStatus;
        public ICommand RefreshDeviceInfoAndControlStatus
        {
            get
            {
                return refreshDeviceInfoAndControlStatus ?? (refreshDeviceInfoAndControlStatus = new CommandHandler((param) =>
                {
                    museManager.RefreshDeviceInfoAndControlStatus(param);
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
