using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueMuse
{
    static class Constants
    {
        // Device name filter will pickup glasses, Muse original, and Muse 2. Names with be something like SMTX-xxxx or Muse-xxxx.
        public static readonly List<string> DeviceNameFilter = new List<string>()
        {
            "Muse", "SMXT"
        };

        // Query string for filtering system devices. We could probably filter in a better way, but this, along with the name match, works for now.
        public const string DEVICE_AQS = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\"";

        // BlueMuse command arguments.
        public const string ARGS_STREAMFIRST = "streamfirst";
        public const string ARGS_ADDRESSES = "addresses";
        public const string ARGS_STARTALL = "startall";
        public const string ARGS_STOPALL = "stopall";
        public const string ARGS_SETTING_KEY = "key";
        public const string ARGS_SETTING_VALUE = "value";

        // BlueMuse commands.
        public const string CMD_START = "start";
        public const string CMD_STOP = "stop";
        public const string CMD_FORCE_REFRESH = "refresh";
        public const string CMD_CLOSE_PROGRAM = "shutdown";
        public const string CMD_SET_SETTING = "setting";

        // Streaming rates.
        public const int MUSE_EEG_SAMPLE_RATE = 256; // 256 hz.
        public const float MUSE_EEG_SAMPLE_TIME_MILLIS = 1000f / MUSE_EEG_SAMPLE_RATE;

        public const int MUSE_ACCELEROMETER_SAMPLE_RATE = 52; // 52 hz.
        public const float MUSE_ACCELEROMETER_SAMPLE_TIME_MILLIS = 1000f / MUSE_ACCELEROMETER_SAMPLE_RATE; // Theoretical time between consecutive Accelerometer samples.

        public const int MUSE_GYROSCOPE_SAMPLE_RATE = 52; // 52 hz.
        public const float MUSE_GYROSCOPE_SAMPLE_TIME_MILLIS = 1000f / MUSE_GYROSCOPE_SAMPLE_RATE; // Theoretical time between consecutive Gyroscope samples.

        public const int MUSE_PPG_SAMPLE_RATE = 64; // 64 hz.
        public const float MUSE_PPG_SAMPLE_TIME_MILLIS = 1000f / MUSE_PPG_SAMPLE_RATE; // Theoretical time between consecutive PPG samples.

        public const float MUSE_TELEMETRY_SAMPLE_RATE = 0.1f; // 0.1 hz (once every 10 seconds).
        public const float MUSE_TELEMETRY_SAMPLE_TIME_MILLIS = 1000f / MUSE_TELEMETRY_SAMPLE_RATE; // Theoretical time between consecutive telemetry samples.

        // Channel counts.
        public const int MUSE_EEG_CHANNEL_COUNT = 5;
        public const int MUSE_EEG_NOAUX_CHANNEL_COUNT = 4;
        public const int MUSE_EEG_NOAUX_TIMEOUT_THRESHOLD_MILLIS = 2000;
        public const int MUSE_EEG_FLUSH_THRESHOLD_MILLIS = 500;
        public const int MUSE_ACCELEROMETER_CHANNEL_COUNT = 3;
        public const int MUSE_GYROSCOPE_CHANNEL_COUNT = 3;
        public const int MUSE_PPG_CHANNEL_COUNT = 3;
        public const int MUSE_TELEMETRY_CHANNEL_COUNT = 4;

        // Names and manufacturers.
        public const string MUSE_DEVICE_NAME = "Muse EEG Headset";
        public const string MUSE_MANUFACTURER = "Interaxon";

        public const string MUSE_2_DEVICE_NAME = "Muse 2 EEG Headset";

        public const string MUSE_S_DEVICE_NAME = "Muse S EEG Headset";
        public static readonly Guid MUSE_S_SPECIAL_CHANNEL = new Guid("00002902-0000-1000-8000-00805f9b34fb");

        public const string MUSE_S_ATHENA_DEVICE_NAME = "Muse S Athena EEG Headset";

        public const string MUSE_SMXT_DEVICE_NAME = "Smith Lowdown Focus";
        public const string MUSE_SMXT_MANUFACTURER = "Smith";

        // Scale factors.
        public const double MUSE_ACCELEROMETER_SCALE_FACTOR = 0.0000610352d;
        public const double MUSE_GYROSCOPE_SCALE_FACTOR = 0.0074768d;

        // Sample sizes (chunk size) for LSL.
        public const int MUSE_EEG_SAMPLE_COUNT = 12; // Number of samples for each EEG LSL push.
        public const int MUSE_ACCELEROMETER_SAMPLE_COUNT = 3; // Number of samples for each accelerometer LSL push.
        public const int MUSE_GYROSCOPE_SAMPLE_COUNT = 3; // Number of samples for each grysocope LSL push.
        public const int MUSE_PPG_SAMPLE_COUNT = 6; // Number of samples for each PPG LSL push.
        public const int MUSE_TELEMETRY_SAMPLE_COUNT = 1; // Number of samples for each telemetry LSL push.

        public const int MUSE_LSL_BUFFER_LENGTH = 360;

        // GATT service to start and stop streams, reset device, select preset, and get device info.
        public static readonly Guid MUSE_GATT_COMMAND_UUID = new Guid("273e0001-4c4d-454d-96be-f03bac821358");

        public static readonly byte[] MUSE_CMD_TOGGLE_STREAM_START = new byte[3] { 0x02, 0x64, 0x0a };
        public static readonly byte[] MUSE_CMD_TOGGLE_STREAM_STOP = new byte[3] { 0x02, 0x68, 0x0a };
        public static readonly byte[] MUSE_CMD_KEEP_STREAM_ALIVE = new byte[3] { 0x02, 0x6b, 0x0a };

        public static readonly byte[] MUSE_CMD_ASK_RESET = new byte[4] { 0x03, 0x2a, 0x31, 0x0a };
        public static readonly byte[] MUSE_CMD_ASK_DEVICE_INFO = new byte[4] { 0x03, 0x76, 0x31, 0x0a };
        public static readonly byte[] MUSE_CMD_ASK_CONTROL_STATUS = new byte[3] { 0x02, 0x73, 0x0a };

        // "Preset" modes.
        // See details on https://goo.gl/FPN1ib
        // For 2016 headband, possible choice are 'p20' and 'p21'.
        // Untested but possible values are 'p22' and 'p23'
        // Default is 'p21 (for Muse 2016)'."""

        // 5-Channel EEG channel streaming.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P20 = new byte[5] { 0x04, 0x70, 0x32, 0x30, 0x0a };

        // 4-Channel EEG channel streaming.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P21 = new byte[5] { 0x04, 0x70, 0x32, 0x31, 0x0a }; // Default mode.

        // 4-Channel EEG channel streaming without accel/gyro.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P22 = new byte[5] { 0x04, 0x70, 0x32, 0x32, 0x0a };

        // Unkown.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P23 = new byte[5] { 0x04, 0x70, 0x32, 0x33, 0x0a };

        // 5-Channel EEG + PPG.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P50 = new byte[5] { 0x04, 0x70, 0x35, 0x30, 0x0a };

        // 4-Channel EEG + PPG.
        public static readonly byte[] MUSE_CMD_PRESET_MODE_P51 = new byte[5] { 0x04, 0x70, 0x35, 0x31, 0x0a };

        // Parent service for channel characteristics.
        public static readonly Guid MUSE_GATT_DATA_SERVICE_UUID = new Guid("0000fe8d-0000-1000-8000-00805f9b34fb");

        // GATT characteristics for device battery level and other general info.
        public static readonly Guid MUSE_GATT_TELEMETRY_UUID = new Guid("273e000b-4c4d-454d-96be-f03bac821358"); // Handle 25.

        // Muse GATT characteristics for the 5 EEG channels, in order: TP9-AF7-AF8-TP10-RIGHTAUX.
        public static readonly Guid[] MUSE_GATT_EGG_CHANNEL_UUIDS = new Guid[MUSE_EEG_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358"), // Handle 40
            new Guid("273e0007-4c4d-454d-96be-f03bac821358") // Handle 43
        };

        public static readonly Guid MUSE_GATT_AUX_CHANNEL_UUID = new Guid("273e0007-4c4d-454d-96be-f03bac821358"); // Handle 43.

        // Muse no AUX GATT characteristics for the 4 EEG channels, in order: TP9-AF7-AF8-TP10. Applies to SMXT and Muse 2.
        public static readonly Guid[] MUSE_GATT_EGG_NOAUX_CHANNEL_UUIDS = new Guid[MUSE_EEG_NOAUX_CHANNEL_COUNT] {
            new Guid("273e0003-4c4d-454d-96be-f03bac821358"), // Handle 31
            new Guid("273e0004-4c4d-454d-96be-f03bac821358"), // Handle 34
            new Guid("273e0005-4c4d-454d-96be-f03bac821358"), // Handle 37
            new Guid("273e0006-4c4d-454d-96be-f03bac821358") // Handle 40
        };

        // Accelerometer GATT characteristic.
        public static Guid MUSE_GATT_ACCELEROMETER_UUID = new Guid("273e000a-4c4d-454d-96be-f03bac821358"); // Handle 22.

        // Gyroscope GATT characteristic.
        public static Guid MUSE_GATT_GYROSCOPE_UUID = new Guid("273e0009-4c4d-454d-96be-f03bac821358"); // Handle 19.

        // Muse (2) GATT characteristics for the 3 PPG channels.
        public static readonly Guid[] MUSE_GATT_PPG_CHANNEL_UUIDS = new Guid[MUSE_PPG_CHANNEL_COUNT] {
            new Guid("273e000f-4c4d-454d-96be-f03bac821358"), // PPG1
            new Guid("273e0010-4c4d-454d-96be-f03bac821358"), // PPG2
            new Guid("273e0011-4c4d-454d-96be-f03bac821358"), // PPG3
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

        // Muse no AUX GATT characteristics for the 4 EEG channels, in order: TP9-AF7-AF8-TP10. Applies to SMXT and Muse 2.
        public static readonly string[] MUSE_EEG_NOAUX_CHANNEL_LABELS = new string[MUSE_EEG_NOAUX_CHANNEL_COUNT] {
            "TP9",
            "AF7",
            "AF8",
            "TP10",
        };

        // LSL labels for the Accelerometer stream channels.
        public static readonly string[] MUSE_ACCELEROMETER_CHANNEL_LABELS = new string[MUSE_ACCELEROMETER_CHANNEL_COUNT] {
            "X",
            "Y",
            "Z"
        };

        // LSL labels for the Gyroscope stream channels.
        public static readonly string[] MUSE_GYROSCOPE_CHANNEL_LABELS = new string[MUSE_GYROSCOPE_CHANNEL_COUNT] {
            "X",
            "Y",
            "Z"
        };

        // LSL labels for the PPG stream channels.
        public static readonly string[] MUSE_PPG_CHANNEL_LABELS = new string[MUSE_PPG_CHANNEL_COUNT] {
            "PPG1",
            "PPG2",
            "PPG3"
        };

        // LSL labels for the Telemetry stream channels.
        public static readonly string[] MUSE_TELEMETRY_CHANNEL_LABELS = new string[MUSE_TELEMETRY_CHANNEL_COUNT] {
            "Battery",
            "Unknown (fuel?)",
            "ADC Voltage",
            "Temperature"
        };

        // ---------------------------------------------------------------------
        // Muse S Athena.
        //
        // Athena keeps the same service and command characteristic as the older
        // headbands, but drops the per-channel data characteristics in favour of two
        // notify characteristics carrying tagged, multiplexed packets. It also takes
        // ASCII commands instead of the fixed byte blobs above, and packs its bit
        // fields LSB-first rather than MSB-first.
        // ---------------------------------------------------------------------

        // DATA_1 is also our discriminator - no other Muse exposes it.
        public static readonly Guid MUSE_GATT_ATHENA_DATA1_UUID = new Guid("273e0013-4c4d-454d-96be-f03bac821358");
        public static readonly Guid MUSE_GATT_ATHENA_DATA2_UUID = new Guid("273e0014-4c4d-454d-96be-f03bac821358");

        // Athena reports device info under "v6" rather than the legacy "v1".
        public static readonly byte[] MUSE_CMD_ASK_DEVICE_INFO_ATHENA = new byte[4] { 0x03, 0x76, 0x36, 0x0a };

        // Preset selects which sensor tags the device emits. p1041 gives EEG + IMU +
        // optics + battery. Other documented values: p20, p21, p50, p51, p60, p61,
        // p1034, p1035, p1042, p1043, p1044, p1045, p1046, p4129.
        public const string MUSE_ATHENA_DEFAULT_PRESET = "p1041";

        // Handshake timing. The device drops commands sent faster than this.
        public const int MUSE_ATHENA_INIT_COMMAND_INTERVAL_MILLIS = 200;
        public const int MUSE_ATHENA_START_REPEAT_DELAY_MILLIS = 50;
        public const int MUSE_ATHENA_START_LOW_LATENCY_DELAY_MILLIS = 100;
        public const int MUSE_ATHENA_START_STATUS_DELAY_MILLIS = 300;
        public const int MUSE_ATHENA_START_TRAILING_DELAY_MILLIS = 200;

        // EEG: tag 0x11, 4 channels x 4 samples per packet (tag 0x12 carries 8 channels
        // x 2 samples, of which the first 4 are the headband electrodes), 14 bit
        // unsigned over a 1450 uV full scale. Samples are centered on the midpoint,
        // which is subtracted to get signed microvolts, as the legacy 12 bit samples
        // subtract 2048.
        public const int MUSE_ATHENA_EEG_CHANNEL_COUNT = 4;
        public const int MUSE_ATHENA_EEG_SAMPLE_COUNT = 4;
        public const double MUSE_ATHENA_EEG_SCALE_FACTOR = 1450d / 16383d;
        public const int MUSE_ATHENA_EEG_MIDPOINT = 1 << 13;

        // ACC + GYRO: tag 0x47, one packet carries both as 6 channels x 3 samples,
        // fanned out to the two existing LSL streams. Accelerometer scale matches the
        // legacy headbands; gyroscope scale is negated relative to them.
        public const int MUSE_ATHENA_IMU_CHANNEL_COUNT = 6;
        public const int MUSE_ATHENA_IMU_SAMPLE_COUNT = 3;
        public const double MUSE_ATHENA_GYROSCOPE_SCALE_FACTOR = -0.0074768d;

        // Optics (fNIRS): tags 0x34 / 0x35 / 0x36 carry 4, 8 or 16 of the same 16
        // canonical channels as raw 20 bit counts. Published on the PPG stream, which
        // is the closest existing equivalent.
        public const int MUSE_ATHENA_OPTICS_CHANNEL_COUNT = 16;
        public const int MUSE_ATHENA_OPTICS_SAMPLE_COUNT = 1;
        public const int MUSE_ATHENA_OPTICS_SAMPLE_RATE = 64; // 64 hz.
        public const string MUSE_ATHENA_OPTICS_UNITS = "counts";
        public static readonly string[] MUSE_ATHENA_OPTICS_CHANNEL_LABELS =
            Enumerable.Range(0, MUSE_ATHENA_OPTICS_CHANNEL_COUNT).Select(i => $"OPTICS{i}").ToArray();

        // Telemetry: tags 0x88 / 0x98 carry battery percent only, not the legacy
        // four channel battery / fuel / voltage / temperature block. The raw uint16 is
        // in 1/512ths of a percent.
        public const int MUSE_ATHENA_TELEMETRY_CHANNEL_COUNT = 1;
        public const int MUSE_ATHENA_TELEMETRY_SAMPLE_COUNT = 1;
        public const float MUSE_ATHENA_TELEMETRY_SAMPLE_RATE = 1.0f; // 1 hz.
        public const double MUSE_ATHENA_BATTERY_SCALE_FACTOR = 1d / 512d;
        public const string MUSE_ATHENA_TELEMETRY_UNITS = "percent";
        public static readonly string[] MUSE_ATHENA_TELEMETRY_CHANNEL_LABELS = new string[MUSE_ATHENA_TELEMETRY_CHANNEL_COUNT] {
            "Battery"
        };

        public const int MUSE_DEVICE_INFO_CONTROL_REFRESH_MS = 4000;

        public static readonly double[] MUSE_EEG_BACKFILL = Enumerable.Repeat(double.NaN, MUSE_EEG_SAMPLE_COUNT).ToArray();

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
        public const string SETTINGS_KEY_TELEMETRY_ENABLED = "telemetry_enabled";
        public const string SETTINGS_KEY_WINDOW_WIDTH = "window_width";
        public const string SETTINGS_KEY_WINDOW_HEIGHT = "window_height";

        public const string EEG_STREAM_TYPE = "EEG";
        public const string EEG_UNITS = "microvolts";

        public const string ACCELEROMETER_STREAM_TYPE = "Accelerometer";
        public const string ACCELEROMETER_UNITS = "g";

        public const string GYROSCOPE_STREAM_TYPE = "Gyroscope";
        public const string GYROSCOPE_UNITS = "dps";

        public const string PPG_STREAM_TYPE = "PPG";
        public const string PPG_UNITS = "mmHg";

        public const string TELEMETRY_STREAM_TYPE = "Telemetry";
    }
}