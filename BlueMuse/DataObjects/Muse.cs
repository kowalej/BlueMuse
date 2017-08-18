using BlueMuse.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public DateTime BaseTimeStamp
        {
            get
            {
                return timeStamps[11];
            }
            set
            {
                for (int i = 0; i < 12; i++)
                {
                    timeStamps[i] = value.AddMilliseconds(-(((12 - i) * 4) - 1)); // Adjust time considering each samples is 4ms.
                }
                timeStamps[11] = value;
            }
        }
        private DateTime[] timeStamps;
        public double[] TimeStamps { get { return timeStamps.Select(x => (double)x.Ticks).OrderBy(x => x).ToArray(); } }

        public Dictionary<Guid, float[]> ChannelData;

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, float[]>();
            timeStamps = new DateTime[12];
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
                    lslStream = new LSL.liblsl.StreamOutlet(lslStreamInfo, 12, 360);
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
                    channelEventHandlers = new TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>[5];
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
            lslStreamInfo = new LSL.liblsl.StreamInfo(string.Format("{0} ({1})", name, MacAddress), "EEG", 5, 256, LSL.liblsl.channel_format_t.cf_float32, Package.Current.DisplayName);
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
