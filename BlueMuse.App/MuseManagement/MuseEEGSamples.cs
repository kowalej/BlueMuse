using BlueMuse.Helpers;
using System;
using System.Collections.Generic;

namespace BlueMuse.MuseManagement
{
    public class MuseEEGSamples
    {
        public static double[] DecodeEEGSamples(string bits)
        {
            // Each packet contains a 16 bit timestamp, followed by 12, 12-bit samples.
            double[] samples = new double[12];
            for (int i = 0; i < 12; i++)
            {
                samples[i] = PacketConversion.ToUInt12(bits, 16 + (i * 12)); // Initial offset by 16 bits for the timestamp.
                samples[i] = (samples[i] - 2048d) * 0.48828125d; // 12 bits on a 2 mVpp range.
            }
            return samples;
        }

        public DateTimeOffset CreatedAt;

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

                for (int i = 0; i < Constants.MUSE_EEG_SAMPLE_COUNT; i++)
                {
                    timestamps[i] = baseSeconds - ((Constants.MUSE_EEG_SAMPLE_COUNT - i) * (Constants.MUSE_EEG_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
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

                for (int i = 0; i < Constants.MUSE_EEG_SAMPLE_COUNT; i++)
                {
                    timestamps2[i] = baseSeconds - ((Constants.MUSE_EEG_SAMPLE_COUNT - i) * (Constants.MUSE_EEG_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
                    timestamps2[i] = timestamps2[i];
                }
                return timestamps2;
            }
        }

        public Dictionary<Guid, double[]> ChannelData { get; set; }

        public MuseEEGSamples()
        {
            ChannelData = new Dictionary<Guid, double[]>();
            CreatedAt = DateTimeOffset.UtcNow;
            timestamps = new double[Constants.MUSE_EEG_SAMPLE_COUNT];
            timestamps2 = new double[Constants.MUSE_EEG_SAMPLE_COUNT];
        }
    }
}
