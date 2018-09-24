using BlueMuse.Helpers;
using BlueMuse.Misc;
using BlueMuse.MuseManagement;
using System;
using System.Linq;

namespace BlueMuse.Settings
{
    public sealed class AppSettings : ObservableObject
    {

        private static readonly Lazy<AppSettings> lazy =
        new Lazy<AppSettings>(() => new AppSettings());

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
            TimestampFormat = tff ?? TimestampFormatsContainer.TimestampFormats.First(x => x.Key == Constants.TIMESTAMP_FORMAT_BLUEMUSE_UNIX);

            // Init secondary timestamp format, defaults to none.
            var tf2 = systemAppSettings.Values[Constants.SETTINGS_KEY_TIMESTAMP_FORMAT2] as string;
            var tff2 = TimestampFormatsContainer.TimestampFormats2.FirstOrDefault(x => x.Key == tf2);
            TimestampFormat2 = tff2 ?? TimestampFormatsContainer.TimestampFormats2.First(x => x.Key == Constants.TIMESTAMP_FORMAT_NONE);
        }

        public void SetCMDSetting(string key, string value)
        {
            switch (key)
            {
                case Constants.SETTINGS_KEY_TIMESTAMP_FORMAT:
                    var tf = TimestampFormatsContainer.TimestampFormats.FirstOrDefault(x => x.Key.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (tf != null)
                    {
                        TimestampFormat = tf;
                    }
                    break;

                case Constants.SETTINGS_KEY_TIMESTAMP_FORMAT2:
                    var tf2 = TimestampFormatsContainer.TimestampFormats2.FirstOrDefault(x => x.Key.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (tf2 != null)
                    {
                        TimestampFormat2 = tf2;
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
    }
}
