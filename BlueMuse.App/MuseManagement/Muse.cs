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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
        public static bool IsTelemetryEnabled = false;

        // Settings for a stream - read from static variables and fixed at stream start.
        private ITimestampFormat timestampFormat = TimestampFormat;
        private ITimestampFormat timestampFormat2 = TimestampFormat2;
        private ChannelDataType channelDataType = ChannelDataType;
        private bool isEEGEnabled = true;
        private bool isAccelerometerEnabled = true;
        private bool isGyroscopeEnabled = true;
        private bool isPPGEnabled = true;
        private bool isTelemetryEnabled = false;

        // It is very important that we keep this referenced as a class member.
        // Otherwise our channels will stop having their event handlers called when the characteristics go out of scope (if referenced only inside a function).
        private IReadOnlyList<GattCharacteristic> streamCharacteristics;

        // We have to buffer EEG and PPG, since any given sample is composed of data from multiple Bluetooth channels (Gatt characteristics).
        private volatile Dictionary<ushort, MuseEEGSamples> eegSampleBuffer;
        private volatile Dictionary<ushort, MusePPGSamples> ppgSampleBuffer;
        private volatile string deviceInfoBuffer = string.Empty;
        private volatile string controlStatusBuffer = string.Empty;

        private MuseModel museModel;
        public MuseModel MuseModel { get { return museModel; } set { lock (syncLock) { SetProperty(ref museModel, value); OnPropertyChanged(nameof(MuseModel)); } } }

        private string name;
        public string Name { get { return name; } set { lock (syncLock) { SetProperty(ref name, value); OnPropertyChanged(nameof(LongName)); } } }

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

        private string lslDeviceInfoManufacturer;
        private string lslDeviceInfoName;

        private int eegChannelCount;
        private Guid[] eegGattChannelUUIDs;
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
                    OnPropertyChanged(nameof(CanRefreshInfo));
                }
            }
        }

        private bool isStreaming;
        public bool IsStreaming { get { return isStreaming; } set { lock (syncLock) { SetProperty(ref isStreaming, value); OnPropertyChanged(nameof(CanReset)); } } }

        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set { 
                lock (syncLock) { 
                    SetProperty(ref isSelected, value); 
                    OnPropertyChanged(nameof(CanReset));
                    OnPropertyChanged(nameof(CanRefreshInfo));
                } 
            }
        }

        public bool CanReset { get { return status == MuseConnectionStatus.Online && !isStreaming; } }

        public bool CanRefreshInfo { get { return status == MuseConnectionStatus.Online && !isStreaming; } }

        public bool CanStream { get { return status == MuseConnectionStatus.Online; } }
        public string LongName { get { return string.Format("{0} ({1})", Name, MacAddress); } }

        // Stream names.
        public string EEGStreamName { get { return $"{LongName} {Constants.EEG_STREAM_TYPE}"; } }
        public string AccelerometerStreamName { get { return $"{LongName} {Constants.ACCELEROMETER_STREAM_TYPE}"; } }
        public string GyroscopeStreamName { get { return $"{LongName} {Constants.GYROSCOPE_STREAM_TYPE}"; } }
        public string PPGStreamName { get { return $"{LongName} {Constants.PPG_STREAM_TYPE}"; } }
        public string TelemetryStreamName { get { return $"{LongName} {Constants.TELEMETRY_STREAM_TYPE}"; } }

        private int batteryLevel = -1;
        public int BatteryLevel { get { return batteryLevel; } set { lock (syncLock) { SetProperty(ref batteryLevel, value); OnPropertyChanged(nameof(BatteryLevel)); OnPropertyChanged(nameof(BatteryLevelOpacity)); } } }
        public double BatteryLevelOpacity { get { return batteryLevel > -1 ? 1.0d : 0.20d; } }

        private volatile string deviceInfoLive = string.Empty;
        private string deviceInfo;
        public string DeviceInfo { get { return deviceInfo; } set { lock (syncLock) { SetProperty(ref deviceInfo, value); OnPropertyChanged(nameof(DeviceInfo)); } } }

        private volatile string controlStatusLive = string.Empty;
        private string controlStatus;
        public string ControlStatus { get { return controlStatus; } set { lock (syncLock) { SetProperty(ref controlStatus, value); OnPropertyChanged(nameof(ControlStatus)); } } }

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

        Timer deviceInfoTimer;

        public Muse(BluetoothLEDevice device, string name, string id, MuseConnectionStatus status)
        {
            Device = device;
            Name = name;
            Id = id;
            Status = status;
            DetermineMuseModel();
            deviceInfoTimer = new Timer(RefreshDeviceInfoAndControlStatus, null, 0, Constants.MUSE_DEVICE_INFO_CONTROL_REFRESH_MS);
        }

        public async void DetermineMuseModel()
        {
            // Already determined model, no work to do.
            if (MuseModel != MuseModel.Undetected) return;

            // Device is Smith Lowdown, we can determine this by name along, therefore the device doesn't need to be actively connected.
            if (name.Contains("SMXT"))
            {
                MuseModel = MuseModel.Smith;
                eegChannelCount = Constants.MUSE_EEG_NOAUX_CHANNEL_COUNT;
                eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS;
                eegChannelLabels = Constants.MUSE_EEG_NOAUX_CHANNEL_LABELS;
                lslDeviceInfoName = Constants.MUSE_SMXT_DEVICE_NAME;
                lslDeviceInfoManufacturer = Constants.MUSE_SMXT_MANUFACTURER;
            }

            // Device must be a regular Muse or Muse 2, so we set basic properties.
            else
            {
                lslDeviceInfoManufacturer = Constants.MUSE_MANUFACTURER;
            }

            // Cannot determine any further (Muse Original vs Muse 2 until connected).
            if (Status == MuseConnectionStatus.Offline) return;
            try
            {
                streamCharacteristics = await GetGattCharacteristics();
                if (streamCharacteristics == null)
                {
                    Log.Error($"Cannot complete determining Muse model due to null GATT characteristics.");
                    return;
                }
                // Device has PPG, therefore we know it's a Muse 2. Note we will also not use AUX channel.
                if (streamCharacteristics.FirstOrDefault(x => x.Uuid == Constants.MUSE_GATT_PPG_CHANNEL_UUIDS[0]) != null)
                {
                    MuseModel = MuseModel.Muse2;
                    eegChannelCount = Constants.MUSE_EEG_NOAUX_CHANNEL_COUNT;
                    eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_NOAUX_CHANNEL_LABELS;
                    lslDeviceInfoName = Constants.MUSE_2_DEVICE_NAME;
                }
                else
                {
                    MuseModel = MuseModel.Original;
                    eegChannelCount = Constants.MUSE_EEG_CHANNEL_COUNT;
                    eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_CHANNEL_LABELS;
                    lslDeviceInfoName = Constants.MUSE_DEVICE_NAME;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during determining Muse model. Exception message: {ex.Message}.", ex);
            }
        }

        public async Task ToggleStream(bool start)
        {
            lock (syncLock)
            {
                if (MuseModel == MuseModel.Undetected) DetermineMuseModel();
                if (start == isStreaming || (start && !CanStream)) return;
            }
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
                    isTelemetryEnabled = IsTelemetryEnabled;

                    if (!isEEGEnabled &&
                        !isAccelerometerEnabled &&
                        !isGyroscopeEnabled &&
                        !isPPGEnabled &&
                        !isTelemetryEnabled) return; // Nothing enabled, can't start under this condition.

                    // Should only need to acquire stream characteristics during start, they will then be reused upon stopping the stream.
                    streamCharacteristics = streamCharacteristics ?? await GetGattCharacteristics();

                    deviceInfoTimer.Change(Timeout.Infinite, Timeout.Infinite); // "Pause" the timer.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, streamCharacteristics, false, DeviceInfo_ValueChanged))
                    {
                        Log.Error($"Cannot get device info due to failure to toggle characteristics for info.");
                    }
                }

                if (streamCharacteristics == null)
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to null GATT characteristics.");
                    if (start) return;
                    else FinishCloseOffStream();
                }

                // Subscribe or unsubscribe EEG.
                if (isEEGEnabled)
                {
                    if (!await ToggleCharacteristics(eegGattChannelUUIDs, streamCharacteristics, start, EEGChannel_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for EEG.");
                        if (start) return;
                    }
                }

                // Subscribe or unsubscribe accelerometer.
                if (isAccelerometerEnabled)
                {
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_ACCELEROMETER_UUID }, streamCharacteristics, start, Accelerometer_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for accelerometer.");
                        if (start) return;
                    }
                }

                // Subscribe or unsubscribe gyroscope.
                if (isGyroscopeEnabled)
                {
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_GYROSCOPE_UUID }, streamCharacteristics, start, Gyroscope_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for gyroscope.");
                        if (start) return;
                    }
                }

                // Subscribe or unsubscribe PPG.
                if (isPPGEnabled)
                {
                    if (!await ToggleCharacteristics(Constants.MUSE_GATT_PPG_CHANNEL_UUIDS, streamCharacteristics, start, PPGChannel_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for PPG.");
                        if (start) return;
                    }
                }

                // Subscribe or unsubscribe telemetry (battery, adc voltage, temperature).
                // Note that we always subscribe to telemetry from a bluetooth standpoint, so that we can have an updated battery level.
                if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_TELEMETRY_UUID }, streamCharacteristics, start, Telemetry_ValueChanged))
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for telemetry.");
                    if (start) return;
                }

                // Determine if we are listening to Gatt channels or stopping (notify vs none) and what command to send to the Muse (start or stop data).
                byte[] toggleCommand = start ? Constants.MUSE_CMD_TOGGLE_STREAM_START : Constants.MUSE_CMD_TOGGLE_STREAM_STOP;

                // Tell Muse to start or stop notifications.
                if (!await WriteCommand(toggleCommand, streamCharacteristics))
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to failure to run toggle command.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during toggle stream (start={start}). Exception message: {ex.Message}.", ex);
                if (!start) FinishCloseOffStream();
            }

            if (start)
                FinishOpenStream();
            else
            {
                deviceInfoTimer.Change(0, Constants.MUSE_DEVICE_INFO_CONTROL_REFRESH_MS); // "Resume" the timer.
                FinishCloseOffStream();
            }
        }

        public async Task Reset()
        {
            if (!CanReset) return;
            var characteristics = await GetGattCharacteristics();
            if (characteristics == null)
            {
                Log.Error($"Cannot reset device due to null GATT characteristics.");
                return;
            }
            await WriteCommand(Constants.MUSE_CMD_ASK_RESET, characteristics);
        }

        public async void RefreshDeviceInfoAndControlStatus(object state = null)
        {
            try
            {
                deviceInfoBuffer = string.Empty;
                controlStatusBuffer = string.Empty;

                if (CanRefreshInfo)
                {
                    streamCharacteristics = streamCharacteristics ?? await GetGattCharacteristics();
                    if (streamCharacteristics == null)
                    {
                        Log.Error($"Cannot get device info due to null GATT characteristics.");
                        return;
                    }

                    // Subscribe to the command channel which we will also write a command to which asks for device info.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, streamCharacteristics, true, DeviceInfo_ValueChanged))
                    {
                        Log.Error($"Cannot get device info due to failure to toggle characteristics for info.");
                    }
                    // Ask for device info. We use the same command handler which waits for multiple packets to deliver a JSON value.
                    await WriteCommand(Constants.MUSE_CMD_ASK_DEVICE_INFO, streamCharacteristics);
                    await Task.Delay(1000); // Small delay to ensure we fully receive the info.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, streamCharacteristics, false, DeviceInfo_ValueChanged))
                    {
                        Log.Error($"Cannot get device info due to failure to toggle characteristics for info.");
                    }
                    DeviceInfo = deviceInfoLive; // Update "stable" property.

                    // Subscribe to the command channel which we will also write a command to which asks for control status.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, streamCharacteristics, true, ControlStatus_ValueChanged))
                    {
                        Log.Error($"Cannot get control status due to failure to toggle characteristics for info.");
                    }
                    // Ask for control status. We use the same command handler which waits for multiple packets to deliver a JSON value.
                    await WriteCommand(Constants.MUSE_CMD_ASK_CONTROL_STATUS, streamCharacteristics);
                    await Task.Delay(1000); // Small delay to ensure we fully receive the info.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, streamCharacteristics, false, ControlStatus_ValueChanged))
                    {
                        Log.Error($"Cannot get control status due to failure to toggle characteristics for info.");
                    }
                    ControlStatus = controlStatusLive; // Update "stable" property.
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error while getting device info / control status.", ex);
            }
        }

        private void DeviceInfo_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    string bits = GetBits(args.CharacteristicValue);
                    // Each packet contains a 1 byte values.
                    int length = args.CharacteristicValue.GetByte(0);
                    char[] chars = Encoding.ASCII.GetChars(args.CharacteristicValue.ToArray(1, 19));
                    string text = new string(chars).Substring(0, length);

                    if (string.IsNullOrEmpty(deviceInfoBuffer) && text.FirstOrDefault() == '{')
                    {
                        deviceInfoBuffer += text;
                    }
                    else if (!string.IsNullOrEmpty(deviceInfoBuffer) && !deviceInfoBuffer.EndsWith(text))
                    {
                        deviceInfoBuffer += text;
                    }

                    // If our message starts with '{' and ends with '}' we know it is complete JSON data that we can use.
                    if (deviceInfoBuffer.FirstOrDefault() == '{' && deviceInfoBuffer.LastOrDefault() == '}')
                    {
                        var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(deviceInfoBuffer), Formatting.Indented);
                        if (json != controlStatus) deviceInfoLive = json; // Weird check, but somehow these can collide.
                        deviceInfoBuffer = string.Empty; // Clear the buffer as we are starting from fresh.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during handling device info Bluetooth channel values.", ex);
                deviceInfoBuffer = string.Empty;
            }
        }

        private void ControlStatus_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    string bits = GetBits(args.CharacteristicValue);
                    // Each packet contains a 1 byte values.
                    int length = args.CharacteristicValue.GetByte(0);
                    char[] chars = Encoding.ASCII.GetChars(args.CharacteristicValue.ToArray(1, 19));
                    string text = new string(chars).Substring(0, length);

                    if (string.IsNullOrEmpty(controlStatusBuffer) && text.FirstOrDefault() == '{')
                    {
                        controlStatusBuffer += text;
                    }
                    else if (!string.IsNullOrEmpty(controlStatusBuffer) && !controlStatusBuffer.EndsWith(text))
                    {
                        controlStatusBuffer += text;
                    }

                    // If our message starts with '{' and ends with '}' we know it is complete JSON data that we can use.
                    if (controlStatusBuffer.FirstOrDefault() == '{' && controlStatusBuffer.LastOrDefault() == '}')
                    {
                        var batteryPer = Regex.Match(controlStatusBuffer, "\"bp\":\\W*([0-9]+)");
                        if (batteryPer.Success)
                        {
                            if (int.TryParse(batteryPer.Groups[1].Value, out int batteryInt))
                            {
                                BatteryLevel = batteryInt;
                            }
                        }
                        var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(controlStatusBuffer), Formatting.Indented);
                        if (json != deviceInfo) controlStatusLive = json; // Weird check, but somehow these can collide.
                        controlStatusBuffer = string.Empty; // Clear the buffer as we are starting from fresh.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during handling control status Bluetooth channel values.", ex);
                controlStatusBuffer = string.Empty;
            }
        }

        private async Task<bool> WriteCommand(byte[] command, IReadOnlyList<GattCharacteristic> characteristics)
        {
            if (characteristics == null)
            {
                Log.Error($"Cannot perform command {command} due to null GATT characteristics.");
                return false;
            }

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
                    characteristic.ValueChanged += eventHandler;
                    notifyToggle = GattClientCharacteristicConfigurationDescriptorValue.Notify;
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
            ppgSampleBuffer.Clear();
            await LSLCloseStream();
            IsStreaming = false;
            streamCharacteristics = null;
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
            if (isTelemetryEnabled)
            {
                await LSLOpenTelemetry();
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
                DeviceManufacturer = lslDeviceInfoManufacturer,
                DeviceName = lslDeviceInfoName,
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
                DeviceManufacturer = lslDeviceInfoManufacturer,
                DeviceName = lslDeviceInfoName,
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
                DeviceManufacturer = lslDeviceInfoManufacturer,
                DeviceName = lslDeviceInfoName,
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
                DeviceManufacturer = lslDeviceInfoManufacturer,
                DeviceName = lslDeviceInfoName,
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

        private async Task LSLOpenTelemetry()
        {
            var channelsInfo = new List<LSLBridgeChannelInfo>();
            foreach (var c in Constants.MUSE_TELEMETRY_CHANNEL_LABELS)
            {
                channelsInfo.Add(new LSLBridgeChannelInfo
                {
                    Label = c,
                    Type = Constants.TELEMETRY_STREAM_TYPE,
                    Unit = "Various"
                });
            }

            LSLBridgeStreamInfo streamInfo = new LSLBridgeStreamInfo()
            {
                BufferLength = Constants.MUSE_LSL_BUFFER_LENGTH,
                Channels = channelsInfo,
                ChannelCount = Constants.MUSE_TELEMETRY_CHANNEL_COUNT,
                ChannelDataType = channelDataType.DataType,
                ChunkSize = Constants.MUSE_TELEMETRY_SAMPLE_COUNT,
                DeviceManufacturer = lslDeviceInfoManufacturer,
                DeviceName = lslDeviceInfoName,
                NominalSRate = Constants.MUSE_TELEMETRY_SAMPLE_RATE,
                StreamType = Constants.TELEMETRY_STREAM_TYPE,
                SendSecondaryTimestamp = timestampFormat2.GetType() != typeof(DummyTimestampFormat),
                StreamName = TelemetryStreamName
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
                 new string[] { EEGStreamName, AccelerometerStreamName, GyroscopeStreamName, PPGStreamName, TelemetryStreamName })
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
                    var channelData = sample.ChannelData[eegGattChannelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_EEG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_EEG_SAMPLE_COUNT) + j] = channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_EEG_SAMPLE_COUNT * eegChannelCount];
                for (int i = 0; i < eegChannelCount; i++)
                {
                    var channelData = sample.ChannelData[eegGattChannelUUIDs[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_EEG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_EEG_SAMPLE_COUNT) + j] = (float)channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL EEG chunk - unsupported stream data type. Must use float32 or double64.");

            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);
        }

        private async Task LSLPushPPGChunk(MusePPGSamples sample)
        {
            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, PPGStreamName }
            };

            // Can only send 1D array with garbage AppService :S - inlined as channel1sample1,channel1sample2,channel1sample3...channel2sample1,channel2sample2...
            if (channelDataType.DataType == LSLBridgeDataType.DOUBLE)
            {
                double[] data = new double[Constants.MUSE_PPG_SAMPLE_COUNT * Constants.MUSE_PPG_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_PPG_CHANNEL_COUNT; i++)
                {
                    var channelData = sample.ChannelData[Constants.MUSE_GATT_PPG_CHANNEL_UUIDS[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_PPG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_PPG_SAMPLE_COUNT) + j] = channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_PPG_SAMPLE_COUNT * Constants.MUSE_PPG_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_PPG_CHANNEL_COUNT; i++)
                {
                    var channelData = sample.ChannelData[Constants.MUSE_GATT_PPG_CHANNEL_UUIDS[i]]; // Maintains muse-lsl.py ordering.
                    for (int j = 0; j < Constants.MUSE_PPG_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_PPG_SAMPLE_COUNT) + j] = (float)channelData[j];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL PPG chunk - unsupported stream data type. Must use float32 or double64.");

            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);
        }

        private async Task LSLPushAccelerometerChunk(MuseAccelerometerSamples sample)
        {
            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, AccelerometerStreamName }
            };

            // Can only send 1D array with garbage AppService :S - inlined as xsample1,xsample2...zsample1,zsample2...
            if (channelDataType.DataType == LSLBridgeDataType.DOUBLE)
            {
                double[] data = new double[Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT * Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT) + j] = sample.XYZSamples[j, i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT * Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_ACCELEROMETER_SAMPLE_COUNT) + j] = (float)sample.XYZSamples[j, i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL Accelerometer chunk - unsupported stream data type. Must use float32 or double64.");

            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);
        }

        private async Task LSLPushGyroscopeChunk(MuseGyroscopeSamples sample)
        {
            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, GyroscopeStreamName }
            };

            // Can only send 1D array with garbage AppService :S - inlined as xsample1,xsample2...zsample1,zsample2...
            if (channelDataType.DataType == LSLBridgeDataType.DOUBLE)
            {
                double[] data = new double[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT * Constants.MUSE_GYROSCOPE_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_GYROSCOPE_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_GYROSCOPE_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_GYROSCOPE_SAMPLE_COUNT) + j] = sample.XYZSamples[j, i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT * Constants.MUSE_GYROSCOPE_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_GYROSCOPE_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_GYROSCOPE_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_GYROSCOPE_SAMPLE_COUNT) + j] = (float)sample.XYZSamples[j, i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL Accelerometer chunk - unsupported stream data type. Must use float32 or double64.");

            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS, sample.Timestamps);
            message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2, sample.Timestamps2);

            await AppServiceManager.SendMessageAsync(LSLBridge.Constants.LSL_MESSAGE_TYPE_SEND_CHUNK, message);
        }

        private async Task LSLPushTelemetryChunk(MuseTelemetrySamples sample)
        {
            ValueSet message = new ValueSet
            {
                { LSLBridge.Constants.LSL_MESSAGE_STREAM_NAME, TelemetryStreamName }
            };

            // Can only send 1D array with garbage AppService :S - inlined as batterysample1,batterysample2...temperaturesample1,temperaturesample2...
            if (channelDataType.DataType == LSLBridgeDataType.DOUBLE)
            {
                double[] data = new double[Constants.MUSE_TELEMETRY_SAMPLE_COUNT * Constants.MUSE_TELEMETRY_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_TELEMETRY_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_TELEMETRY_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_TELEMETRY_SAMPLE_COUNT) + j] = sample.TelemetryData[i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else if (channelDataType.DataType == LSLBridgeDataType.FLOAT)
            {
                float[] data = new float[Constants.MUSE_TELEMETRY_SAMPLE_COUNT * Constants.MUSE_TELEMETRY_CHANNEL_COUNT];
                for (int i = 0; i < Constants.MUSE_TELEMETRY_CHANNEL_COUNT; i++)
                {
                    for (int j = 0; j < Constants.MUSE_TELEMETRY_SAMPLE_COUNT; j++)
                    {
                        data[(i * Constants.MUSE_TELEMETRY_SAMPLE_COUNT) + j] = (float)sample.TelemetryData[i];
                    }
                }
                message.Add(LSLBridge.Constants.LSL_MESSAGE_CHUNK_DATA, data);
            }

            else throw new InvalidOperationException("Can't push LSL Telemetry chunk - unsupported stream data type. Must use float32 or double64.");

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
                        samples.ChannelData[sender.Uuid] = MuseEEGSamples.DecodeEEGSamples(bits);
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
                    Log.Error($"Exception during handling EEG Blueooth channel values.", ex);
                }
            }
        }

        private async void PPGChannel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = GetBits(args.CharacteristicValue);
                    ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MusePPGSamples samples;
                    lock (ppgSampleBuffer)
                    {
                        if (!ppgSampleBuffer.ContainsKey(museTimestamp))
                        {
                            samples = new MusePPGSamples();
                            ppgSampleBuffer.Add(museTimestamp, samples);
                            samples.BaseTimestamp = timestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                            samples.BasetimeStamp2 = timestampFormat.GetType() != timestampFormat2.GetType() ?
                                  timestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                                : samples.BasetimeStamp2 = samples.BaseTimestamp; // Ensures they are equal if using same timestamp format.
                        }
                        else samples = ppgSampleBuffer[museTimestamp];

                        // Get time samples.
                        samples.ChannelData[sender.Uuid] = MusePPGSamples.DecodePPGSamples(bits);
                    }
                    // If we have all PPG channels, we can push the 12 samples for each channel.
                    if (samples.ChannelData.Count == Constants.MUSE_PPG_CHANNEL_COUNT)
                    {
                        await LSLPushPPGChunk(samples);
                        lock (ppgSampleBuffer)
                            ppgSampleBuffer.Remove(museTimestamp);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during handling PPG Bluetooth channel values.", ex);
                }
            }
        }

        private async void Accelerometer_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = GetBits(args.CharacteristicValue);
                    ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MuseAccelerometerSamples samples = new MuseAccelerometerSamples();
                    samples.BaseTimestamp = timestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                    samples.BasetimeStamp2 = timestampFormat.GetType() != timestampFormat2.GetType() ?
                            timestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                        : samples.BasetimeStamp2 = samples.BaseTimestamp; // Ensures they are equal if using same timestamp format.

                    samples.XYZSamples = MuseAccelerometerSamples.DecodeAccelerometerSamples(bits);
                    await LSLPushAccelerometerChunk(samples);
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during handling accelerometer Bluetooth channel values.", ex);
                }
            }
        }

        private async void Gyroscope_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = GetBits(args.CharacteristicValue);
                    ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MuseGyroscopeSamples samples = new MuseGyroscopeSamples();
                    samples.BaseTimestamp = timestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                    samples.BasetimeStamp2 = timestampFormat.GetType() != timestampFormat2.GetType() ?
                            timestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                        : samples.BasetimeStamp2 = samples.BaseTimestamp; // Ensures they are equal if using same timestamp format.

                    samples.XYZSamples = MuseGyroscopeSamples.DecodeGyroscopeSamples(bits);
                    await LSLPushGyroscopeChunk(samples);
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during handling gyroscope Bluetooth channel values.", ex);
                }
            }
        }

        private async void Telemetry_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = GetBits(args.CharacteristicValue);
                    ushort museTimestamp = PacketConversion.ToUInt16(bits, 0); // Zero bit offset, since first 16 bits represent Muse timestamp.
                    MuseTelemetrySamples samples = new MuseTelemetrySamples();
                    samples.BaseTimestamp = timestampFormat.GetNow(); // This is the real timestamp, not the Muse timestamp which we use to group channel data.
                    samples.BasetimeStamp2 = timestampFormat.GetType() != timestampFormat2.GetType() ?
                            timestampFormat2.GetNow() // This is the real timestamp (format 2), not the Muse timestamp which we use to group channel data.
                        : samples.BasetimeStamp2 = samples.BaseTimestamp; // Ensures they are equal if using same timestamp format.

                    samples.TelemetryData = MuseTelemetrySamples.DecodeTelemetrySamples(bits);

                    BatteryLevel = (int)samples.TelemetryData[0];

                    if (isTelemetryEnabled)
                    {
                        await LSLPushTelemetryChunk(samples);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during handling telemetry Bluetooth channel values.", ex);
                }
            }
        }

    }
}
