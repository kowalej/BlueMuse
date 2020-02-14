using BlueMuse.Helpers;
using BlueMuse.MuseManagement;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
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
        private bool museDeviceWatcherReset = false;
        private volatile bool LSLBridgeLaunched = false;
        private static readonly object syncLock = new object();
        Timer pollMuseTimer;
        Timer pollBridge;

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
            pollBridge = new Timer(PollBridge, null, 0, 500);
        }

        public async void PollBridge(object state)
        {
            if(LSLBridgeLaunched)
                await AppService.AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_KEEP_ACTIVE, new Windows.Foundation.Collections.ValueSet());
        }

        public async void Close()
        {
            await StopStreamingAll();
            await DeactivateLSLBridge();
            await Task.Delay(2000); // This delay ensures LSL bridge gets shutdown in time.
        }

        public void FindMuses()
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.ItemNameDisplay" };
            museDeviceWatcher = DeviceInformation.CreateWatcher(Constants.DEVICE_AQS, requestedProperties, DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            museDeviceWatcher.Added += DeviceWatcher_Added;
            museDeviceWatcher.Updated += DeviceWatcher_Updated;
            //museDeviceWatcher.Removed += DeviceWatcher_Removed; // Omitted - removing from list causes issues.

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
                    await device.GetGattServicesForUuidAsync(Constants.MUSE_GATT_COMMAND_UUID);

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
                            muse = new Muse(device, device.Name, device.DeviceId, device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline);
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

        public void ResolveAutoStreamAll()
        {
            foreach(var muse in Muses)
            {
                if (muse.ConnectionStatus == MuseConnectionStatus.Online)
                    ResolveAutoStream(muse);
            }
        }

        private void ResolveAutoStream(Muse muse)
        {
            if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                if (StreamFirst && Muses.IndexOf(muse) == 0)
                {
                    StreamFirst = false;
                    StartStreaming(muse.Id);
                }
                else
                {
                    string find = MusesToAutoStream.FirstOrDefault(x => x == muse.MacAddress || x == muse.Name);
                    if(!string.IsNullOrEmpty(find)) {
                        MusesToAutoStream.Remove(muse.MacAddress);
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
            pollMuseTimer = new Timer(PollMuses, new AutoResetEvent(false), 0, 5); // Poll every 5 seconds to allow Muses to auto-reconnect if they went offline.
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

        public async Task ActivateLSLBridge()
        {
            lock (syncLock)
            {
                if (LSLBridgeLaunched)
                    return;
                LSLBridgeLaunched = true;
            }
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public async Task DeactivateLSLBridge()
        {
            if (LSLBridgeLaunched && Muses.Where(x => x.IsStreaming).Count() < 1)
            {
                await AppService.AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_CLOSE_BRIDGE, new Windows.Foundation.Collections.ValueSet());
                lock (syncLock)
                    LSLBridgeLaunched = false;
            }
        }

        public async void StartStreaming(object museId)
        {
            var muse = Muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                await ActivateLSLBridge();
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
            var muses = this.Muses.Where(x => !x.IsStreaming);
            if (muses.Count() > 0)
            {
                await ActivateLSLBridge();
                foreach (var muse in muses)
                {
                    await muse.ToggleStream(true);
                }
            }
        }

        public async Task StopStreamingAll()
        {
            var muses = this.Muses.Where(x => x.IsStreaming);
            if (muses.Count() > 0)
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
                foreach (var muse in Muses)
                {
                    if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                    {
                        // Retreive an arbitrary service. This will allow the device to auto connect.
                        await muse.Device.GetGattServicesForUuidAsync(Constants.MUSE_GATT_COMMAND_UUID);
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
