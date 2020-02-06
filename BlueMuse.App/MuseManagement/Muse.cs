using BlueMuse.AppService;
using BlueMuse.Helpers;
using BlueMuse.Misc;
using LSLBridge.LSL;
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
using Newtonsoft.Json;

namespace BlueMuse.MuseManagement
{
    public class Muse : ObservableObject
    {
        private static readonly object syncLock = new object();

        public BluetoothLEDevice Device;

        public static ITimestampFormat TimestampFormat = new BlueMuseUnixTimestampFormat();
        public static ITimestampFormat TimestampFormat2 = new LSLLocalClockBlueMuseTimestampFormat();
        public static ChannelDataType ChannelDataType = ChannelDataTypesContainer.ChannelDataTypes.FirstOrDefault();
        public static bool IsEEGEnabled = true;
        public static bool IsAccelerometerEnabled = true;
        public static bool IsGyroscopeEnabled = true;
        public static bool IsPPGEnabled = true;

        // Settings for a stream - read from static variables and fixed at stream start.
        private ITimestampFormat timestampFormat = TimestampFormat;
        private ITimestampFormat timestampFormat2 = TimestampFormat2;
        private ChannelDataType channelDataType = ChannelDataType;
        private bool isEEGEnabled = true;
        private bool isAccelerometerEnabled = true;
        private bool isGyroscopeEnabled = true;
        private bool isPPGEnabled = true;

        private GattDeviceService deviceService;
        private List<GattCharacteristic> inUseGattChannels;

        private volatile Dictionary<UInt16, MuseEEGSamples> eegSampleBuffer;

        private MuseModel museModel;
        public MuseModel MuseModel { get { return museModel; } set { SetProperty(ref museModel, value); OnPropertyChanged(nameof(MuseModel)); } }

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

        private string deviceInfoManufacturer;
        private string deviceInfoName;

        private int eegChannelCount;
        private Guid[] eegChannelUUIDs;
        private string[] eegChannelLabels;

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

        // Stream names.
        public string EEGStreamName { get { return $"{LongName} {Constants.EEG_STREAM_TYPE}"; } }
        public string AccelerometerStreamName { get { return $"{LongName} {Constants.ACCELEROMETER_STREAM_TYPE}"; } }
        public string GyroscopeStreamName { get { return $"{LongName} {Constants.GYROSCOPE_STREAM_TYPE}"; } }
        public string PPGStreamName { get { return $"{LongName} {Constants.PPG_STREAM_TYPE}"; } }

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

        public Muse(BluetoothLEDevice device, string name, string id, MuseConnectionStatus status)
        {
            Device = device;
            Name = name;
            Id = id;
            Status = status;

            // Is Smith Lowdown?
            if (name.Contains(Constants.DeviceNameFilter[1]))
            {
                eegChannelCount = Constants.MUSE_SMXT_EEG_CHANNEL_COUNT;
                eegChannelUUIDs = Constants.MUSE_SMXT_GATT_EEG_CHANNEL_UUIDS;
                eegChannelLabels = Constants.MUSE_SMXT_EEG_CHANNEL_LABELS;
                deviceInfoName = Constants.MUSE_SMXT_DEVICE_NAME;
                deviceInfoManufacturer = Constants.MUSE_SMXT_MANUFACTURER;
            }
            //// Muse 2.
            //else if(AssumeMuse2)
            //{
            //    channelCount = Constants.MUSE_2_CHANNEL_COUNT;
            //    channelUUIDs = Constants.MUSE_2_EEG_CHANNEL_UUIDS;
            //    channelLabels = Constants.MUSE_2_EEG_CHANNEL_LABELS;
            //    deviceInfoName = Constants.MUSE_2_DEVICE_NAME;
            //    deviceInfoManufacturer = Constants.MUSE_2_MANUFACTURER;
            //}
            // Default to Muse.
            else
            {
                eegChannelCount = Constants.MUSE_EEG_CHANNEL_COUNT;
                eegChannelUUIDs = Constants.MUSE_GATT_EGG_CHANNEL_UUIDS;
                eegChannelLabels = Constants.MUSE_EEG_CHANNEL_LABELS;
                deviceInfoName = Constants.MUSE_DEVICE_NAME;
                deviceInfoManufacturer = Constants.MUSE_MANUFACTURER;
            }
        }

