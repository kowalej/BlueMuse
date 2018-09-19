using System;
using System.Collections.Generic;

namespace LSLBridge.LSL
{
    public class LSLChannelInfo
    {
        public string Label { get; set; }
        public string Unit { get; set; }
        public string Type { get; set; }
    }

    public class LSLStreamInfo
    {
        public string StreamName { get; set; }
        public string StreamType { get; set; }
        public string DeviceName { get; set; }
        public string DeviceManufacturer { get; set; }
        public double NominalSRate { get; set; }
        public Type ChannelDataType { get; set; }
        public int ChannelCount { get; set; }
        public int ChunkSize { get; set; }
        public int BufferLength { get; set; }
        public List<LSLChannelInfo> Channels { get; set; }
        public bool SendSecondaryTimestamp { get; set; }
    }
}
