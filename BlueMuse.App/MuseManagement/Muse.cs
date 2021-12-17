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
        private IReadOnlyList<GattCharacteristic> deviceControlCharacteristics;

        // We have to buffer EEG and PPG, since any given sample is composed of data from multiple Bluetooth channels (Gatt characteristics).
        private volatile Dictionary<ushort, MuseEEGSamples> eegSampleBuffer;
        private volatile Dictionary<ushort, MusePPGSamples> ppgSampleBuffer;
        private volatile string deviceInfoBuffer = string.Empty;
        private volatile string controlStatusBuffer = string.Empty;
        private DateTimeOffset auxLastSent;

        private volatile bool togglingStream = false;
        private volatile bool resetLocked = false;

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

        private MuseConnectionStatus connectionStatus;
        public MuseConnectionStatus ConnectionStatus
        {
            get { return connectionStatus; }
            set
            {
                lock (syncLock)
                {
                    if (value == MuseConnectionStatus.Online)
                    {
                        DetermineMuseModel();
                    }
                    SetProperty(ref connectionStatus, value);
                    OnPropertyChanged(nameof(MuseModel));
                    OnPropertyChanged(nameof(CanStream));
                    OnPropertyChanged(nameof(CanReset));
                    OnPropertyChanged(nameof(CanViewTechInfo));
                }
            }
        }

        private bool isStreaming;
        public bool IsStreaming
        {
            get
            {
                return isStreaming;
            }
            set
            {
                lock (syncLock)
                {
                    SetProperty(ref isStreaming, value);
                    OnPropertyChanged(nameof(CanReset));
                    OnPropertyChanged(nameof(CanViewTechInfo));
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                lock (syncLock)
                {
                    SetProperty(ref isSelected, value);
                    OnPropertyChanged(nameof(CanReset));
                    OnPropertyChanged(nameof(CanViewTechInfo));
                }
            }
        }

        public bool CanReset { get { return connectionStatus == MuseConnectionStatus.Online && !isStreaming && isSelected; } }

        public bool CanViewTechInfo
        {
            get
            {
                return
                    ((connectionStatus == MuseConnectionStatus.Online && !isStreaming) || (!string.IsNullOrEmpty(DeviceInfo) && !string.IsNullOrEmpty(ControlStatus)))
                    &&
                    isSelected
                ;
            }
        }

        public bool CanStream { get { return connectionStatus == MuseConnectionStatus.Online && !resetLocked; } }
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
            ConnectionStatus = status;
            DetermineMuseModel();
            deviceInfoTimer = new Timer(RefreshDeviceInfoAndControlStatus, null, 250, Constants.MUSE_DEVICE_INFO_CONTROL_REFRESH_MS);
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
            if (ConnectionStatus == MuseConnectionStatus.Offline) return;
            try
            {
                streamCharacteristics = await GetGattCharacteristics();
                if (streamCharacteristics?.Count < 1)
                {
                    Log.Error($"Cannot complete determining Muse model due to null or empty GATT characteristics.");
                    MuseModel = MuseModel.Undetected;
                }

                // Muse S should have device name MuseS-****. As another check, it should have a channel that the Muse 2 and Muse 2016 do not have.
                if (name.Contains("MuseS") || streamCharacteristics.FirstOrDefault(x => x.Uuid == Constants.MUSE_S_SPECIAL_CHANNEL) != null)
                {
                    MuseModel = MuseModel.MuseS;
                    eegChannelCount = Constants.MUSE_EEG_NOAUX_CHANNEL_COUNT;
                    eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_NOAUX_CHANNEL_LABELS;
                    lslDeviceInfoName = Constants.MUSE_S_DEVICE_NAME;
                }
                // Device has PPG, therefore we know it's a Muse 2.
                else if (streamCharacteristics.FirstOrDefault(x => x.Uuid == Constants.MUSE_GATT_PPG_CHANNEL_UUIDS[0]) != null)
                {
                    MuseModel = MuseModel.Muse2;
                    eegChannelCount = Constants.MUSE_EEG_CHANNEL_COUNT;
                    eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_CHANNEL_LABELS;
                    lslDeviceInfoName = Constants.MUSE_2_DEVICE_NAME;
                }
                else
                {
                    MuseModel = MuseModel.Muse2016;
                    eegChannelCount = Constants.MUSE_EEG_CHANNEL_COUNT;
                    eegGattChannelUUIDs = Constants.MUSE_GATT_EGG_CHANNEL_UUIDS;
                    eegChannelLabels = Constants.MUSE_EEG_CHANNEL_LABELS;
                    lslDeviceInfoName = Constants.MUSE_DEVICE_NAME;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during determining Muse model. Exception message: {ex.Message}.");
            }
        }

        public async Task ToggleStream(bool start)
        {
            if (MuseModel == MuseModel.Undetected) DetermineMuseModel();

            lock (syncLock)
            {
                if (start == isStreaming || (start && !CanStream)) return;
                togglingStream = true;
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
                    isPPGEnabled = IsPPGEnabled && (museModel == MuseModel.Muse2 || museModel == MuseModel.MuseS); // Only Muse 2 & Muse S support PPG.
                    isTelemetryEnabled = IsTelemetryEnabled;

                    if (!isEEGEnabled &&
                        !isAccelerometerEnabled &&
                        !isGyroscopeEnabled &&
                        !isPPGEnabled &&
                        !isTelemetryEnabled) return; // Nothing enabled, can't start under this condition.

                    deviceInfoTimer.Change(Timeout.Infinite, Timeout.Infinite); // "Pause" the timer.

                    // Should only need to acquire stream characteristics during start, they will then be reused upon stopping the stream.
                    streamCharacteristics = await GetGattCharacteristics();
                }

                if (streamCharacteristics?.Count < 1)
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to null or empty GATT characteristics.");
                    if (start) return;
                    else FinishCloseOffStream();
                }

                // Subscribe or unsubscribe EEG.
                if (isEEGEnabled)
                {
                    if (!await ToggleCharacteristics(eegGattChannelUUIDs, streamCharacteristics, start, EEGChannel_ValueChanged))
                    {
                        Log.Error($"Cannot complete toggle stream (start={start}) due to failure to toggle characteristics for EEG.");
                        if (start)
                            return;
                    }
                    auxLastSent = DateTimeOffset.MinValue;
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

                // When starting we need to make sure Muse is in proper preset mode to get all data.
                // TODO: it may be ideal to introduce an AUX toggle, and see if PPG can be disabled at the Muse level (when not toggled off in settings).
                if (start)
                {
                    byte[] preset = null;
                    if (MuseModel == MuseModel.Smith)
                    {
                        preset = Constants.MUSE_CMD_PRESET_MODE_P21;
                    }
                    else if (MuseModel == MuseModel.Muse2016)
                    {
                        preset = Constants.MUSE_CMD_PRESET_MODE_P20;
                    }
                    else if (MuseModel == MuseModel.Muse2 || MuseModel == MuseModel.MuseS)
                    {
                        preset = Constants.MUSE_CMD_PRESET_MODE_P50;
                    }
                    if (!await WriteCommand(preset, streamCharacteristics))
                    {
                        Log.Error($"Cannot place Muse in proper preset mode: {preset}");
                    }
                }

                // Determine if we are listening to Gatt channels or stopping (notify vs none) and what command to send to the Muse (start or stop data).
                byte[] toggleCommand = start ? Constants.MUSE_CMD_TOGGLE_STREAM_START : Constants.MUSE_CMD_TOGGLE_STREAM_STOP;

                // Tell Muse to start or stop notifications.
                if (!await WriteCommand(toggleCommand, streamCharacteristics))
                {
                    Log.Error($"Cannot complete toggle stream (start={start}) due to failure to run toggle command.");
                }

                if (start)
                    FinishOpenStream();
                else
                {
                    FinishCloseOffStream();
                    deviceInfoTimer.Change(Constants.MUSE_DEVICE_INFO_CONTROL_REFRESH_MS, Constants.MUSE_DEVICE_INFO_CONTROL_REFRESH_MS); // "Resume" the timer.
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during toggle stream (start={start}). Exception message: {ex.Message}.");
                try
                {
                    // Attempt to clean up, but this can fail.
                    if (!start) FinishCloseOffStream();
                }
                catch (Exception ext)
                {
                    Log.Error(ext, $"Exception during FinishCloseOffStream() after an exception was already caught while toggling. Exception message: {ext.Message}.");
                }
            }
            finally
            {
                togglingStream = false;
            }
        }

        public async Task Reset()
        {
            if (!CanReset) return;
            var characteristics = await GetGattCharacteristics();
            if (characteristics?.Count < 1)
            {
                Log.Error($"Cannot reset device due to null or empty GATT characteristics.");
                return;
            }
            await WriteCommand(Constants.MUSE_CMD_ASK_RESET, characteristics);

            lock (syncLock)
            {
                resetLocked = true;
                OnPropertyChanged(nameof(CanStream));
            }
            await Task.Delay(10000); // Add a reasonably long delay so the device has a chance to fully reset, otherwise we can get some sketchy errors.
            lock (syncLock)
            {
                resetLocked = false;
                OnPropertyChanged(nameof(CanStream));
            }
        }

        public async void RefreshDeviceInfoAndControlStatus(object state = null)
        {
            try
            {
                deviceInfoBuffer = string.Empty;
                controlStatusBuffer = string.Empty;

                if (connectionStatus == MuseConnectionStatus.Online && !isStreaming && !resetLocked)
                {
                    deviceControlCharacteristics = await GetGattCharacteristics();
                    if (deviceControlCharacteristics?.Count < 1)
                    {
                        Log.Error($"Cannot get device info due to null or empty GATT characteristics.");
                        return;
                    }
                    if (togglingStream || resetLocked) return; // Prevents Bluetooth errors.

                    // Subscribe to the command channel which we will also write a command to which asks for device info.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, deviceControlCharacteristics, true, DeviceInfo_ValueChanged))
                    {
                        Log.Error($"Cannot get device info due to failure to toggle characteristics for info.");
                        return;
                    }
                    // Ask for device info. We use the same command handler which waits for multiple packets to deliver a JSON value.
                    await WriteCommand(Constants.MUSE_CMD_ASK_DEVICE_INFO, deviceControlCharacteristics);
                    await Task.Delay(800); // Small delay to ensure we fully receive the info.
                    if (deviceInfoLive?.Length > 0) DeviceInfo = deviceInfoLive; // Update "stable" property.

                    // Unsubscribe.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, deviceControlCharacteristics, false, DeviceInfo_ValueChanged))
                    {
                        Log.Error($"Cannot get device info due to failure to toggle characteristics for info.");
                        return;
                    }
                    if (togglingStream || resetLocked) return; // Prevents Bluetooth errors.

                    await Task.Delay(500); // Small delay so that we don't get device info values coming into the control handlers.

                    // Subscribe to the command channel which we will also write a command to which asks for control status.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, deviceControlCharacteristics, true, ControlStatus_ValueChanged))
                    {
                        Log.Error($"Cannot get control status due to failure to toggle characteristics for info.");
                        return;
                    }
                    // Ask for control status. We use the same command handler which waits for multiple packets to deliver a JSON value.
                    await WriteCommand(Constants.MUSE_CMD_ASK_CONTROL_STATUS, deviceControlCharacteristics);
                    await Task.Delay(800); // Small delay to ensure we fully receive the info.
                    if (controlStatusLive?.Length > 0) ControlStatus = controlStatusLive; // Update "stable" property.

                    // Unsubscribe.
                    if (!await ToggleCharacteristics(new[] { Constants.MUSE_GATT_COMMAND_UUID }, deviceControlCharacteristics, false, ControlStatus_ValueChanged))
                    {
                        Log.Error($"Cannot get control status due to failure to toggle characteristics for info.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected error while getting device info / control status.");
            }
        }

        private void DeviceInfo_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
                    // Each packet contains a 1 byte length value (n) at the beginning followed by characters of length n.
                    int length = args.CharacteristicValue.GetByte(0);
                    char[] chars = Encoding.ASCII.GetChars(args.CharacteristicValue.ToArray(1, length));
                    string text = new string(chars);

                    // Sometimes it seems duplicate data comes in bursts, so this check prevents that.
                    if(!deviceInfoBuffer.EndsWith(text))
                    {
                        deviceInfoBuffer += text;
                    }

                    // If our message contains a '{' and ends with '}' we know somewhere, we have complete JSON data that we can use.
                    if (deviceInfoBuffer.Contains("{") && deviceInfoBuffer.LastOrDefault() == '}')
                    {
                        deviceInfoBuffer = deviceInfoBuffer.Substring(deviceInfoBuffer.LastIndexOf('{'));
                        try
                        {
                            var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(deviceInfoBuffer), Formatting.Indented);
                            if (json != controlStatus) deviceInfoLive = json; // Weird check, but somehow these can collide.
                        }
                        catch (JsonReaderException) { } // Don't care, probably throws a JSON error since the data is messed up.
                        finally
                        {
                            deviceInfoBuffer = string.Empty; // Clear the buffer as we are starting from fresh.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during handling device info Bluetooth channel values.");
                deviceInfoBuffer = string.Empty;
            }
        }

        private void ControlStatus_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
                    // Each packet contains a 1 byte length value (n) at the beginning followed by characters of length n.
                    int length = args.CharacteristicValue.GetByte(0);
                    char[] chars = Encoding.ASCII.GetChars(args.CharacteristicValue.ToArray(1, length));
                    string text = new string(chars);

                    // Sometimes it seems duplicate data comes in bursts, so this check prevents that.
                    if (!controlStatusBuffer.EndsWith(text))
                    {
                        controlStatusBuffer += text;
                    }

                    // If our message contains a '{' and ends with '}' we know somewhere, we have complete JSON data that we can use.
                    if (controlStatusBuffer.Contains("{") && controlStatusBuffer.LastOrDefault() == '}')
                    {
                        controlStatusBuffer = controlStatusBuffer.Substring(controlStatusBuffer.LastIndexOf('{'));

                        // Pull our battery info, we can do this before parsing the JSON.
                        var batteryPer = Regex.Match(controlStatusBuffer, "\"bp\":\\W*([0-9]+)");
                        if (batteryPer.Success)
                        {
                            if (int.TryParse(batteryPer.Groups[1].Value, out int batteryInt))
                            {
                                BatteryLevel = batteryInt;
                            }
                        }
                        try
                        {
                            var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(controlStatusBuffer), Formatting.Indented);
                            if (json != deviceInfo) controlStatusLive = json; // Weird check, but somehow these can collide.
                        }
                        catch (JsonReaderException) { } // Don't care, probably throws a JSON error since the data is messed up.
                        finally
                        {
                            controlStatusBuffer = string.Empty; // Clear the buffer as we are starting from fresh.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during handling control status Bluetooth channel values.");
                controlStatusBuffer = string.Empty;
            }
        }

        private async Task<bool> WriteCommand(byte[] command, IReadOnlyList<GattCharacteristic> characteristics)
        {
            try
            {
                if (characteristics == null || characteristics.Count < 1)
                {
                    Log.Error($"Cannot perform command {command} due to null or empty GATT characteristics.");
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
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected error during write command {command}");
                return false;
            }
        }

        private async Task<IReadOnlyList<GattCharacteristic>> GetGattCharacteristics(string deviceServiceUuid = null)
        {
            try
            {
                if (deviceServiceUuid == null) deviceServiceUuid = Constants.MUSE_GATT_DATA_SERVICE_UUID.ToString();

                // Get GATT device service.
                var deviceService = await Device.GetGattServicesForUuidAsync(new Guid(deviceServiceUuid), BluetoothCacheMode.Uncached);
                if (deviceService == null)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected null GATT device service (UUID={deviceServiceUuid}).");
                    return new List<GattCharacteristic>();
                }
                else if (deviceService.Status != GattCommunicationStatus.Success)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected communication error with GATT device service (UUID={deviceServiceUuid}). Status: {deviceService.Status}. Protocol Error {deviceService.ProtocolError}.");
                    return new List<GattCharacteristic>();
                }
                else if (deviceService.Services == null || deviceService.Services.Count < 1)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected null or empty GATT device service (UUID={deviceServiceUuid}) services.");
                    return new List<GattCharacteristic>();
                }
                else if (deviceService.Services.FirstOrDefault() == null)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected null GATT device service list of services (UUID={deviceServiceUuid}).");
                    return new List<GattCharacteristic>();
                }

                var characteristics = await deviceService.Services.First().GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (characteristics == null)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected null characteristics.");
                    return new List<GattCharacteristic>();
                }
                else if (characteristics.Status != GattCommunicationStatus.Success)
                {
                    Log.Error($"Cannot get GATT characteristics due to unexpected communication error with GATT device service (UUID={deviceServiceUuid}). Status: {characteristics.Status}. Protocol Error {characteristics.ProtocolError}.");
                    return new List<GattCharacteristic>();
                }
                else return characteristics.Characteristics;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected error get GATT characteristics with GATT device service (UUID={deviceServiceUuid}).");
                return new List<GattCharacteristic>();
            }
        }

        private async Task<bool> ToggleCharacteristics(Guid[] characteristicTargets, IReadOnlyList<GattCharacteristic> characteristics, bool start, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> eventHandler)
        {
            if (characteristics == null || characteristics.Count < 1)
            {
                Log.Error($"Cannot toggle characteristics (start={start}) due to unexpected null or empty characteristics list.");
                return false;
            }

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
                ChannelCount = Constants.MUSE_PPG_CHANNEL_COUNT,
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

        private async void EEGChannel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    if (sender.Uuid == Constants.MUSE_GATT_AUX_CHANNEL_UUID)
                    {
                        auxLastSent = DateTimeOffset.UtcNow;
                    }
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
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
                    else if (samples.ChannelData.Count == eegChannelCount - 1 &&
                      !samples.ChannelData.ContainsKey(Constants.MUSE_GATT_AUX_CHANNEL_UUID) &&
                      (DateTime.UtcNow - auxLastSent).TotalMilliseconds > Constants.MUSE_EEG_NOAUX_TIMEOUT_THRESHOLD_MILLIS)
                    {
                        samples.ChannelData.Add(Constants.MUSE_GATT_AUX_CHANNEL_UUID, Constants.MUSE_EEG_BACKFILL);
                        await LSLPushEEGChunk(samples);
                        lock (eegSampleBuffer)
                            eegSampleBuffer.Remove(museTimestamp);
                    }

                    // Cleanup broken samples.
                    var flushSamples = eegSampleBuffer.Where(x =>
                        (DateTimeOffset.UtcNow - x.Value.CreatedAt).TotalMilliseconds > Constants.MUSE_EEG_FLUSH_THRESHOLD_MILLIS)
                            .OrderBy(x => x.Value.BaseTimestamp);
                    foreach (var sample in flushSamples)
                    {
                        var channelData = sample.Value.ChannelData;
                        if (channelData.Count != eegChannelCount)
                        {
                            var missingChannels = eegGattChannelUUIDs.Where(x => !channelData.ContainsKey(x));
                            foreach (var channel in missingChannels)
                            {
                                channelData.Add(channel, Constants.MUSE_EEG_BACKFILL);
                            }
                        }
                        await LSLPushEEGChunk(sample.Value);
                        lock (eegSampleBuffer)
                            eegSampleBuffer.Remove(sample.Key);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Exception during handling EEG Blueooth channel values.");
                }
            }
        }

        private async void PPGChannel_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
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
                    Log.Error(ex, $"Exception during handling PPG Bluetooth channel values.");
                }
            }
        }

        private async void Accelerometer_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
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
                    Log.Error(ex, $"Exception during handling accelerometer Bluetooth channel values.");
                }
            }
        }

        private async void Gyroscope_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
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
                    Log.Error(ex, $"Exception during handling gyroscope Bluetooth channel values.");
                }
            }
        }

        private async void Telemetry_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (isStreaming)
            {
                try
                {
                    string bits = PacketConversion.GetBits(args.CharacteristicValue);
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
                    Log.Error(ex, $"Exception during handling telemetry Bluetooth channel values.");
                }
            }
        }

    }
}
