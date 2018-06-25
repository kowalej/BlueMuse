using System;
using System.Collections.Generic;

namespace BlueMuse.MuseManagement
{
    public class MuseSample
    {
        private DateTimeOffset baseTimeStamp = DateTimeOffset.MaxValue;
        public DateTimeOffset BaseTimeStamp
        {
            get
            {
                return baseTimeStamp;
            }
            set
            {
                // Always set to the earliest timestamp value.
                if(value < baseTimeStamp)
                    baseTimeStamp = value;
            }
        }

        private double[] timestamps;
        public double[] TimeStamps
        {
            get
            {
                double baseMillis = baseTimeStamp.ToUnixTimeMilliseconds();

                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    timestamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                    timestamps[i] = timestamps[i] / 1000d; // Convert to seconds, as this is a more standard Unix epoch timestamp format.
                }
                return timestamps;
            }
        }

        public Dictionary<Guid, float[]> ChannelData { get; set; }

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, float[]>();
            timestamps = new double[Constants.MUSE_SAMPLE_COUNT];
        }
    }
}
