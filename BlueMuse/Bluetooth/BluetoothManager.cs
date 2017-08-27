using BlueMuse.MuseManagement;
using BlueMuse.Helpers;
using System;
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
        private ObservableCollection<Muse> muses;
        private DeviceWatcher museDeviceWatcher;
        private bool museDeviceWatcherReset = false;
        private volatile bool LSLBridgeLaunched = false;
        private static readonly Object syncLock = new object();
        Timer pollMuseTimer; 

        public BluetoothManager(ObservableCollection<Muse> muses) {
            this.muses = muses;
            App.Current.Suspending += Current_Suspending; // Close off streams when application exiting.    
        }

        private async void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            var def = e.SuspendingOperation.GetDeferral();
            await StopStreamingAll();
            await DeactivateLSLBridge();
            def.Complete();
        }

        public void FindMuses()
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.ItemNameDisplay" };
            museDeviceWatcher = DeviceInformation.CreateWatcher(Constants.ALL_AQS, requestedProperties, DeviceInformationKind.AssociationEndpoint);

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

        public async void ForceRefresh()
        {
            if (museDeviceWatcher.Status != DeviceWatcherStatus.Stopped && museDeviceWatcher.Status != DeviceWatcherStatus.Stopping)
            {
                await StopStreamingAll();
                foreach (var muse in muses)
                {
                    // Remove event handler and dispose.
                    if (muse.Device != null)
                    {
                        muse.Device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                        muse.Dispose();
                    }
                }
                muses.Clear();
            }
            museDeviceWatcherReset = true;
            museDeviceWatcher.Stop();
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            // Filter out Muses. A name filter is the best method currently, since wildcards are not supported in AQS string.
            // A more robust method may be to query for a Muse specific GAAT service, however this requires devices to be powered on, and even if the device was previously paired with the machine, the service won't be cached.
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

                // Retreive an arbitrary service. This will allow the device to auto connect.
                await device.GetGattServicesForUuidAsync(Constants.MUSE_TOGGLE_STREAM_UUID);

                lock (muses)
                {
                    var muse = muses.FirstOrDefault(x => x.Id == args.Id);
                    if (muse != null)
                    {
                        muse.Id = device.DeviceId;
                        muse.Name = device.Name;
                        muse.Status = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                    }
                    else
                    {
                        muse = new Muse(device, device.Name, device.DeviceId, device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline);
                        muses.Add(muse);
                    }
                    ResolveAutoStream(muse);
                }

                // Must watch for status changed because Added and Updated are not always called upon connecting or disconnecting.
                device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
            }
        }

        private void ResolveAutoStream(Muse muse)
        {
            if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                if (Constants.StreamFirst && muses.IndexOf(muse) == 0)
                {
                    Constants.StreamFirst = false;
                    StartStreaming(muse.Id);
                }
                else if (Constants.MusesToAutoStream.Any(x => x == muse.MacAddress))
                {
                    Constants.MusesToAutoStream.Remove(muse.MacAddress);
                    StartStreaming(muse.Id);
                }
            }
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // Again, filter for Muses.
            var muse = muses.FirstOrDefault(x => x.Id == args.Id);
            if (muse != null)
            {
                var device = muse.Device;
                muse.Id = device.DeviceId;
                muse.Name = device.Name;
                muse.Status = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
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
            var muse = muses.FirstOrDefault(x => x.Id == sender.DeviceId);
            if (muse != null)
            {
                muse.Status = sender.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
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
            if (LSLBridgeLaunched && muses.Where(x => x.IsStreaming).Count() < 1)
            {
                await AppService.AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_CLOSE_BRIDGE, new Windows.Foundation.Collections.ValueSet());
                lock (syncLock)
                    LSLBridgeLaunched = false;
            }
        }

        public async void StartStreaming(object museId)
        {
            var muse = muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                await ActivateLSLBridge();
                await muse.ToggleStream(true);
            }        
        }

        public async void StopStreaming(object museId)
        {
            var muse = muses.SingleOrDefault(x => x.Id == (string)museId);
            if (muse != null)
            {
                await muse.ToggleStream(false);
                //await DeactivateLSLBridge();
            }
        }

        public async Task StartStreamingAll()
        {
            var muses = this.muses.Where(x => !x.IsStreaming);
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
            var muses = this.muses.Where(x => x.IsStreaming);
            if (muses.Count() > 0)
            {
                foreach (var muse in muses)
                {
                    await muse.ToggleStream(false);
                }
                //await DeactivateLSLBridge();
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
                foreach (var muse in muses)
                {
                    if (muse.Device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                    {
                        // Retreive an arbitrary service. This will allow the device to auto connect.
                        await muse.Device.GetGattServicesForUuidAsync(Constants.MUSE_TOGGLE_STREAM_UUID);
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
