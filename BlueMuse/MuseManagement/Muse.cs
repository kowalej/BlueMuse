using BlueMuse.AppService;
using BlueMuse.Helpers;
using BlueMuse.Misc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace BlueMuse.MuseManagement
{
    public class Muse : ObservableObject, IDisposable
    {
        private static readonly Object syncLock = new object();

        public BluetoothLEDevice Device;

        public static ITimestampFormat TimestampFormat = new BlueMuseUnixTimestampFormat();
        public static ITimestampFormat TimestampFormat2 = new LSLLocalClockNativeTimestampFormat();

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
                Regex deviceIdRegex = new Regex(@"^*-(?<MAC>(\w{2}:){5}\w{2})"); // Get correct MAC which is located after the dash.
                string museId = Id;
                Match matches = deviceIdRegex.Match(museId);
                if (matches.Success)
                    museId = matches.Groups["MAC"].Value;
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

            // Device.Dispose causing uncatchable exceptions.
            //if (disposing)
            //{
            //    try
            //    {
            //        if (Device != null)
            //            Device.Dispose();
            //    }
            //    catch (Exception) { }
            //}

            // Free any unmanaged objects here.
            //

            disposed = true;
        }

        public async Task ToggleStream(bool start)
        {
            if (start == isStreaming) return;
            try
            {
                if (start)
                {
                    if (channels == null) channels = new List<GattCharacteristic>();
                    // Get GATT service on start, therefore it will be already available when stopping.
                    deviceService = (await Device.GetGattServicesForUuidAsync(Constants.MUSE_DATA_SERVICE_UUID)).Services.First();
                }
                var characteristics = (await deviceService.GetCharacteristicsAsync()).Characteristics;
                foreach (var c in channelUUIDs)
                {
                    var characteristic = characteristics.SingleOrDefault(x => x.Uuid == c);
                    if (characteristic == null)
                    {
                        Log.Error($"Unexpected null GATT characteristic (UUID={c}) during toggle stream (start={start}).");
                        return;
                    }
                    channels.Add(characteristic);
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
                        channels[i].ValueChanged += Channel_ValueChanged;
                        sampleBuffer = new Dictionary<ushort, MuseSample>();
                    }
                    else channels[i].ValueChanged -= Channel_ValueChanged;
                    await channels[i].WriteClientCharacteristicConfigurationDescriptorAsync(notify);
                }

                // Tell Muse to start or stop notifications.
                await characteristics.Single(x => x.Uuid == Constants.MUSE_TOGGLE_STREAM_UUID).WriteValueWithResultAsync(buffer);
            }
            catch (Exception ex) {
                Log.Error($"Exception during toggle stream (start={start}).", ex);
                if (isStreaming) FinishCloseOffStream();
                return;
            }

            if (start)
                FinishOpenStream();
            else
                FinishCloseOffStream(); // Don't have to keep service reference around anymore. The handlers for the channels will also stop.
        }

        private async void FinishOpenStream()
        {
            await LSLOpenStream();
            IsStreaming = true;
        }

        private async void FinishCloseOffStream()
        {
            channels.Clear();
            await LSLCloseStream();
            deviceService.Dispose();
            IsStreaming = false;
        }

        private async Task LSLOpenStream()
        {
            ValueSet message = new ValueSet
            {
                { Constants.LSL_MESSAGE_DEVICE_NAME, LongName },
                { Constants.LSL_MESSAGE_SEND_SECONDARY_TIMESTAMP, TimestampFormat2.GetType() != typeof(DummyTimestampFormat) }
            };
            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_OPEN_STREAM, message);
        }

        private async Task LSLCloseStream()
        {
            ValueSet message = new ValueSet
            {
                { Constants.LSL_MESSAGE_DEVICE_NAME, LongName }
            };
            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM, message);
        }

        private async Task LSLPushChunk(MuseSample sample)
        {
            ValueSet message = new ValueSet
            {
                { Constants.LSL_MESSAGE_DEVICE_NAME, LongName }
            };
            double[] data = new double[Constants.MUSE_SAMPLE_COUNT * channelCount]; // Can only send 1D array with this garbage :S
            for (int i = 0; i < channelCount; i++)
            {
                var channelData = sample.ChannelData[channelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                {
                    data[(i * Constants.MUSE_SAMPLE_COUNT) + j] = channelData[j];
                }
            }

            message.Add(Constants.LSL_MESSAGE_CHUNK_DATA, data);
            message.Add(Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);            
        }

        private double[] GetTimeSamples(string bits)
        {
            // Extract our 12, 12-bit samples.
            double[] timeSamples = new double[Constants.MUSE_SAMPLE_COUNT];
            for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
            {
                timeSamples[i] = PacketConversion.ToFakeUInt12(bits, 16 + (i * 12)); // Initial offset by 16 bits for the timestamp.
                timeSamples[i] = (timeSamples[i] - 2048d) * 0.48828125d; // 12 bits on a 2 mVpp range.
            }
            return timeSamples;
        }

        private static string GetBits(IBuffer buffer)
        {
            byte[] raw = new byte[buffer.Length];
            CryptographicBuffer.CopyToByteArray(buffer, out raw);
            string hexStr = BitConverter.ToString(raw);
            string[] hexSplit = hexStr.Split('-');
            string bits = string.Empty;
            foreach (var hex in hexSplit)
            {
                UInt16 longValue = Convert.ToUInt16("0x" + hex, 16);
                bits = bits + Convert.ToString(longValue, 2).PadLeft(8, '0');
            }
            return bits;
        }

        private async void Channel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                string bits = GetBits(args.CharacteristicValue);
                UInt16 museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                MuseSample sample;
                lock (sampleBuffer)
                {
                    if (!sampleBuffer.ContainsKey(museTimestamp))
                    {
                        sample = new MuseSample();
                        sampleBuffer.Add(museTimestamp, sample);
                        sample.BaseTimestamp = TimestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                        sample.BasetimeStamp2 = TimestampFormat2.GetNow(); // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                    }
                    else sample = sampleBuffer[museTimestamp];

                    // Get time samples.
                    sample.ChannelData[sender.Uuid] = GetTimeSamples(bits);
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
