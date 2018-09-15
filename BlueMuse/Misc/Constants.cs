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

        public static readonly string ALL_AQS = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\"";
        public static readonly string MUSE_AQS = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\" AND (System.ItemNameDisplay:~~\"Muse\" OR System.ItemNameDisplay:~~\"SMTX\")"; // Needs testing.

        public const string ARGS_STREAMFIRST = "streamfirst";
        public const string ARGS_ADDRESSES = "addresses";
        public const string ARGS_STARTALL = "startall";
        public const string ARGS_STOPALL = "stopall";
        public const string CMD_START = "start";
        public const string CMD_STOP = "stop";
        public const string CMD_CLOSE_PROGRAM = "shutdown";

        public const int MUSE_SAMPLE_RATE = 256;
        public const float MUSE_SAMPLE_TIME_MILLIS = 1000f / MUSE_SAMPLE_RATE;

        public const int MUSE_CHANNEL_COUNT = 5;
        public const int MUSE_SMXT_CHANNEL_COUNT = 4;

        public static string MUSE_DEVICE_NAME = "Muse EEG Headset";
        public static string MUSE_MANUFACTURER = "Interaxon";

        public static string MUSE_SMXT_DEVICE_NAME = "Smith Lowdown Focus";
        public static string MUSE_SMXT_MANUFACTURER = "Smith";

        public const int MUSE_SAMPLE_COUNT = 12;
        public const int MUSE_LSL_BUFFER_LENGTH = 360;

        public const string LSL_MESSAGE_TYPE = nameof(LSL_MESSAGE_TYPE);
        public const string LSL_MESSAGE_TYPE_KEEP_ACTIVE = nameof(LSL_MESSAGE_TYPE_KEEP_ACTIVE);
        public const string LSL_MESSAGE_TYPE_OPEN_STREAM = nameof(LSL_MESSAGE_TYPE_OPEN_STREAM);
        public const string LSL_MESSAGE_TYPE_CLOSE_STREAM = nameof(LSL_MESSAGE_TYPE_CLOSE_STREAM);
        public const string LSL_MESSAGE_TYPE_SEND_CHUNK = nameof(LSL_MESSAGE_TYPE_SEND_CHUNK);
        public const string LSL_MESSAGE_TYPE_CLOSE_BRIDGE = nameof(LSL_MESSAGE_TYPE_CLOSE_BRIDGE);
        public const string LSL_MESSAGE_DEVICE_NAME = nameof(LSL_MESSAGE_DEVICE_NAME);
        public const string LSL_MESSAGE_SEND_SECONDARY_TIMESTAMP = nameof(LSL_MESSAGE_SEND_SECONDARY_TIMESTAMP);
        public const string LSL_MESSAGE_CHUNK_DATA = nameof(LSL_MESSAGE_CHUNK_DATA);
        public const string LSL_MESSAGE_CHUNK_TIMESTAMPS = nameof(LSL_MESSAGE_CHUNK_TIMESTAMPS);
        public const string LSL_MESSAGE_CHUNK_TIMESTAMPS2 = nameof(LSL_MESSAGE_CHUNK_TIMESTAMPS2);

        // GAAT service to start and stop streaming.
        public static readonly Guid MUSE_TOGGLE_STREAM_UUID = new Guid("273e0001-4c4d-454d-96be-f03bac821358");
        public static readonly byte[] MUSE_TOGGLE_STREAM_START = new byte[3] { 0x02, 0x64, 0x0a };
        public static readonly byte[] MUSE_TOGGLE_STREAM_STOP = new byte[3] { 0x02, 0x68, 0x0a };

        // Parent service for channel characteristics.
        public static readonly Guid MUSE_DATA_SERVICE_UUID = new Guid("0000fe8d-0000-1000-8000-00805f9b34fb");

        // GAAT characteristics for the 5 channels, in order: TP9-AF7-AF8-TP10-RIGHTAUX.
        public static Guid[] MUSE_CHANNEL_UUIDS = new Guid[MUSE_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358"), // Handle 40
            new Guid("273e0007-4c4d-454d-96be-f03bac821358") // Handle 43
        };

        // GAAT characteristics for the 4 channels, in order: TP9-AF7-AF8-TP10.
        public static Guid[] MUSE_SMXT_CHANNEL_UUIDS = new Guid[MUSE_SMXT_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358") // Handle 40
        };

        // LSL labels for the 5 channels, in specific order to match muse-lsl.py.
        public static string[] MUSE_CHANNEL_LABELS = new string[MUSE_CHANNEL_COUNT]
        {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
            "Right AUX"
        };

        // LSL labels for the 4 channels, in specific order to match muse-lsl.py.
        public static string[] MUSE_SMXT_CHANNEL_LABELS = new string[MUSE_SMXT_CHANNEL_COUNT]
        {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
        };
    }
}