        public async Task ToggleStream(bool start)
        {
            if (start == isStreaming || (start && !CanStream)) return;
            try
            {
                if (start)
                {
                    // Pull properties for stream from static global settings.
                    timestampFormat = TimestampFormat;
                    timestampFormat2 = TimestampFormat2;
                    channelDataType = ChannelDataType;
                    isEEGEnabled = IsEEGEnabled;
                    isAccelerometerEnabled = IsAccelerometerEnabled;
                    isGyroscopeEnabled = IsGyroscopeEnabled;
                    isPPGEnabled = IsPPGEnabled;

                    if (!isEEGEnabled &&
                        !isAccelerometerEnabled &&
                        !isGyroscopeEnabled &&
                        !isPPGEnabled) return; // Nothing enabled, can't start under this condition.

                    if (inUseGattChannels == null) inUseGattChannels = new List<GattCharacteristic>();

                    // Get GATT service on start, therefore it will be already available when stopping.
                    deviceService = (await Device.GetGattServicesForUuidAsync(Constants.MUSE_GATT_DATA_SERVICE_UUID)).Services.First();
                }

                // Determine if we are listening to Gatt channels or stopping (notify vs none) and what command to send to the Muse (start or stop data).
                GattClientCharacteristicConfigurationDescriptorValue notify;
                byte[] toggleData;
                if (start)
                {
                    notify = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                    toggleData = Constants.MUSE_CMD_TOGGLE_STREAM_START;
                }
                else
                {
                    notify = GattClientCharacteristicConfigurationDescriptorValue.None;
                    toggleData = Constants.MUSE_CMD_TOGGLE_STREAM_STOP;
                }
                var buffer = WindowsRuntimeBuffer.Create(toggleData, 0, toggleData.Length, toggleData.Length);

                // Get our corresponding Gatt characteristics for the data we are targeting.
                var characteristics = (await deviceService.GetCharacteristicsAsync()).Characteristics;
                foreach (var c in eegChannelUUIDs)
                {
                    var characteristic = characteristics.SingleOrDefault(x => x.Uuid == c);
                    if (characteristic == null)
                    {
                        Log.Error($"Unexpected null GATT characteristic (UUID={c}) during toggle stream (start={start}).");
                        return;
                    }
                    inUseGattChannels.Add(characteristic);
                }

                for (int i = 0; i < inUseGattChannels.Count; i++)
                {
                    if (start)
                    {
                        inUseGattChannels[i].ValueChanged += EEGChannel_ValueChanged;
                    }
                    else inUseGattChannels[i].ValueChanged -= EEGChannel_ValueChanged;
                    await inUseGattChannels[i].WriteClientCharacteristicConfigurationDescriptorAsync(notify);
                }

                // Tell Muse to start or stop notifications.
                await characteristics.Single(x => x.Uuid == Constants.MUSE_GATT_COMMAND_UUID).WriteValueWithResultAsync(buffer);
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

        // Handles LSL stream opening.
        private async void FinishOpenStream()
        {
            await LSLOpenStreams();
            IsStreaming = true;
        }

        // Handles LSL stream closing, and Bluetooth cleanup.
        private async void FinishCloseOffStream()
        {
            inUseGattChannels.Clear();
            await LSLCloseStream();
            deviceService.Dispose();
            IsStreaming = false;
        }

        private async Task LSLOpenStreams()
        {
            if (isEEGEnabled)
            {
                await LSLOpenEEG();
            }
            if (isAccelerometerEnabled)
            {
                await LSLOpenAccelerometer();
            }
            if (isGyroscopeEnabled)
            {
                await LSLOpenGyroscope();
            }
            if (isPPGEnabled)
            {
                //await LSLOpenPPG();
            }
        }

        private async Task LSLOpenEEG()
        {
            var channelsInfo = new List<LSLBridgeChannelInfo>();
            foreach (var c in eegChannelLabels)
            {
                channelsInfo.Add(new LSLBridgeChannelInfo
                {
                    Label = c,
                    Type = Constants.EEG_STREAM_TYPE,
                    Unit = Constants.EEG_UNITS
                });
            }

            LSLBridgeStreamInfo streamInfo = new LSLBridgeStreamInfo()
            {
                BufferLength = Constants.MUSE_LSL_BUFFER_LENGTH,
                Channels = channelsInfo,
                ChannelCount = eegChannelCount,
                ChannelDataType = channelDataType.DataType,
                ChunkSize = Constants.MUSE_EEG_SAMPLE_COUNT,
                DeviceManufacturer = deviceInfoManufacturer,
                DeviceName = deviceInfoName,
                NominalSRate = Constants.MUSE_EEG_SAMPLE_RATE,
                StreamType = Constants.EEG_STREAM_TYPE,
                SendSecondaryTimestamp = timestampFormat2.GetType() != typeof(DummyTimestampFormat),
                StreamName = EEGStreamName
            };

            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_INFO, JsonConvert.SerializeObject(streamInfo) }
            };
            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_OPEN_STREAM, message);
        }

