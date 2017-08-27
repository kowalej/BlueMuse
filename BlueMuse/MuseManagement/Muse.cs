using BlueMuse.AppService;
using BlueMuse.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;

namespace BlueMuse.MuseManagement
{
    public class Muse : ObservableObject, IDisposable
    {
        private static readonly Object syncLock = new object();

        public BluetoothLEDevice Device;

        private GattDeviceService deviceService;
        private List<GattCharacteristic> channels;

        private volatile Dictionary<UInt16, MuseSample> sampleBuffer;

        private string name;
        public string Name { get { return name; } set { SetProperty(ref name, value); OnPropertyChanged(nameof(LongName)); } }

        private string id;
        public string Id {
            get { return id; }
            set
            {
                lock (syncLock)
                {
                    SetProperty(ref id, value);
                    OnPropertyChanged(nameof(MacAddress));
                    OnPropertyChanged(nameof(LongName));
                }
            }
        }

        private MuseConnectionStatus status;
        public MuseConnectionStatus Status
        {
            get { return status; }
            set
            {
                lock (syncLock)
                {
                    SetProperty(ref status, value);
                    OnPropertyChanged(nameof(CanStream));
                }
            }
        }

        private bool isStreaming;
        public bool IsStreaming { get { return isStreaming; } set { lock(syncLock) SetProperty(ref isStreaming, value); } }

        private bool isSelected;
        public bool IsSelected { get { return isSelected; } set { lock (syncLock) SetProperty(ref isSelected, value); } }

        public bool CanStream { get { return status == MuseConnectionStatus.Online; } }
        public string LongName { get { return string.Format("{0} ({1})", Name, MacAddress); } }
        public string MacAddress
        {
            get
            {
                Regex deviceIdRegex = new Regex(@"^*(\w{2}:){5}\w{2}");
                string museId = Id;
                Match matches = deviceIdRegex.Match(museId);
                if (matches.Success)
                    museId = matches.Value;
                return museId;
            }
        }

        int channelCount;
        Guid[] channelUUIDs;

        public Muse(BluetoothLEDevice device, string name, string id, MuseConnectionStatus status)
        {
            Device = device;
            Name = name;
            Id = id;
            Status = status;
            if (name.Contains(Constants.DeviceNameFilter[0]))
            {
                channelCount = Constants.MUSE_CHANNEL_COUNT;
                channelUUIDs = Constants.MUSE_CHANNEL_UUIDS;
            }
            else
            {
                channelCount = Constants.MUSE_SMXT_CHANNEL_COUNT;
                channelUUIDs = Constants.MUSE_SMXT_CHANNEL_UUIDS;
            }
        }

        // Flag: Has Dispose already been called?
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if(Device != null)
                    Device.Dispose();
            }

            // Free any unmanaged objects here.
            //

            disposed = true;
        }

        public async Task ToggleStream(bool start)
        {
            try
            {
                if (start)
                {
                    await LSLOpenStream();
                    if (channels == null) channels = new List<GattCharacteristic>();
                    // Get GATT service on start, therefore it will be already available when stopping.
                    deviceService = (await Device.GetGattServicesForUuidAsync(Constants.MUSE_DATA_SERVICE_UUID)).Services.First();
                }

                var characteristics = (await deviceService.GetCharacteristicsAsync()).Characteristics;
                foreach (var c in channelUUIDs)
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

                for (int i = 0; i < channels.Count; i++)
                {
                    if (start)
                    {
                        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> d = (s, a) => Channel_ValueChanged(s, a);
                        channels[i].ValueChanged += Channel_ValueChanged;
                        sampleBuffer = new Dictionary<ushort, MuseSample>();
                    }
                    else channels[i].ValueChanged -= Channel_ValueChanged;
                    await channels[i].WriteClientCharacteristicConfigurationDescriptorAsync(notify);
                }

                // Tell Muse to start or stop notifications.
                await characteristics.Single(x => x.Uuid == Constants.MUSE_TOGGLE_STREAM_UUID).WriteValueWithResultAsync(buffer);
            }
            catch (InvalidOperationException) { if(isStreaming) CloseOffStream(); return; }

            if (!start)
            {
                CloseOffStream(); // Don't have to keep service reference around anymore. The handlers for the channels will also stop.
            }
            IsStreaming = start;
        }

        private async void CloseOffStream()
        {
            channels.Clear();
            await LSLCloseStream();
            deviceService.Dispose();
            IsStreaming = false;
        }

        private async Task LSLOpenStream()
        {
            ValueSet message = new ValueSet();
            message.Add(Constants.LSL_MESSAGE_MUSE_NAME, LongName);
            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_OPEN_STREAM, message);
        }

        private async Task LSLCloseStream()
        {
            ValueSet message = new ValueSet();
            message.Add(Constants.LSL_MESSAGE_MUSE_NAME, LongName);
            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM, message);
        }

        private async Task LSLPushChunk(MuseSample sample)
        {
            ValueSet message = new ValueSet();
            message.Add(Constants.LSL_MESSAGE_MUSE_NAME, LongName);
            float[] data = new float[Constants.MUSE_SAMPLE_COUNT * channelCount]; // Can only send 1D array with this garbage :S
            double[] timestamps = new double[Constants.MUSE_SAMPLE_COUNT];

            for (int i = 0; i < channelCount; i++)
            {
                var channelData = sample.ChannelData[channelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                {
                    if (i == 1)
                        timestamps[j] = sample.TimeStamps[j];
                    data[(i * j) + j] = channelData[j];
                }
            }
          
            message.Add(Constants.LSL_MESSAGE_CHUNK_DATA, data);
            message.Add(Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, timestamps);
                
            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);            
        }

        private float[] GetTimeSamples(BitArray bitData)
        {
            // Extract our 12, 12-bit samples.
            float[] timeSamples = new float[Constants.MUSE_SAMPLE_COUNT];
            for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
            {
                timeSamples[i] = bitData.ToUInt12(16 + (i * 12)); // Initial offset by 16 bits for the timestamp.
                timeSamples[i] = (timeSamples[i] - 2048) * 0.48828125f; // 12 bits on a 2 mVpp range.
            }
            return timeSamples;
        }

        private async void Channel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                byte[] rawData = new byte[args.CharacteristicValue.Length];
                using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
                {
                    reader.ReadBytes(rawData);
                    BitArray bitData = new BitArray(rawData);
                    UInt16 museTimestamp = bitData.ToUInt16(0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MuseSample sample;
                    lock (sampleBuffer)
                    {
                        if (!sampleBuffer.ContainsKey(museTimestamp))
                        {
                            sample = new MuseSample();
                            sample.BaseTimeStamp = args.Timestamp; // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                            sampleBuffer.Add(museTimestamp, sample);
                        }
                        else sample = sampleBuffer[museTimestamp];

                        // Get time samples.
                        sample.ChannelData[sender.Uuid] = GetTimeSamples(bitData);
                    }
                    // If we have all 5 channels, we can push the 12 samples for each channel.
                    if (sample.ChannelData.Count == channelCount)
                    {
                        await LSLPushChunk(sample);
                        lock(sampleBuffer)
                            sampleBuffer.Remove(museTimestamp);
                    }
                }
            }
        }
    }
}
