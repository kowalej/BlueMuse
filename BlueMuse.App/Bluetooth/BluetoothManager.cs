using BlueMuse.LSL;
using BlueMuse.Helpers;
using BlueMuse.MuseManagement;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BlueMuse.Bluetooth
{
    public class BluetoothManager
    {
        public static bool AlwaysPair { get; set; }
        public ObservableCollection<Muse> Muses;
        private DeviceWatcher museDeviceWatcher;
        public HashSet<string> MusesToAutoStream = new HashSet<string>();
        public bool StreamFirst = false;

        // When set, every Muse that comes online (now or discovered later) should be auto-streamed.
        // This is needed because "startall" can be sent before any devices have been discovered by
        // the DeviceWatcher (e.g. at app launch), so we can't rely on only streaming what's already
        // in the Muses collection at the moment the command is received.
        public bool AutoStreamAll = false;
        private bool museDeviceWatcherReset = false;
        private static readonly object syncLock = new object();
        Timer pollMuseTimer;

        public ObservableCollection<BlueMuseLSLStream> LSLStreams { get; } = new ObservableCollection<BlueMuseLSLStream>();
        public BlueMuseLSLStreamManager LSLStreamManager { get; }
        private int lslStreamCount;
        public int LSLStreamCount { get { return lslStreamCount; } private set { lslStreamCount = value; } }

        private static volatile BluetoothManager instance;
        public static BluetoothManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncLock)
                    {
                        if (instance == null)
                            instance = new BluetoothManager();
                    }
                }
                return instance;
            }
        }

        private BluetoothManager() {
            Muses = new ObservableCollection<Muse>();
            LSLStreamManager = new BlueMuseLSLStreamManager(LSLStreams, count => LSLStreamCount = count);
        }

        public async void Close()
        {
            await StopStreamingAll();
            LSLStreamManager.CloseAllStreams();
            await Task.Delay(200);
        }

        public void FindMuses()
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.ItemNameDisplay" };
            museDeviceWatcher = DeviceInformation.CreateWatcher(Constants.DEVICE_AQS, requestedProperties, DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            museDeviceWatcher.Added += DeviceWatcher_Added;
            museDeviceWatcher.Updated += DeviceWatcher_Updated;
            museDeviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            museDeviceWatcher.EnumerationCompleted += MuseDeviceWatcher_EnumerationCompleted;
            museDeviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            museDeviceWatcher.Start();
        }

        public void ForceRefresh()
        {
            if (museDeviceWatcher.Status != DeviceWatcherStatus.Stopped && museDeviceWatcher.Status != DeviceWatcherStatus.Stopping)
            {
                for(int i = 0; i < Muses.Count; i++)
                {
                    var muse = Muses[i];
                    if (!muse.IsStreaming)
                    {
                        // Remove event handler and dispose.
                        if (muse.Device != null)
                        {
                            muse.Device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                        }
                        muse.Dispose();
                        Muses.Remove(muse);
                    }
                }
            }
            museDeviceWatcherReset = true;
            museDeviceWatcher.Stop();
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                // Filter out Muses. A name filter is the best method currently, since wildcards are not supported in AQS string.
                // A more robust method may be to query for a Muse specific GATT service, however this requires devices to be powered on, and even if the device was previously paired with the machine, the service won't be cached.
                if (Constants.DeviceNameFilter.Any(x => args.Name.Contains(x)))
                {
                    var device = await BluetoothLEDevice.FromIdAsync(args.Id);
                    
                    Debug.WriteLine("Device Name: " + device.Name);
                    Debug.WriteLine("Current Connection Status: " + device.ConnectionStatus);

                    // For debugging - list all services and characteristics. 
                    //var services = await device.GetGattServicesAsync();
                    //foreach(var service in services.Services)
                    //{
                    //    var characteristics = await service.GetCharacteristicsAsync();
                    //    Debug.WriteLine("Service: " + service.Uuid + " Handle: " + service.AttributeHandle);
                    //    foreach(var characteristic in characteristics.Characteristics)
                    //    {
                    //        Debug.WriteLine("Characteristic: " + characteristic.Uuid + " Handle: " + characteristic.AttributeHandle + " Description: " + characteristic.UserDescription);
                    //    }
                    //}

                    var muse = Muses.FirstOrDefault(x => x.Id == args.Id);

                    // Don't try to pair an actively streaming Muse.
                    if (muse == null || (muse != null && !muse.IsStreaming))
                    {
                        var di = await DeviceInformation.CreateFromIdAsync(args.Id);

                        // Always re-pair device via BlueMuse if AlwaysPair is "on".
                        if (AlwaysPair && di.Pairing != null && di.Pairing.IsPaired && di.Pairing.CanPair)
                        {
                            await di.Pairing.UnpairAsync();
                        }
                        if (AlwaysPair && di.Pairing != null && !di.Pairing.IsPaired && di.Pairing.CanPair)
                        {
                            await di.Pairing.PairAsync();
                        }
                    }

                    // Retreive an arbitrary service. This will allow the device to auto connect.
                    // Skip this if we already know about this Muse and it's actively streaming or already
                    // connected - an extra GATT round-trip here can collide with in-progress GATT operations
                    // (stream toggling, device info refresh) on the same device and cause spurious disconnects.
                    if (muse == null || (!muse.IsStreaming && device.ConnectionStatus != BluetoothConnectionStatus.Connected))
                    {
                        await device.GetGattServicesForUuidAsync(Constants.MUSE_GATT_COMMAND_UUID);
                    }

                    lock (Muses)
                    {
                        muse = Muses.FirstOrDefault(x => x.Id == args.Id);
                        if (muse != null)
                        {
                            muse.Id = device.DeviceId;
                            muse.Name = device.Name;
                            muse.ConnectionStatus = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                        }
                        else
                        {
                            muse = new Muse(device, device.Name, device.DeviceId, device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline, LSLStreamManager);
                            Muses.Add(muse);
                        }
                        ResolveAutoStream(muse);
                    }

                    // Must watch for status changed because Added and Updated are not always called upon connecting or disconnecting.
                    device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                    device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, $"Exception during find device (DeviceWatcher_Added) (device ID={args.Id}).");
            }
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (Muses)
            {
                var muse = Muses.FirstOrDefault(x => x.Id == args.Id);
                // AQS can raise a Removed event for a device that's still actually connected (e.g. transient
                // enumeration churn). Only drop it from the list if it's truly not streaming AND not connected,
                // otherwise we cause the device to visibly disappear from the UI while still functional.
                if (muse != null && !muse.IsStreaming &&
                    (muse.Device == null || muse.Device.ConnectionStatus != BluetoothConnectionStatus.Connected))
                {
                    if (muse.Device != null)
                    {
                        muse.Device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                    }
                    muse.Dispose();
                    Muses.Remove(muse);
                }
            }
        }

        public void ResolveAutoStreamAll()
        {
            Muse[] muses;
            lock (Muses)
            {
                muses = Muses.ToArray();
            }

            foreach (var muse in muses)
            {
                if (muse.ConnectionStatus == MuseConnectionStatus.Online)
                    ResolveAutoStream(muse);
            }
        }

        private void ResolveAutoStream(Muse muse)
        {
            if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                if (StreamFirst && Muses.Count(x => x.IsStreaming) == 0)
                {
                    StreamFirst = false;
                    StartStreaming(muse.Id);
                }
                else if (AutoStreamAll)
                {
                    if (!muse.IsStreaming) StartStreaming(muse.Id);
                }
                else
                {
                    string find = MusesToAutoStream.FirstOrDefault(x => x == muse.MacAddress || x == muse.Name);
                    if(!string.IsNullOrEmpty(find)) {
                        MusesToAutoStream.Remove(find);
                        StartStreaming(muse.Id);
                    }
                }
            }
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // Again, filter for Muses.
            var muse = Muses.FirstOrDefault(x => x.Id == args.Id);
            if (muse != null)
            {
                var device = muse.Device;
                muse.Id = device.DeviceId;
                muse.Name = device.Name;
                muse.ConnectionStatus = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
            }
        }

        private void MuseDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            pollMuseTimer = new Timer(PollMuses, new AutoResetEvent(false), 0, 5000); // Poll every 5 seconds to allow Muses to auto-reconnect if they went offline.
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            if (museDeviceWatcherReset)
            {
                museDeviceWatcherReset = false;
                museDeviceWatcher.Start();
            }
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            var muse = Muses.FirstOrDefault(x => x.Id == sender.DeviceId);
            if (muse != null)
            {
                muse.ConnectionStatus = sender.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected && muse.IsStreaming) StopStreaming(muse.Id);
                else ResolveAutoStream(muse);
                Debug.WriteLine(string.Format("Device: {0} is now {1}.", sender.Name, sender.ConnectionStatus));
            }
        }

        public async void StartStreaming(object museId)
        {
            var muse = Muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                await muse.ToggleStream(true);
            }        
        }

        public async void StopStreamingAddress(string address)
        {
            var muse = Muses.SingleOrDefault(x => x.MacAddress == address || x.Name == address);
            if (muse != null)
            {
                await muse.ToggleStream(false);
            }
        }

        public async void StopStreaming(object museId)
        {
            var muse = Muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                await muse.ToggleStream(false);
            }
        }

        public async void ResetMuse(object museId)
        {
            var muse = Muses.SingleOrDefault(x => x.Id == (string)museId);
            if(muse != null)
            {
                await muse.Reset();
            }
        }

        public void RefreshDeviceInfoAndControlStatus(object museId)
        {
            var muse = Muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                muse.RefreshDeviceInfoAndControlStatus();
            }
        }

        public async Task StartStreamingAll()
        {
            // Also flag so that any Muse discovered/connected after this point (e.g. because
            // discovery hasn't found it yet when "startall" was sent) gets streamed automatically.
            AutoStreamAll = true;

            Muse[] muses;
            lock (Muses)
            {
                muses = Muses.Where(x => !x.IsStreaming).ToArray();
            }
            if (muses.Length > 0)
            {
                foreach (var muse in muses)
                {
                    await muse.ToggleStream(true);
                }
            }
        }

        public async Task StopStreamingAll()
        {
            AutoStreamAll = false;

            Muse[] muses;
            lock (Muses)
            {
                muses = Muses.Where(x => x.IsStreaming).ToArray();
            }
            if (muses.Length > 0)
            {
                foreach (var muse in muses)
                {
                    await muse.ToggleStream(false);
                }
            }
        }

        /// <summary>
        /// Poll arbitrary service regularily to allows Muses to automatically connect at any time.
        /// </summary>
        /// <returns></returns>
        private async void PollMuses(object stateInfo)
        {
            try
            {
                // Snapshot under the same lock used elsewhere when mutating Muses, so we don't
                // enumerate a collection that's concurrently being modified by the device watcher
                // (which runs on a different thread). Locks can't safely span an await, so we copy
                // first and then do the async work against the snapshot.
                Muse[] muses;
                lock (Muses)
                {
                    muses = Muses.ToArray();
                }

                foreach (var muse in muses)
                {
                    if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                    {
                        // Retreive an arbitrary service. This will allow the device to auto connect.
                        await muse.WarmupConnectionAsync();
                    }
                }
            }
            // Can occur if collection is currently being modified.
            catch (InvalidOperationException)
            {
                return;
            }
        }
    }
}
