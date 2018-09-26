using LSLBridge.LSL;
using System.Collections.Generic;

namespace BlueMuse.Misc
{
    public class ChannelDataType
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public LSLBridgeDataType DataType { get; set; }
    }

    public static class ChannelDataTypesContainer
    {
        public static List<ChannelDataType> ChannelDataTypes = new List<ChannelDataType>()
        {
            new ChannelDataType() {
                Key = Constants.CHANNEL_DATA_TYPE_FLOAT,
                DisplayName = "Single Precision (float32)",
                DataType = LSLBridgeDataType.FLOAT
            },
            new ChannelDataType()
            {
                Key = Constants.CHANNEL_DATA_TYPE_DOUBLE,
                DisplayName = "Double Precision (double64)",
                DataType = LSLBridgeDataType.DOUBLE
            }
        };
    }

}
