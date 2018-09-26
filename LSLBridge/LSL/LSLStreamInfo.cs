using System;
using System.Collections.Generic;

namespace LSLBridge.LSL
{
    public class LSLBridgeChannelInfo
    {
        public string Label { get; set; }
        public string Unit { get; set; }
        public string Type { get; set; }
    }

    public enum LSLBridgeDataType
    {
        FLOAT = 0,
        DOUBLE = 1,
        INT = 2,
        STRING = 3
    }

    public class LSLBridgeStreamInfo
    {
        public string StreamName { get; set; }
        public string StreamType { get; set; }
        public string DeviceName { get; set; }
        public string DeviceManufacturer { get; set; }
        public double NominalSRate { get; set; }
        public LSLBridgeDataType ChannelDataType { get; set; }
        public int ChannelCount { get; set; }
        public int ChunkSize { get; set; }
        public int BufferLength { get; set; }
        public List<LSLBridgeChannelInfo> Channels { get; set; }
        public bool SendSecondaryTimestamp { get; set; }
    }
}
