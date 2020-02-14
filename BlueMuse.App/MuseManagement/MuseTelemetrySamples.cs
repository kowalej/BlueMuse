using BlueMuse.Helpers;
using System;

namespace BlueMuse.MuseManagement
{
    public class MuseTelemetrySamples
    {
        public static double[] DecodeTelemetrySamples(string bits)
        {
            // Each packet contains a 16 bit timestamp, followed by 4, 16-bit values representing battery, "fuel_gauge?", adc voltage, and temperature.
            double[] samples = new double[Constants.MUSE_TELEMETRY_CHANNEL_COUNT];
            for (int i = 0; i < Constants.MUSE_TELEMETRY_CHANNEL_COUNT; i++)
            {
                samples[i] = PacketConversion.ToUInt16(bits, 16 + (i * 16)); // Initial offset by 16 bits for the timestamp.
            }

            // The following conversion are from muse-lsl (inside muse.py). Not sure how accurate this info is. 2/13/2020.

            // Battery.
            samples[0] = samples[0] / 512;

            // Fuel guage...
            samples[1] = samples[1] * 2.2d;

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

                for (int i = 0; i < Constants.MUSE_TELEMETRY_SAMPLE_COUNT; i++)
                {
                    timestamps[i] = baseSeconds - ((Constants.MUSE_TELEMETRY_SAMPLE_COUNT - i) * (Constants.MUSE_TELEMETRY_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
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

                for (int i = 0; i < Constants.MUSE_TELEMETRY_SAMPLE_COUNT; i++)
                {
                    timestamps2[i] = baseSeconds - ((Constants.MUSE_TELEMETRY_SAMPLE_COUNT - i) * (Constants.MUSE_TELEMETRY_SAMPLE_TIME_MILLIS / 1000d)); // Offset times based on sample rate.
                    timestamps2[i] = timestamps2[i];
                }
                return timestamps2;
            }
        }

        public double[] TelemetryData { get; set; }

        public MuseTelemetrySamples()
        {
            TelemetryData = new double[Constants.MUSE_TELEMETRY_SAMPLE_COUNT];
            timestamps = new double[Constants.MUSE_TELEMETRY_SAMPLE_COUNT];
            timestamps2 = new double[Constants.MUSE_TELEMETRY_SAMPLE_COUNT];
        }
    }
}
