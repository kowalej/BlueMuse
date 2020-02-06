using System;
using System.Collections.Generic;

namespace BlueMuse
{
    static class Constants
    {
        // Device name filter will pickup glasses, Muse original, and Muse 2. Names with be something like SMTX-xxxx or Muse-xxxx.
        public static readonly List<string> DeviceNameFilter = new List<string>()
        {
            "Muse", "SMTX"
        };

        public const string ALL_AQS = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\"";
        public const string MUSE_AQS = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\" AND (System.ItemNameDisplay:~~\"Muse\" OR System.ItemNameDisplay:~~\"SMTX\")"; // Needs testing.

        public const string ARGS_STREAMFIRST = "streamfirst";
        public const string ARGS_ADDRESSES = "addresses";
        public const string ARGS_STARTALL = "startall";
        public const string ARGS_STOPALL = "stopall";
        public const string ARGS_SETTING_KEY = "key";
        public const string ARGS_SETTING_VALUE = "value";

        public const string CMD_START = "start";
        public const string CMD_STOP = "stop";
        public const string CMD_CLOSE_PROGRAM = "shutdown";
        public const string CMD_SET_SETTING = "setting";

        public const int MUSE_EEG_SAMPLE_RATE = 256;
        public const float MUSE_EEG_SAMPLE_TIME_MILLIS = 1000f / MUSE_EEG_SAMPLE_RATE;

        public const int MUSE_ACCELEROMETER_SAMPLE_RATE = 52;
        public const float MUSE_ACCELEROMETER_SAMPLE_TIME_MILLIS = 1.0f / MUSE_ACCELEROMETER_SAMPLE_RATE; // Theoretical time between consecutive Accelerometer samples.

        public const int MUSE_GYROSCOPE_SAMPLE_RATE = 52;
        public const float MUSE_GYROSCOPE_SAMPLE_TIME_MILLIS = 1.0f / MUSE_GYROSCOPE_SAMPLE_RATE; // Theoretical time between consecutive Gyroscope samples.

        public const int MUSE_PPG_SAMPLE_RATE = 64;
        public const float MUSE_PPG_SAMPLE_TIME_MILLIS = 1.0f / MUSE_PPG_SAMPLE_RATE; // Theoretical time between consecutive PPG samples.

        // Muse specific channel counts.
        public const int MUSE_EEG_CHANNEL_COUNT = 5;

        // Muse 2 specific channel counts.
        public const int MUSE_2_EEG_CHANNEL_COUNT = 4;
        public const int MUSE_2_PPG_CHANNEL_COUNT = 3;

        // SMXT specific channel counts.
        public const int MUSE_SMXT_EEG_CHANNEL_COUNT = 4;

        // Shared channel counts (Muse, Muse2, SMXT).
        public const int MUSE_ACCELEROMETER_CHANNEL_COUNT = 3;
        public const int MUSE_GYROSCOPE_CHANNEL_COUNT = 3;

        public const string MUSE_DEVICE_NAME = "Muse EEG Headset";
        public const string MUSE_MANUFACTURER = "Interaxon";

        public const string MUSE_2_DEVICE_NAME = "Muse 2 EEG Headset";
        public const string MUSE_2_MANUFACTURER = "Interaxon";

        public const string MUSE_SMXT_DEVICE_NAME = "Smith Lowdown Focus";
        public const string MUSE_SMXT_MANUFACTURER = "Smith";

        public const int MUSE_EEG_SAMPLE_COUNT = 12; // Number of samples for each EEG LSL push.
        public const int MUSE_ACCELEROMETER_SAMPLE_COUNT = 1; // Number of samples for each accelerometer LSL push.
        public const int MUSE_GYROSCOPE_SAMPLE_COUNT = 1; // Number of samples for each grysocope LSL push.
        public const int MUSE_PPG_SAMPLE_COUNT = 6; // Number of samples for each PPG LSL push.

        public const int MUSE_LSL_BUFFER_LENGTH = 360;

        // GATT service to start and stop streams, reset device, select preset, and get device info.
        public static readonly Guid MUSE_GATT_COMMAND_UUID = new Guid("273e0001-4c4d-454d-96be-f03bac821358");

        public static readonly byte[] MUSE_CMD_TOGGLE_STREAM_START = new byte[3] { 0x02, 0x64, 0x0a };
        public static readonly byte[] MUSE_CMD_TOGGLE_STREAM_STOP = new byte[3] { 0x02, 0x68, 0x0a };
        public static readonly byte[] MUSE_CMD_KEEP_STREAM_ALIVE = new byte[3] { 0x02, 0x6b, 0x0a };

        public static readonly byte[] MUSE_CMD_ASK_RESET = new byte[4] { 0x03, 0x2a, 0x31, 0x0a };
        public static readonly byte[] MUSE_CMD_ASK_DEVICE_INFO = new byte[4] { 0x03, 0x76, 0x31, 0x0a };

