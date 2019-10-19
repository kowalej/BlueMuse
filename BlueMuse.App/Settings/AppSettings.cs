using BlueMuse.Bluetooth;
using BlueMuse.Helpers;
using BlueMuse.Misc;
using BlueMuse.MuseManagement;
using System;
using System.Linq;

namespace BlueMuse.Settings
{
    public sealed class AppSettings : ObservableObject
    {
        private static readonly Lazy<AppSettings> lazy = new Lazy<AppSettings>(() => new AppSettings());

        public static AppSettings Instance { get { return lazy.Value; } }
        Windows.Storage.ApplicationDataContainer systemAppSettings;

        private AppSettings()
        {
            systemAppSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        }

        public void LoadInitialSettings()
        {
            // Init primary timestamp format, defaults to bluemuse.
            var tf = systemAppSettings.Values[Constants.SETTINGS_KEY_TIMESTAMP_FORMAT] as string;
            var tff = TimestampFormatsContainer.TimestampFormats.FirstOrDefault(x => x.Key == tf);
            // Call public prop to trigger notification.
            TimestampFormat = tff ?? TimestampFormatsContainer.TimestampFormats.First(x => x.Key == Constants.TIMESTAMP_FORMAT_BLUEMUSE_UNIX);

            // Init secondary timestamp format, defaults to none.
            var tf2 = systemAppSettings.Values[Constants.SETTINGS_KEY_TIMESTAMP_FORMAT2] as string;
            var tff2 = TimestampFormatsContainer.TimestampFormats2.FirstOrDefault(x => x.Key == tf2);
            // Call public prop to trigger notification.
            TimestampFormat2 = tff2 ?? TimestampFormatsContainer.TimestampFormats2.First(x => x.Key == Constants.TIMESTAMP_FORMAT_NONE);

            // Init channel data type.
            var cdt = systemAppSettings.Values[Constants.SETTINGS_KEY_CHANNEL_DATA_TYPE] as string;
            var cdtf = ChannelDataTypesContainer.ChannelDataTypes.FirstOrDefault(x => x.Key == cdt);
            // Call public prop to trigger notification.
            ChannelDataType = cdtf ?? ChannelDataTypesContainer.ChannelDataTypes.First(x => x.Key == Constants.CHANNEL_DATA_TYPE_FLOAT);

            // Init always pair.
            var ap = systemAppSettings.Values[Constants.SETTINGS_KEY_ALWAYS_PAIR] as bool?;
            // Call public prop to trigger notification.
            AlwaysPair = ap.HasValue ? ap.Value : false;

            // Assume Muse 2.
            var am2 = systemAppSettings.Values[Constants.SETTINGS_KEY_ASSUME_MUSE_2] as bool?;
            // Call public prop to trigger notification.
            AssumeMuse2 = am2.HasValue ? am2.Value : false;
        }

        public void SetCMDSetting(string key, string value)
        {
            switch (key)
            {
                case Constants.SETTINGS_KEY_TIMESTAMP_FORMAT:
                    var tf = TimestampFormatsContainer.TimestampFormats.FirstOrDefault(x => x.Key.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (tf != null)
                    {
                        TimestampFormat = tf; // Call public prop to trigger notification.
                    }
                    break;

                case Constants.SETTINGS_KEY_TIMESTAMP_FORMAT2:
                    var tf2 = TimestampFormatsContainer.TimestampFormats2.FirstOrDefault(x => x.Key.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (tf2 != null)
                    {
                        TimestampFormat2 = tf2; // Call public prop to trigger notification.
                    }
                    break;

                case Constants.SETTINGS_KEY_CHANNEL_DATA_TYPE:
                    var cdt = ChannelDataTypesContainer.ChannelDataTypes.FirstOrDefault(x => x.Key.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (cdt != null)
                    {
                        ChannelDataType = cdt; // Call public prop to trigger notification.
                    }
                    break;

                case Constants.SETTINGS_KEY_ALWAYS_PAIR:
                    if (value.ToLower() == "true" ? true : value.ToLower() == "false") {
                        AlwaysPair = value.ToLower() == "true" ? true : false;
                    }
                    break;

                case Constants.SETTINGS_KEY_ASSUME_MUSE_2:
                    if (value.ToLower() == "true" ? true : value.ToLower() == "false")
                    {
                        AssumeMuse2 = value.ToLower() == "true" ? true : false;
                    }
                    break;
            }
        }

        private BaseTimestampFormat timestampFormat;
        public BaseTimestampFormat TimestampFormat
        {
            get
            {
                return timestampFormat;
            }
            set
            {
                Muse.TimestampFormat = value;
                systemAppSettings.Values[Constants.SETTINGS_KEY_TIMESTAMP_FORMAT] = value.Key;
                SetProperty(ref timestampFormat, value);
            }
        }
        private BaseTimestampFormat timestampFormat2;
        public BaseTimestampFormat TimestampFormat2
        {
            get
            {
                return timestampFormat2;
            }
            set
            {
                Muse.TimestampFormat2 = value;
                systemAppSettings.Values[Constants.SETTINGS_KEY_TIMESTAMP_FORMAT2] = value.Key;
                SetProperty(ref timestampFormat2, value);
            }
        }

        private ChannelDataType channelDataType;
        public ChannelDataType ChannelDataType
        {
            get
            {
                return channelDataType;
            }
            set
            {
                Muse.ChannelDataType = value;
                systemAppSettings.Values[Constants.SETTINGS_KEY_CHANNEL_DATA_TYPE] = value.Key;
                SetProperty(ref channelDataType, value);
            }
        }

        private bool alwaysPair;
        public bool AlwaysPair
        {
            get
            {
                return alwaysPair;
            }
            set
            {
                BluetoothManager.AlwaysPair = value;
                systemAppSettings.Values[Constants.SETTINGS_KEY_ALWAYS_PAIR] = value;
                SetProperty(ref alwaysPair, value);
            }
        }

        private bool assumeMuse2;
        public bool AssumeMuse2
        {
            get
            {
                return assumeMuse2;
            }
            set
            {
                Muse.AssumeMuse2 = value;
                systemAppSettings.Values[Constants.SETTINGS_KEY_ASSUME_MUSE_2] = value;
                SetProperty(ref assumeMuse2, value);
            }
        }
    }
}
