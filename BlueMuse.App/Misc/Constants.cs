using System;
using System.Collections.Generic;

namespace BlueMuse
{
    static class Constants
    {
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

        public const int MUSE_SAMPLE_RATE = 256;
        public const float MUSE_SAMPLE_TIME_MILLIS = 1000f / MUSE_SAMPLE_RATE;

        public const int MUSE_CHANNEL_COUNT = 5;
        public const int MUSE_SMXT_CHANNEL_COUNT = 4;

        public const string MUSE_DEVICE_NAME = "Muse EEG Headset";
        public const string MUSE_MANUFACTURER = "Interaxon";

        public const string MUSE_SMXT_DEVICE_NAME = "Smith Lowdown Focus";
        public const string MUSE_SMXT_MANUFACTURER = "Smith";

        public const int MUSE_SAMPLE_COUNT = 12;
        public const int MUSE_LSL_BUFFER_LENGTH = 360;

        // GATT service to start and stop streaming.
        public static readonly Guid MUSE_TOGGLE_STREAM_UUID = new Guid("273e0001-4c4d-454d-96be-f03bac821358");
        public static readonly byte[] MUSE_TOGGLE_STREAM_START = new byte[3] { 0x02, 0x64, 0x0a };
        public static readonly byte[] MUSE_TOGGLE_STREAM_STOP = new byte[3] { 0x02, 0x68, 0x0a };

        // Parent service for channel characteristics.
        public static readonly Guid MUSE_DATA_SERVICE_UUID = new Guid("0000fe8d-0000-1000-8000-00805f9b34fb");

        // GATT characteristics for device battery level and other general info.
        public static readonly Guid MUSE_BATTERY_UUID = new Guid("273e000b-4c4d-454d-96be-f03bac821358");

        // GATT characteristics for the device position data, in order: Gyroscope, Accelerometer
        public static readonly Guid[] MUSE_POSITIONAL_DATA_CHANNEL_UUIDS = new Guid[2] {
            new Guid("273e0009-4c4d-454d-96be-f03bac821358"), // Gyroscope
            new Guid("273e000a-4c4d-454d-96be-f03bac821358") // Accelerometer
        };

        // GATT characteristics for the 5 EEG channels, in order: TP9-AF7-AF8-TP10-RIGHTAUX.
        public static readonly Guid[] MUSE_EGG_CHANNEL_UUIDS = new Guid[MUSE_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358"), // Handle 40
            new Guid("273e0007-4c4d-454d-96be-f03bac821358") // Handle 43
        };

        // GATT characteristics for the 4 EEG channels, in order: TP9-AF7-AF8-TP10.
        public static readonly Guid[] MUSE_SMXT_EEG_CHANNEL_UUIDS = new Guid[MUSE_SMXT_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358") // Handle 40
        };

        // LSL Labels for device position data, in order: Gyroscope, Accelerometer
        public static readonly string[] MUSE_POSITIONAL_DATA_CHANNEL_LABELS= new string[2] {
            "Gyroscope",
            "Accelerometer"
        };

        // LSL labels for the 5 EEG channels, in specific order to match muse-lsl.py.
        public static readonly string[] MUSE_EEG_CHANNEL_LABELS = new string[MUSE_CHANNEL_COUNT]
        {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
            "Right AUX"
        };

        // LSL labels for the 4 EEG channels, in specific order to match muse-lsl.py.
        public static readonly string[] MUSE_SMXT_EEG_CHANNEL_LABELS = new string[MUSE_SMXT_CHANNEL_COUNT]
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

        public const string EEG_STREAM_TYPE = "EEG";
        public const string EEG_UNITS = "microvolts";
    }
}
