using System.Collections.Generic;

namespace BlueMuse.LSL
{
    public class BlueMuseLSLChannelInfo
    {
        public string Label { get; set; }
        public string Unit { get; set; }
        public string Type { get; set; }
    }

    public enum BlueMuseLSLDataType
    {
        FLOAT = 0,
        DOUBLE = 1,
        INT = 2,
        STRING = 3
    }

    public class BlueMuseLSLStreamInfo
    {
        public string StreamName { get; set; }
        public string StreamType { get; set; }
        public string DeviceName { get; set; }
        public string DeviceManufacturer { get; set; }
        public double NominalSRate { get; set; }
        public BlueMuseLSLDataType ChannelDataType { get; set; }
        public int ChannelCount { get; set; }
        public int ChunkSize { get; set; }
        public int BufferLength { get; set; }
        public List<BlueMuseLSLChannelInfo> Channels { get; set; }
        public bool SendSecondaryTimestamp { get; set; }
    }
}
