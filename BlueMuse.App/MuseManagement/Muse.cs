using BlueMuse.AppService;
using BlueMuse.Helpers;
using BlueMuse.Misc;
using LSLBridge.LSL;
using Newtonsoft.Json;
using Serilog;
using System;
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

        // It is very important that we keep this referenced as a class member.
        // Otherwise our channels will stop having their event handlers called when the characteristics go out of scope (if referenced only inside a function).
        private IReadOnlyList<GattCharacteristic> streamCharacteristics;

        // We have to buffer EEG and PPG, since any given sample is composed of data from multiple Bluetooth channels (Gatt characteristics).
        private volatile Dictionary<ushort, MuseEEGSamples> eegSampleBuffer;
        private volatile Dictionary<ushort, MusePPGSamples> ppgSampleBuffer;

        private MuseModel museModel;
        public MuseModel MuseModel { get { return museModel; } set { SetProperty(ref museModel, value); OnPropertyChanged(nameof(MuseModel)); } }

        private string name;
        public string Name { get { return name; } set { SetProperty(ref name, value); OnPropertyChanged(nameof(LongName)); } }

        private string id;
        public string Id
        {
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
                    if (value == MuseConnectionStatus.Online)
                    {
                        DetermineMuseModel();
                    }
                    SetProperty(ref status, value);
                    OnPropertyChanged(nameof(CanStream));
                    OnPropertyChanged(nameof(CanReset));
                }
            }
        }

        private bool isStreaming;
        public bool IsStreaming { get { return isStreaming; } set { lock (syncLock) { SetProperty(ref isStreaming, value); OnPropertyChanged(nameof(CanReset)); } } }

        private bool isSelected;
        public bool IsSelected { get { return isSelected; } set { lock (syncLock) { SetProperty(ref isSelected, value); OnPropertyChanged(nameof(CanReset)); } } }

        public bool CanReset { get { return isSelected && status == MuseConnectionStatus.Online && !isStreaming; } }

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
            DetermineMuseModel();
        }

        public async void DetermineMuseModel()
        {
            // Already determined model, no work to do.
            if (MuseModel != MuseModel.Undetected) return;

            // Device is Smith Lowdown.
            if (name.Contains("SMXT"))
            {
                MuseModel = MuseModel.Smith;
                eegChannelCount = Constants.MUSE_EEG_NOAUX_CHANNEL_COUNT;
                eegChannelUUIDs = Constants.MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS;
                eegChannelLabels = Constants.MUSE_EEG_NOAUX_CHANNEL_LABELS;
                deviceInfoName = Constants.MUSE_SMXT_DEVICE_NAME;
                deviceInfoManufacturer = Constants.MUSE_SMXT_MANUFACTURER;
            }

            // Device must be a regular Muse or Muse 2, so we set basic properties.
            else
            {
                deviceInfoManufacturer = Constants.MUSE_MANUFACTURER;
            }

            // Cannot determine any further (Muse Original vs Muse 2 until connected).
            if (Status == MuseConnectionStatus.Offline) return;
            try
            {
                var characteristics = await GetGattCharacteristics();
                if (characteristics == null)
                {
                    Log.Error($"Cannot complete determining Muse model due to null GATT characteristics.");
                    return;
                }
                // Device has PPG, therefore we know it's a Muse 2. Note we will also not use AUX channel.
                if (characteristics.FirstOrDefault(x => x.Uuid == Constants.MUSE_GATT_PPG_CHANNEL_UUIDS[0]) != null)
                {
                    MuseModel = MuseModel.Muse2;
                    eegChannelCount = Constants.MUSE_EEG_NOAUX_CHANNEL_COUNT;
                    eegChannelUUIDs = Constants.MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_NOAUX_CHANNEL_LABELS;
                    deviceInfoName = Constants.MUSE_2_DEVICE_NAME;
                }
                else
                {
                    MuseModel = MuseModel.Original;
                    eegChannelCount = Constants.MUSE_EEG_CHANNEL_COUNT;
                    eegChannelUUIDs = Constants.MUSE_GATT_EGG_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_CHANNEL_LABELS;
                    deviceInfoName = Constants.MUSE_DEVICE_NAME;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during determining Muse model. Exception message: {ex.Message}.", ex);
            }
        }

        public async Task ToggleStream(bool start)
        {
            if (MuseModel == MuseModel.Undetected) DetermineMuseModel();
            if (start == isStreaming || (start && !CanStream)) return;
            try
            {
                if (start)
                {
                    // Create fresh sample buffers.
                    eegSampleBuffer = new Dictionary<ushort, MuseEEGSamples>();
                    ppgSampleBuffer = new Dictionary<ushort, MusePPGSamples>();

                    // Pull properties for stream from static global settings.
                    timestampFormat = TimestampFormat;
                    timestampFormat2 = TimestampFormat2;
                    channelDataType = ChannelDataType;
                    isEEGEnabled = IsEEGEnabled;
                    isAccelerometerEnabled = IsAccelerometerEnabled;
                    isGyroscopeEnabled = IsGyroscopeEnabled;
                    isPPGEnabled = IsPPGEnabled && MuseModel == MuseModel.Muse2; // Only Muse 2 supports PPG.

                    if (!isEEGEnabled &&
                        !isAccelerometerEnabled &&
                        !isGyroscopeEnabled &&
                        !isPPGEnabled) return; // Nothing enabled, can't start under this condition.

                }

                streamCharacteristics = await GetGattCharacteristics();
                if (streamCharacteristics == null)
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to null GATT characteristics.");
                    return;
                }

                // Subscribe or unsubscribe EEG.
                if (isEEGEnabled)
                {
                    if (!await ToggleCharacteristics(eegChannelUUIDs, streamCharacteristics, start, EEGChannel_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for EEG.");
                        return;
                    }
                }

                // Subscribe or unsubscribe accelerometer.
                if (isAccelerometerEnabled)
                {
                    //await ToggleCharacteristic(Constants.MUSE_GATT_ACCELEROMETER_UUID, characteristics, start, AccelerometerChannel_ValueChanged);
                    //{
                    //Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for accelerometer.");
                    //return;
                    //}
                }

                // Subscribe or unsubscribe gyroscope.
                if (isGyroscopeEnabled)
                {
                    //await ToggleCharacteristic(Constants.MUSE_GATT_GYROSCOPE_UUID, characteristics, start, GyroscopeChannel_ValueChanged);
                    //{
                    //Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for accelerometer.");
                    //return;
                    //}
                }

                // Subscribe or unsubscribe PPG.
                if (isPPGEnabled)
                {
                    //await ToggleCharacteristic(Constants.MUSE_GATT_PPG_CHANNEL_UUIDS, characteristics, start, PPGChannel_ValueChanged);
                    //{
                    //Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for accelerometer.");
                    //return;
                    //}
                }

                // Determine if we are listening to Gatt channels or stopping (notify vs none) and what command to send to the Muse (start or stop data).
                byte[] toggleCommand = start ? Constants.MUSE_CMD_TOGGLE_STREAM_START : Constants.MUSE_CMD_TOGGLE_STREAM_STOP;

                // Tell Muse to start or stop notifications.
                bool success = await WriteCommand(toggleCommand, characteristics);
                if (!success)
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to failure to run toggle command.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during toggle stream (start={start}). Exception message: {ex.Message}.", ex);
                if (isStreaming) FinishCloseOffStream();
                return;
            }

            if (start)
                FinishOpenStream();
            else
                FinishCloseOffStream(); // Don't have to keep service reference around anymore. The handlers for the channels will also stop.
        }

        public async Task Reset()
        {
            var characteristics = await GetGattCharacteristics();
            if (characteristics == null)
            {
                Log.Error($"Cannot reset device due to null GATT characteristics.");
                return;
            }
            await WriteCommand(Constants.MUSE_CMD_ASK_RESET, characteristics);
        }

        private async Task<bool> WriteCommand(byte[] command, IReadOnlyList<GattCharacteristic> characteristics)
        {
            var commandCharacteristic = characteristics.FirstOrDefault(x => x.Uuid == Constants.MUSE_GATT_COMMAND_UUID);
            if (commandCharacteristic == null)
            {
                Log.Error($"Cannot perform command {command} due to null GATT characteristic (UUID={Constants.MUSE_GATT_COMMAND_UUID}.");
                return false;
            }

            var writeResult = await commandCharacteristic.WriteValueWithResultAsync(WindowsRuntimeBuffer.Create(command, 0, command.Length, command.Length));
            if (writeResult.Status != GattCommunicationStatus.Success)
            {
                Log.Error($"Cannot perform command {command} due to unexpected communication error with GATT characteristic (UUID={Constants.MUSE_GATT_COMMAND_UUID}). Status: {writeResult.Status}. Protocol Error {writeResult.ProtocolError}.");
                return false;
            }
            return true;
        }

        private async Task<IReadOnlyList<GattCharacteristic>> GetGattCharacteristics(string deviceServiceUuid = null)
        {
            if (deviceServiceUuid == null) deviceServiceUuid = Constants.MUSE_GATT_DATA_SERVICE_UUID.ToString();

            // Get GATT device service.
            var deviceService = await Device.GetGattServicesForUuidAsync(new Guid(deviceServiceUuid));
            if (deviceService == null)
            {
                Log.Error($"Cannot get GATT characteristics due to unexpected null GATT device service (UUID={deviceServiceUuid}).");
                return null;
            }
            else if (deviceService.Status != GattCommunicationStatus.Success)
            {
                Log.Error($"Cannot get GATT characteristics due to unexpected communication error with GATT device service (UUID={deviceServiceUuid}). Status: {deviceService.Status}. Protocol Error {deviceService.ProtocolError}.");
                return null;
            }
            else if (deviceService.Services.FirstOrDefault() == null)
            {
                Log.Error($"Cannot get GATT characteristics due to unexpected null GATT device service list of services (UUID={deviceServiceUuid}).");
                return null;
            }

            var characteristics = await deviceService.Services.First().GetCharacteristicsAsync();
            if (characteristics == null)
            {
                Log.Error($"Cannot get GATT characteristics due to unexpected null characteristics.");
                return null;
            }
            else if (characteristics.Status != GattCommunicationStatus.Success)
            {
                Log.Error($"Cannot get GATT characteristics due to unexpected communication error with GATT device service (UUID={deviceServiceUuid}). Status: {characteristics.Status}. Protocol Error {characteristics.ProtocolError}.");
                return null;
            }
            else return characteristics.Characteristics;
        }

        private async Task<bool> ToggleCharacteristics(Guid[] characteristicTargets, IReadOnlyList<GattCharacteristic> characteristics, bool start, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> eventHandler)
        {
            foreach (var c in characteristicTargets)
            {
                var characteristic = characteristics.SingleOrDefault(x => x.Uuid == c);
                if (characteristic == null)
                {
                    Log.Error($"Cannot toggle characteristics (start={start}) due to unexpected null GATT characteristic (UUID={c}).");
                    return false;
                }

                GattClientCharacteristicConfigurationDescriptorValue notifyToggle;
                if (start)
                {
                    notifyToggle = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                    characteristic.ValueChanged += eventHandler;
                }
                else
                {
                    characteristic.ValueChanged -= eventHandler;
                    notifyToggle = GattClientCharacteristicConfigurationDescriptorValue.None;
                }
                var writeResult = await characteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(notifyToggle);
                if (writeResult.Status != GattCommunicationStatus.Success)
                {
                    Log.Error($"Cannot perform command {notifyToggle} due to unexpected communication error with GATT characteristic (UUID={c}). Status: {writeResult.Status}. Protocol Error {writeResult.ProtocolError}.");
                    return false;
                }
            }
            return true;
        }

        // Handles LSL stream opening.
        private async void FinishOpenStream()
        {
            await LSLOpenStreams();
            IsStreaming = true;
        }

        // Handles LSL stream closing.
        private async void FinishCloseOffStream()
        {
            eegSampleBuffer.Clear();
            await LSLCloseStream();
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
                await LSLOpenPPG();
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

        private async Task LSLOpenPPG()
        {
            var channelsInfo = new List<LSLBridgeChannelInfo>();
            foreach (var c in Constants.MUSE_PPG_CHANNEL_LABELS)
            {
                channelsInfo.Add(new LSLBridgeChannelInfo
                {
                    Label = c,
                    Type = Constants.PPG_STREAM_TYPE,
                    Unit = Constants.PPG_UNITS
                });
            }

            LSLBridgeStreamInfo streamInfo = new LSLBridgeStreamInfo()
            {
                BufferLength = Constants.MUSE_LSL_BUFFER_LENGTH,
                Channels = channelsInfo,
                ChannelCount = eegChannelCount,
                ChannelDataType = channelDataType.DataType,
                ChunkSize = Constants.MUSE_PPG_SAMPLE_COUNT,
                DeviceManufacturer = deviceInfoManufacturer,
                DeviceName = deviceInfoName,
                NominalSRate = Constants.MUSE_PPG_SAMPLE_RATE,
                StreamType = Constants.PPG_STREAM_TYPE,
                SendSecondaryTimestamp = timestampFormat2.GetType() != typeof(DummyTimestampFormat),
                StreamName = PPGStreamName
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

        private static string GetBits(IBuffer buffer)
        {
            byte[] vals = new byte[buffer.Length];
            for (uint i = 0; i < buffer.Length; i++)
            {
                vals[i] = buffer.GetByte(i);
            }
            string hexStr = BitConverter.ToString(vals);
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
            double[] DecodeEEGSamples(string bits)
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

            if (isStreaming)
            {
                try
                {
                    string bits = GetBits(args.CharacteristicValue);
                    ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MuseEEGSamples samples;
                    lock (eegSampleBuffer)
                    {
                        if (!eegSampleBuffer.ContainsKey(museTimestamp))
                        {
                            samples = new MuseEEGSamples();
                            eegSampleBuffer.Add(museTimestamp, samples);
                            samples.BaseTimestamp = timestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                            samples.BasetimeStamp2 = timestampFormat.GetType() != timestampFormat2.GetType() ?
                                  timestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
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
                        lock (eegSampleBuffer)
                            eegSampleBuffer.Remove(museTimestamp);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during handling EEG channel values.", ex);
                }
            }
        }
    }
}
