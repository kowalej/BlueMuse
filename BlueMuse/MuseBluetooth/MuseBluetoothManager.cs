using BlueMuse.DataObjects;
using BlueMuse.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace BlueMuse.MuseBluetooth
{
    public class MuseBluetoothManager
    {
        object syncLock = new object();
        public volatile ObservableCollection<Muse> Muses = new ObservableCollection<Muse>();
        private DeviceWatcher museDeviceWatcher;
        private bool museDeviceWatcherReset = false;
        private Timer pollMusesTimer;

        public MuseBluetoothManager() {
            App.Current.Suspending += Current_Suspending; // Close off streams when application exiting.    
        }

        private async void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            await StopStreamingAll();
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
                foreach (var muse in Muses)
                {
                    // Remove event handler and dispose.
                    muse.Device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                    try
                    {
                        if (muse.Device != null) muse.Device.Dispose();
                    }
                    catch (InvalidOperationException) { } // Shouldn't occur, but is possible to be thrown.
                }
                Muses.Clear();
                museDeviceWatcher.Stop();
                museDeviceWatcherReset = true;
            }
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            // Filter out Muses. A name filter is the best method currently, since wildcards are not supported in AQS string.
            // A more robust method may be to query for a Muse specific GAAT service, however this requires devices to be powered on, and even if the device was previously paired with the machine, the service won't be cached.
            if (args.Name.Contains("Muse"))
            {
                var device = await BluetoothLEDevice.FromIdAsync(args.Id);
                Debug.WriteLine("Device Name: " + device.Name); 
                Debug.WriteLine("Current Connection Status: " + device.ConnectionStatus);

                // List all services and characteristics. For debugging.
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

                Muse muse = Muses.FirstOrDefault(x => x.Id == args.Id);
                lock (syncLock)
                {
                    if (muse != null)
                    {
                        muse.Id = device.DeviceId;
                        muse.Name = device.Name;
                        muse.Status = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                    }
                    else
                    {
                        Muses.Add(new Muse(device, device.Name, device.DeviceId, device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline));
                    }
                }

                // Must watch for status changed because Added and Updated are not always called upon connecting or disconnecting.
                device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
            }
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // Again, filter for Muses.
            Muse muse = Muses.FirstOrDefault(x => x.Id == args.Id);
            if (muse != null)
            {
                var device = muse.Device;
                lock (syncLock)
                {
                    muse.Id = device.DeviceId;
                    muse.Name = device.Name;
                    muse.Status = device.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                }
            }
        }

        private void MuseDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            var autoEvent = new AutoResetEvent(false);
            if (pollMusesTimer == null)
                pollMusesTimer = new Timer(PollMuses, autoEvent, 0, 30); // Poll every 30 seconds to allow Muses to auto-reconnect if they went offline.
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
            Muse muse = Muses.FirstOrDefault(x => x.Id == sender.DeviceId);
            if (muse != null)
            {
                lock (syncLock)
                    muse.Status = sender.ConnectionStatus == BluetoothConnectionStatus.Connected ? MuseConnectionStatus.Online : MuseConnectionStatus.Offline;
                if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected && muse.IsStreaming) StopStreaming(muse);
                Debug.WriteLine(string.Format("Device: {0} is now {1}.", sender.Name, sender.ConnectionStatus));
            }
        }

        public async void StartStreaming(object museId)
        {
            await ToggleStream((string)museId, true);
        }

        public async void StopStreaming(object museId)
        {
            await ToggleStream((string)museId, false);
        }

        public async Task StartStreamingAll()
        {
            foreach (var muse in Muses.Where(x => !x.IsStreaming))
            {
                await ToggleStream(muse.Id, true);
            }
        }

        public async Task StopStreamingAll()
        {
            foreach (var muse in Muses.Where(x => x.IsStreaming).ToList())
            {
                await ToggleStream(muse.Id, false);
            }
        }

        private async Task ToggleStream(string museId, bool start)
        {
            Muse muse =  Muses.FirstOrDefault(x => x.Id == museId);
            var device = muse.Device;

            try
            {
                if (start) // Get GATT service on start, therefore it will be already available when stopping.
                    muse.DeviceService = (await device.GetGattServicesForUuidAsync(Constants.MUSE_DATA_SERVICE_UUID)).Services.First();
                
                var characteristics = (await muse.DeviceService.GetCharacteristicsAsync()).Characteristics;
                var channels = new List<GattCharacteristic>();
                foreach (var c in Constants.MUSE_CHANNEL_UUIDS)
                {
                    channels.Add(characteristics.Single(x => x.Uuid == c));
                }

                GattClientCharacteristicConfigurationDescriptorValue notify;
                byte[] toggleData;

                if (start)
                {
                    notify = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                    toggleData = Constants.MUSE_TOGGLE_STREAM_START;
                }
                else
                {
                    notify = GattClientCharacteristicConfigurationDescriptorValue.None;
                    toggleData = Constants.MUSE_TOGGLE_STREAM_STOP;
                }
                var buffer = WindowsRuntimeBuffer.Create(toggleData, 0, toggleData.Length, toggleData.Length);

                for(int i = 0; i < channels.Count; i++)
                {
                    await channels[i].WriteClientCharacteristicConfigurationDescriptorAsync(notify);
                    if (start) {
                        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (s, a) => { Channel_ValueChanged(s, a, muse); };
                        channels[i].ValueChanged += handler;
                        muse.ChannelEventHandlers[i] = handler; // Keep track of handlers (since we use lambda to pass in muse reference) for removal later on.
                        muse.SampleBuffer = new Dictionary<ushort, MuseSample>();
                    }
                    else channels[i].ValueChanged -= muse.ChannelEventHandlers[i];
                }

                // Tell Muse to start or stop notifications.
                await characteristics.Single(x => x.Uuid == Constants.MUSE_TOGGLE_STREAM_UUID).WriteValueWithResultAsync(buffer);
            }
            catch (InvalidOperationException) { muse.DeviceService.Dispose(); return; }

            lock (syncLock)
                muse.IsStreaming = start;
            if (!start)
                muse.DeviceService.Dispose(); // Don't have to keep service reference around anymore. The handlers for the channels will also stop.
        }

        private float[] GetTimeSamples(BitArray bitData)
        {
            // Extract our 12, 12-bit samples.
            float[] timeSamples = new float[12];
            for (int i = 0; i < 12; i++)
            {
                timeSamples[i] = bitData.ToUInt12(16 + (i * 12)); // Initial offset by 16 bits for the timestamp.
                timeSamples[i] = (timeSamples[i] - 2048) * 0.48828125f; // 12 bits on a 2 mVpp range.
            }
            return timeSamples;
        }

        private void PushSampleLSL(MuseSample sample, LSL.liblsl.StreamOutlet stream)
        {
            float[,] data = new float[12,5];
            for(int i = 0; i < Constants.MUSE_CHANNEL_UUIDS.Length; i++)
            {
                var channelData = sample.ChannelData[Constants.MUSE_CHANNEL_UUIDS[i]]; // Maintains muse-lsl.py ordering.
                for(int j = 0; j < channelData.Length; j++)
                {
                    data[j, i] = channelData[j];
                }
            }
            stream.push_chunk(data, sample.TimeStamps);
        }

        private void TestLSLStream()
        {
            LSL.liblsl.StreamInfo[] results = LSL.liblsl.resolve_stream("type", "EEG");
            // open an inlet and print some interesting info about the stream (meta-data, etc.)
            LSL.liblsl.StreamInlet inlet = new LSL.liblsl.StreamInlet(results[0]);
            inlet.open_stream();
            Debug.Write(inlet.info().as_xml());
            float[,] data = new float[12, 5];
            double[] timestamps = new double[12];
            var chunk = inlet.pull_chunk(data, timestamps, 100);
            for (int i = 0; i < 12; i++)
            {
                Debug.WriteLine(timestamps[i]);

                for (int j = 0; j < 5; j++)
                {
                    Debug.Write(data[i, j]);
                }
                Debug.Write("\n");
            }
            inlet.close_stream();
        }

        private void Channel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args, Muse muse)
        {
            byte[] rawData = new byte[args.CharacteristicValue.Length];
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(rawData);
                BitArray bitData = new BitArray(rawData);
                UInt16 museTimestamp = bitData.ToUInt16(0); // Zero bit offset, since first 16 bits represent Muse timestamp.

                lock (syncLock)
                {
                    MuseSample sample;
                    if (!muse.SampleBuffer.ContainsKey(museTimestamp))
                    {
                        sample = new MuseSample();
                        sample.BaseTimeStamp = DateTime.Now; // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                        muse.SampleBuffer[museTimestamp] = sample;
                    }
                    else sample = muse.SampleBuffer[museTimestamp];

                    // Get time samples.
                    sample.ChannelData[sender.Uuid] = GetTimeSamples(bitData);

                    // If we have all 5 channels, we can push the 12 samples for each channel.
                    if (sample.ChannelData.Count == 5)
                    {
                        PushSampleLSL(sample, muse.LSLStream);
                        muse.SampleBuffer.Remove(museTimestamp);
                    }
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
