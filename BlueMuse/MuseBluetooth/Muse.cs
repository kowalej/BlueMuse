using BlueMuse.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BlueMuse.MuseBluetooth
{
    public enum MuseConnectionStatus
    {
        Online = 0,
        Offline = 1
    }

    public class MuseSample
    {
        private DateTimeOffset baseTimeStamp;
        public DateTimeOffset BaseTimeStamp
        {
            get
            {
                return baseTimeStamp;
            }
            set
            {
                baseTimeStamp = value;
                double baseMillis = baseTimeStamp.DateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    TimeStamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                }
            }
        }

        public double[] TimeStamps { get; private set; }
        public Dictionary<Guid, float[]> ChannelData { get; private set; }

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, float[]>();
            TimeStamps = new double[Constants.MUSE_SAMPLE_COUNT];
        }
    }

    public class Muse : ObservableObject, IDisposable
    {
        private static readonly Object syncLock = new object();
        public LSL.liblsl.StreamInfo LSLStreamInfo { get; private set; }
        private LSL.liblsl.StreamInlet lslStreamIn;
        private LSL.liblsl.StreamOutlet lslStream;

        public BluetoothLEDevice Device { get; set; }
        private GattDeviceService deviceService { get; set; }
        private Dictionary<UInt16, MuseSample> sampleBuffer { get; set; }

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
                    if (value == MuseConnectionStatus.Offline && isStreaming == true)
                    {
                        IsStreaming = false;
                    }
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

        public Muse(BluetoothLEDevice device, string name, string id, MuseConnectionStatus status)
        {
            Device = device;
            Name = name;
            Id = id;
            Status = status;
            LSLStreamInfo = new LSL.liblsl.StreamInfo(string.Format("{0} ({1})", name, MacAddress), "EEG", Constants.MUSE_CHANNEL_COUNT, Constants.MUSE_SAMPLE_RATE, LSL.liblsl.channel_format_t.cf_float32, Package.Current.DisplayName);
            LSLStreamInfo.desc().append_child_value("manufacturer", "Muse");
            LSLStreamInfo.desc().append_child_value("manufacturer", "Muse");
            LSLStreamInfo.desc().append_child_value("type", "EEG");
            var channels = LSLStreamInfo.desc().append_child("channels");
            foreach (var c in Constants.MUSE_CHANNEL_LABELS)
            {
                channels.append_child("channel")
                .append_child_value("label", c)
                .append_child_value("unit", "microvolts")
                .append_child_value("type", "EEG");
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
                Device.Dispose();
                LSLStreamInfo.Dispose();
                lslStream.Dispose();
                lslStreamIn.Dispose();
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
                    lslStream = new LSL.liblsl.StreamOutlet(LSLStreamInfo, Constants.MUSE_SAMPLE_COUNT, Constants.MUSE_LSL_BUFFER_LENGTH);
                    // Get GATT service on start, therefore it will be already available when stopping.
                    deviceService = (await Device.GetGattServicesForUuidAsync(Constants.MUSE_DATA_SERVICE_UUID)).Services.First();
                }

                var characteristics = (await deviceService.GetCharacteristicsAsync()).Characteristics;
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

                for (int i = 0; i < channels.Count; i++)
                {
                    await channels[i].WriteClientCharacteristicConfigurationDescriptorAsync(notify);
                    if (start)
                    {
                        channels[i].ValueChanged += Channel_ValueChanged;
                        sampleBuffer = new Dictionary<ushort, MuseSample>();
                    }
                    else channels[i].ValueChanged -= Channel_ValueChanged;
                }

                // Tell Muse to start or stop notifications.
                await characteristics.Single(x => x.Uuid == Constants.MUSE_TOGGLE_STREAM_UUID).WriteValueWithResultAsync(buffer);
            }
            catch (InvalidOperationException) { deviceService.Dispose(); return; }

            if (start)
            {
                ActivateStream();
            }

            else
            {
                DeactivateStream();
                deviceService.Dispose(); // Don't have to keep service reference around anymore. The handlers for the channels will also stop.
            }
            IsStreaming = start;
        }

        private void ActivateStream()
        {
            new Timer((s) => {
                lslStreamIn = new LSL.liblsl.StreamInlet(LSLStreamInfo, Constants.MUSE_LSL_BUFFER_LENGTH, Constants.MUSE_SAMPLE_COUNT);
                lslStreamIn.open_stream();
            }, new AutoResetEvent(false), 65, 0); // Activate inlet in 70ms so we have our first chunk.   
        }

        private void DeactivateStream()
        {
            if (lslStreamIn != null)
            {
                lslStreamIn.close_stream();
                lslStreamIn.Dispose();
            }
            if(lslStream != null)
                lslStream.Dispose();
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

        private void PushSampleLSL(MuseSample sample)
        {
            float[,] data = new float[Constants.MUSE_SAMPLE_COUNT, Constants.MUSE_CHANNEL_COUNT];
            for (int i = 0; i < Constants.MUSE_CHANNEL_COUNT; i++)
            {
                var channelData = sample.ChannelData[Constants.MUSE_CHANNEL_UUIDS[i]]; // Maintains muse-lsl.py ordering.
                for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                {
                    data[j, i] = channelData[j];
                }
            }
            //try
            //{
                lslStream.push_chunk(data, sample.TimeStamps);
            //}
            //catch (UnauthorizedAccessException) { }
        }

        private void Channel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
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
                    if (!sampleBuffer.ContainsKey(museTimestamp))
                    {
                        sample = new MuseSample();
                        sample.BaseTimeStamp = args.Timestamp; // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                        sampleBuffer[museTimestamp] = sample;
                    }
                    else sample = sampleBuffer[museTimestamp];

                    // Get time samples.
                    sample.ChannelData[sender.Uuid] = GetTimeSamples(bitData);

                    // If we have all 5 channels, we can push the 12 samples for each channel.
                    if (sample.ChannelData.Count == Constants.MUSE_CHANNEL_COUNT)
                    {
                        PushSampleLSL(sample);
                        sampleBuffer.Remove(museTimestamp);
                    }
                }
            }
        }
    }
}
