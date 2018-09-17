using System;
using System.Collections.Generic;

namespace BlueMuse.MuseManagement
{
    public class MuseSample
    {
        private double baseTimestamp = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
        public double BaseTimestamp
        {
            get
            {
                return baseTimestamp;
            }
            set
            {
                // Always set to the earliest timestamp value.
                if(value < baseTimestamp)
                    baseTimestamp = value;
            }
        }

        private double baseTimestamp2 = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
        public double BasetimeStamp2
        {
            get
            {
                return baseTimestamp2;
            }
            set
            {
                // Always set to the earliest timestamp value.
                if (value < baseTimestamp2)
                    baseTimestamp2 = value;
            }
        }

        private double[] timestamps;
        public double[] Timestamps
        {
            get
            {
                double baseMillis = baseTimestamp;

                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    timestamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                    timestamps[i] = timestamps[i] / 1000d; // Convert to seconds, as this is a more standard Unix epoch timestamp format.
                }
                return timestamps;
            }
        }

        private double[] timestamps2;
        public double[] Timestamps2
        {
            get
            {
                double baseMillis = baseTimestamp2;

                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    timestamps2[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                    timestamps2[i] = timestamps2[i] / 1000d; // Convert to seconds, as this is a more standard Unix epoch timestamp format.
                }
                return timestamps2;
            }
        }

        public Dictionary<Guid, double[]> ChannelData { get; set; }

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, double[]>();
            timestamps = new double[Constants.MUSE_SAMPLE_COUNT];
            timestamps2 = new double[Constants.MUSE_SAMPLE_COUNT];
        }
    }
}
