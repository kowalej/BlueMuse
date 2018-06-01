using System;
using System.Collections.Generic;

namespace BlueMuse.MuseManagement
{
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
                double baseMillis = baseTimeStamp.ToUnixTimeMilliseconds();
                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    TimeStamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
                    TimeStamps[i] = TimeStamps[i] / 1000d; // Convert to seconds, as this is a more standard Unix epoch timestamp format.
                }
            }
        }

        public double[] TimeStamps;
        public Dictionary<Guid, float[]> ChannelData;

        public MuseSample()
        {
            ChannelData = new Dictionary<Guid, float[]>();
            TimeStamps = new double[Constants.MUSE_SAMPLE_COUNT];
        }
    }
}