        // "Preset" modes.
        // See details on https://goo.gl/FPN1ib
        // For 2016 headband, possible choice are 'p20' and 'p21'.
        // Untested but possible values are 'p22' and 'p23'
        // Default is 'p21'."""
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P20 = new byte[5] { 0x04, 0x70, 0x32, 0x30, 0x0a };
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P21 = new byte[5] { 0x04, 0x70, 0x32, 0x31, 0x0a }; // Default mode.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P22 = new byte[5] { 0x04, 0x70, 0x32, 0x32, 0x0a };
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P23 = new byte[5] { 0x04, 0x70, 0x32, 0x33, 0x0a };

        // Parent service for channel characteristics.
        public static readonly Guid MUSE_GATT_DATA_SERVICE_UUID = new Guid("0000fe8d-0000-1000-8000-00805f9b34fb");

        // GATT characteristics for device battery level and other general info.
        public static readonly Guid MUSE_GATT_TELEMETRY_UUID = new Guid("273e000b-4c4d-454d-96be-f03bac821358");

        // Muse GATT characteristics for the 5 EEG channels, in order: TP9-AF7-AF8-TP10-RIGHTAUX.
        public static readonly Guid[] MUSE_GATT_EGG_CHANNEL_UUIDS = new Guid[MUSE_EEG_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358"), // Handle 40
            new Guid("273e0007-4c4d-454d-96be-f03bac821358") // Handle 43
        };

        // Muse glasses GATT characteristics for the 4 EEG channels, in order: TP9-AF7-AF8-TP10.
        public static readonly Guid[] MUSE_SMXT_GATT_EEG_CHANNEL_UUIDS = new Guid[MUSE_SMXT_EEG_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358") // Handle 40
        };

        // Muse 2 GATT characteristics for the 3 PPG channels.
        public static readonly Guid[] MUSE_2_GATT_PPG_CHANNEL_UUIDS = new Guid[MUSE_2_PPG_CHANNEL_COUNT] {
            new Guid("273e000f-4c4d-454d-96be-f03bac821358"), // PPG1
            new Guid("273e0010-4c4d-454d-96be-f03bac821358"), // PPG2
            new Guid("273e0011-4c4d-454d-96be-f03bac821358"), // PPG3
        };

        // LSL labels for the Accelerometer stream channels.
        public static readonly string[] MUSE_ACCELEROMETER_CHANNEL_LABELS = new string[3] {
            "X",
            "Y",
            "Z"
        };

        // LSL labels for the Gyroscope stream channels.
        public static readonly string[] MUSE_GYROSCOPE_CHANNEL_LABELS = new string[3] {
            "X",
            "Y",
            "Z"
        };

        // LSL labels for the PPG stream channels.
        public static readonly string[] MUSE_PPG_CHANNEL_LABELS = new string[MUSE_2_PPG_CHANNEL_COUNT] {
            "PPG1",
            "PPG2",
            "PPG3"
        };

        // LSL labels for the 5 EEG channels, in specific order to match muse-lsl.py.
        public static readonly string[] MUSE_EEG_CHANNEL_LABELS = new string[MUSE_EEG_CHANNEL_COUNT]
        {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
            "Right AUX"
        };

        // LSL labels for the 4 EEG channels, in specific order to match muse-lsl.py. Note the SMXT does not have AUX channel.
        public static readonly string[] MUSE_SMXT_EEG_CHANNEL_LABELS = new string[MUSE_SMXT_EEG_CHANNEL_COUNT]
        {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
        };

        public const string TIMESTAMP_FORMAT_BLUEMUSE_UNIX = "BLUEMUSE";
        public const string TIMESTAMP_FORMAT_LSL_LOCAL_CLOCK_BLUEMUSE = "LSL_LOCAL_CLOCK_BLUEMUSE";
        public const string TIMESTAMP_FORMAT_LSL_LOCAL_CLOCK_NATIVE = "LSL_LOCAL_CLOCK_NATIVE";
        public const string TIMESTAMP_FORMAT_NONE = "NONE";

        public const string CHANNEL_DATA_TYPE_FLOAT = "FLOAT32";
        public const string CHANNEL_DATA_TYPE_DOUBLE = "DOUBLE64";

        public const string SETTINGS_KEY_TIMESTAMP_FORMAT = "primary_timestamp_format";
        public const string SETTINGS_KEY_TIMESTAMP_FORMAT2 = "secondary_timestamp_format";
        public const string SETTINGS_KEY_CHANNEL_DATA_TYPE = "channel_data_type";
        public const string SETTINGS_KEY_ALWAYS_PAIR = "always_pair";
        public const string SETTINGS_KEY_EEG_ENABLED = "eeg_enabled";
        public const string SETTINGS_KEY_ACCELEROMETER_ENABLED = "accelerometer_enabled";
        public const string SETTINGS_KEY_GYROSCOPE_ENABLED = "gyroscope_enabled";
        public const string SETTINGS_KEY_PPG_ENABLED = "ppg_enabled";

        public const string EEG_STREAM_TYPE = "EEG";
        public const string EEG_UNITS = "microvolts";

        public const string ACCELEROMETER_STREAM_TYPE = "Accelerometer";
        public const string ACCELEROMETER_UNITS = "g";

        public const string GYROSCOPE_STREAM_TYPE = "Gyroscope";
        public const string GYROSCOPE_UNITS = "dps";

        public const string PPG_STREAM_TYPE = "PPG";
        public const string PPG_UNITS = "mmHg";
    }
}
