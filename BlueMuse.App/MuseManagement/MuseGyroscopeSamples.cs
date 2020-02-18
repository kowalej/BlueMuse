using BlueMuse.Helpers;
using System;
using System.Diagnostics;

namespace BlueMuse.MuseManagement
{
    public class MuseGyroscopeSamples
    {
        public static double[,] DecodeGyroscopeSamples(string bits)
        {
            // Each packet contains a 16 bit timestamp, followed by 9, 16 bit values representing 3 "xyz" samples.
            double[,] samples = new double[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT, Constants.MUSE_GYROSCOPE_CHANNEL_COUNT];
            for (int i = 0; i < Constants.MUSE_GYROSCOPE_SAMPLE_COUNT; i++)
            {
                for (int j = 0; j < Constants.MUSE_GYROSCOPE_CHANNEL_COUNT; j++)
                {
                    samples[i, j] = PacketConversion.ToInt16(bits, 16 + ( ((i * Constants.MUSE_GYROSCOPE_CHANNEL_COUNT) + j) * 16) ); // Initial offset by 16 bits for the timestamp.
                    samples[i, j] *= Constants.MUSE_GYROSCOPE_SCALE_FACTOR;
                }
            }
            return samples;
        }

        private double baseTimestamp = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() / 1000d;
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

        private double baseTimestamp2 = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() / 1000d;
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
                double baseSeconds = baseTimestamp;

                for (int i = 0; i < Constants.MUSE_GYROSCOPE_SAMPLE_COUNT; i++)
                {
                    timestamps[i] = baseSeconds - ((Constants.MUSE_GYROSCOPE_SAMPLE_COUNT - i) * (Constants.MUSE_GYROSCOPE_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
                    timestamps[i] = timestamps[i];
                }
                return timestamps;
            }
        }

        private double[] timestamps2;
        public double[] Timestamps2
        {
            get
            {
                double baseSeconds = baseTimestamp2;

                for (int i = 0; i < Constants.MUSE_GYROSCOPE_SAMPLE_COUNT; i++)
                {
                    timestamps2[i] = baseSeconds - ((Constants.MUSE_GYROSCOPE_SAMPLE_COUNT - i) * (Constants.MUSE_GYROSCOPE_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
                    timestamps2[i] = timestamps2[i];
                }
                return timestamps2;
            }
        }

        public double[,] XYZSamples { get; set; }

        public MuseGyroscopeSamples()
        {
            XYZSamples = new double[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT, Constants.MUSE_GYROSCOPE_CHANNEL_COUNT];
            timestamps = new double[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT];
            timestamps2 = new double[Constants.MUSE_GYROSCOPE_SAMPLE_COUNT];
        }
    }
}