        private async Task LSLOpenAccelerometer()
        {
            var channelsInfo = new List<LSLBridgeChannelInfo>();
            foreach (var c in Constants.MUSE_ACCELEROMETER_CHANNEL_LABELS)
            {
                channelsInfo.Add(new LSLBridgeChannelInfo
                {
                    Label = c,
                    Type = Constants.ACCELEROMETER_STREAM_TYPE,
                    Unit = Constants.ACCELEROMETER_UNITS
                });
            }

            LSLBridgeStreamInfo streamInfo = new LSLBridgeStreamInfo()
            {
                BufferLength = Constants.MUSE_LSL_BUFFER_LENGTH,
                Channels = channelsInfo,
                ChannelCount = Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT,
                ChannelDataType = channelDataType.DataType,
                ChunkSize = Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT,
                DeviceManufacturer = deviceInfoManufacturer,
                DeviceName = deviceInfoName,
                NominalSRate = Constants.MUSE_ACCELEROMETER_SAMPLE_RATE,
                StreamType = Constants.ACCELEROMETER_STREAM_TYPE,
                SendSecondaryTimestamp = timestampFormat2.GetType() != typeof(DummyTimestampFormat),
                StreamName = AccelerometerStreamName
            };

            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_INFO, JsonConvert.SerializeObject(streamInfo) }
            };
            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_OPEN_STREAM, message);
        }

        private async Task LSLOpenGyroscope()
        {
            var channelsInfo = new List<LSLBridgeChannelInfo>();
            foreach (var c in Constants.MUSE_GYROSCOPE_CHANNEL_LABELS)
            {
                channelsInfo.Add(new LSLBridgeChannelInfo
                {
                    Label = c,
                    Type = Constants.GYROSCOPE_STREAM_TYPE,
                    Unit = Constants.GYROSCOPE_UNITS
                });
            }

            LSLBridgeStreamInfo streamInfo = new LSLBridgeStreamInfo()
            {
                BufferLength = Constants.MUSE_LSL_BUFFER_LENGTH,
                Channels = channelsInfo,
                ChannelCount = Constants.MUSE_GYROSCOPE_CHANNEL_COUNT,
                ChannelDataType = channelDataType.DataType,
                ChunkSize = Constants.MUSE_GYROSCOPE_SAMPLE_COUNT,
                DeviceManufacturer = deviceInfoManufacturer,
                DeviceName = deviceInfoName,
                NominalSRate = Constants.MUSE_GYROSCOPE_SAMPLE_RATE,
                StreamType = Constants.GYROSCOPE_STREAM_TYPE,
                SendSecondaryTimestamp = timestampFormat2.GetType() != typeof(DummyTimestampFormat),
                StreamName = GyroscopeStreamName
            };

            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_INFO, JsonConvert.SerializeObject(streamInfo) }
            };
            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_OPEN_STREAM, message);
        }

        private async Task LSLCloseStream()
        {
            // It is safe to just iterate the possible stream names and request that the bridge closes them.
            foreach (var streamName in
                 new string[] { EEGStreamName, AccelerometerStreamName, GyroscopeStreamName, PPGStreamName })
            {
                ValueSet message = new ValueSet
                {
                    { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, streamName }
                };
                await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM, message);
            }
        }

        private async Task LSLPushEEGChunk(MuseEEGSamples sample)
        {
            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, EEGStreamName }
            };

            // Can only send 1D array with garbage AppService :S - inlined as channel1sample1,channel1sample2,channel1sample3...channel2sample1,channel2sample2...
            if (channelDataType.DataType == LSLBridgeDataType.DOUBLE)
            {
                double[] data = new double[Constants.MUSE_EEG_SAMPLE_COUNT * eegChannelCount];
                for (int i = 0; i < eegChannelCount; i++)
                {
                    var channelData = sample.ChannelData[eegChannelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_EEG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_EEG_SAMPLE_COUNT) + j] = channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            // Default to float.
            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_EEG_SAMPLE_COUNT * eegChannelCount];
                for (int i = 0; i < eegChannelCount; i++)
                {
                    var channelData = sample.ChannelData[eegChannelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_EEG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_EEG_SAMPLE_COUNT) + j] = (float)channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL chunk - unsupported stream data type. Must use float32 or double64.");

            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);            
        }

        private double[] DecodeEEGSamples(string bits)
        {
            // Extract our 12, 12-bit samples.
            double[] samples = new double[Constants.MUSE_EEG_SAMPLE_COUNT];
            for (int i = 0; i < Constants.MUSE_EEG_SAMPLE_COUNT; i++)
            {
                samples[i] = PacketConversion.ToFakeUInt12(bits, 16 + (i * 12)); // Initial offset by 16 bits for the timestamp.
                samples[i] = (samples[i] - 2048d) * 0.48828125d; // 12 bits on a 2 mVpp range.
            }
            return samples;
        }

        private static string GetBits(IBuffer buffer)
        {
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] raw);
            string hexStr = BitConverter.ToString(raw);
            string[] hexSplit = hexStr.Split('-');
            string bits = string.Empty;
            foreach (var hex in hexSplit)
            {
                ushort longValue = Convert.ToUInt16("0x" + hex, 16);
                bits = bits + Convert.ToString(longValue, 2).PadLeft(8, '0');
            }
            return bits;
        }

        private async void EEGChannel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                if (sender.AttributeHandle == 43)
                {
                    string fish = "";
                }
                string bits = GetBits(args.CharacteristicValue);
                ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                MuseEEGSamples samples;
                lock (eegSampleBuffer)
                {
                    if (!eegSampleBuffer.ContainsKey(museTimestamp))
                    {
                        samples = new MuseEEGSamples();
                        eegSampleBuffer.Add(museTimestamp, samples);
                        samples.BaseTimestamp = TimestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                        samples.BasetimeStamp2 = TimestampFormat.GetType() != TimestampFormat2.GetType() ?
                              TimestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                            : samples.BasetimeStamp2 = samples.BaseTimestamp; // Ensures they are equal if using same timestamp format.
                    }
                    else samples = eegSampleBuffer[museTimestamp];

                    // Get time samples.
                    samples.ChannelData[sender.Uuid] = DecodeEEGSamples(bits);
                }
                // If we have all EEG channels, we can push the 12 samples for each channel.
                if (samples.ChannelData.Count == eegChannelCount)
                {
                    await LSLPushEEGChunk(samples);
                    lock(eegSampleBuffer)
                        eegSampleBuffer.Remove(museTimestamp);
                }
            }
        }
    }
}
