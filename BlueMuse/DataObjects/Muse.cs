using BlueMuse.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;

namespace BlueMuse.DataObjects
{
    public enum MuseConnectionStatus
    {
        Online = 0,
        Offline = 1
    }

    public class MuseSample
    {
        private DateTimeOffset baseTimeStamp;
        public DateTimeOffset BaseTimeStamp
        {
            get
            {
                return baseTimeStamp;
            }
            set
            {
                baseTimeStamp = value;
                double baseMillis = baseTimeStamp.DateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    timeStamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                }
            }
        }

        private double[] timeStamps;
        public double[] TimeStamps { get { return timeStamps; } }

        public Dictionary<Guid, float[]> ChannelData;

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, float[]>();
            timeStamps = new double[Constants.MUSE_SAMPLE_COUNT];
        }
    }

    public class Muse : ObservableObject
    {
        private LSL.liblsl.StreamInfo lslStreamInfo;
        public LSL.liblsl.StreamInfo LSLStreamInfo { get { return lslStreamInfo; } }
        private LSL.liblsl.StreamOutlet lslStream;
        public LSL.liblsl.StreamOutlet LSLStream
        {
            get
            {
                if (lslStream == null)
                    lslStream = new LSL.liblsl.StreamOutlet(lslStreamInfo, Constants.MUSE_SAMPLE_COUNT, 360);
                var xml = lslStream.info().as_xml();
                return lslStream;
            }
        }
        public BluetoothLEDevice Device { get; set; }
        public GattDeviceService DeviceService { get; set; }
        public Dictionary<UInt16, MuseSample> SampleBuffer { get; set; }

        private TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>[] channelEventHandlers;
        public TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>[] ChannelEventHandlers
        {
            get
            {
                if (channelEventHandlers == null)
                    channelEventHandlers = new TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>[Constants.MUSE_CHANNEL_COUNT];
                return channelEventHandlers;
            }
            set { channelEventHandlers = value; }
        }

        private string name;
        public string Name { get { return name; } set { SetProperty(ref name, value); OnPropertyChanged(nameof(LongName)); } }

        private string id;
        public string Id { get { return id; } set { SetProperty(ref id, value); OnPropertyChanged(nameof(MacAddress)); OnPropertyChanged(nameof(LongName)); } }

        private MuseConnectionStatus status;
        public MuseConnectionStatus Status
        {
            get { return status; }
            set
            {
                SetProperty(ref status, value);
                OnPropertyChanged(nameof(CanStream));
                if (value == MuseConnectionStatus.Offline && isStreaming == true)
                {
                    IsStreaming = false;
                }
            }
        }

        private bool isStreaming;
        public bool IsStreaming { get { return isStreaming; } set { SetProperty(ref isStreaming, value); } }

        private bool isSelected;
        public bool IsSelected { get { return isSelected; } set { SetProperty(ref isSelected, value); } }

        private int streamingPort;
        public int StreamingPort { get { return streamingPort; } set { SetProperty(ref streamingPort, value); } }

        public bool CanStream { get { return status == MuseConnectionStatus.Online; } }
        public string LongName { get { return string.Format("{0} ({1})", Name, MacAddress); } }
        public string MacAddress
        {
            get
            {
                Regex deviceIdRegex = new Regex(@"^*(\w{2}:){5}\w{2}");
                string museId = Id;
                Match matches = deviceIdRegex.Match(museId);
                if (matches.Success)
                    museId = matches.Value;
                return museId;
            }
        }

        public Muse(BluetoothLEDevice device, string name, string id, MuseConnectionStatus status)
        {
            Device = device;
            Name = name;
            Id = id;
            Status = status;
            lslStreamInfo = new LSL.liblsl.StreamInfo(string.Format("{0} ({1})", name, MacAddress), "EEG", Constants.MUSE_CHANNEL_COUNT, Constants.MUSE_SAMPLE_RATE, LSL.liblsl.channel_format_t.cf_float32, Package.Current.DisplayName);
            lslStreamInfo.desc().append_child_value("manufacturer", "Muse");
            lslStreamInfo.desc().append_child_value("manufacturer", "Muse");
            lslStreamInfo.desc().append_child_value("type", "EEG");
            var channels = lslStreamInfo.desc().append_child("channels");
            foreach (var c in Constants.MUSE_CHANNEL_LABELS)
            {
                channels.append_child("channel")
                .append_child_value("label", c)
                .append_child_value("unit", "microvolts")
                .append_child_value("type", "EEG");
            }
        }
    }
}
