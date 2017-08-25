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
                double baseMillis = baseTimeStamp.DateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                for (int i = 0; i < Constants.MUSE_SAMPLE_COUNT; i++)
                {
                    TimeStamps[i] = baseMillis - ((Constants.MUSE_SAMPLE_COUNT - i) * Constants.MUSE_SAMPLE_TIME_MILLIS); // Offset times based on sample rate.
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
