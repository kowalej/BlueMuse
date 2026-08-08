using System;

namespace BlueMuse.Athena
{
    /// <summary>
    /// Payload decoders for the Muse S Athena.
    ///
    /// Athena packs its bit fields LSB-first, which is the opposite of the older
    /// headbands (see <see cref="BlueMuse.Helpers.PacketConversion"/>, which renders
    /// each byte MSB-first into a string and reads from there). EEG is also plain
    /// unsigned with no midpoint offset, again unlike the legacy 12-bit offset-binary
    /// samples.
    /// </summary>
    public static class AthenaDecoders
    {
        public const int EEG_BITS = 14;
        public const int OPTICS_BITS = 20;

        /// <summary>
        /// Read <paramref name="width"/> bits starting at absolute bit
        /// <paramref name="bitStart"/>, LSB-first: absolute bit 0 is the least
        /// significant bit of byte 0, and the first bit read is the field's LSB.
        /// </summary>
        public static uint ExtractLsbBits(byte[] data, int bitStart, int width)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width < 0 || width > 32) throw new ArgumentOutOfRangeException(nameof(width), "width must be 0..32");
            if (bitStart < 0) throw new ArgumentOutOfRangeException(nameof(bitStart));
            if (bitStart + width > data.Length * 8) throw new ArgumentOutOfRangeException(nameof(width), "field extends past end of buffer");

            uint value = 0;
            for (int i = 0; i < width; i++)
            {
                int absBit = bitStart + i;
                uint bit = (uint)((data[absBit >> 3] >> (absBit & 7)) & 1);
                value |= bit << i;
            }
            return value;
        }

        /// <summary>
        /// EEG (tags 0x11 / 0x12): 14-bit unsigned LSB-first fields laid out
        /// sample-major, scaled to microvolts over a 1450 uV full scale.
        /// </summary>
        public static double[,] DecodeEEG(byte[] payload, int channelCount, int sampleCount)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var samples = new double[sampleCount, channelCount];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int j = 0; j < channelCount; j++)
                {
                    uint raw = ExtractLsbBits(payload, ((i * channelCount) + j) * EEG_BITS, EEG_BITS);
                    samples[i, j] = raw * Constants.MUSE_ATHENA_EEG_SCALE_FACTOR;
                }
            }
            return samples;
        }

        /// <summary>
        /// Combined accelerometer + gyroscope (tag 0x47): 6 channels x 3 samples of
        /// int16 LE, per sample [ax, ay, az, gx, gy, gz]. The accelerometer scale
        /// matches the older headbands; the gyroscope scale is NEGATED relative to
        /// them, so channels 3..5 come out sign-flipped versus a legacy Muse 2.
        /// </summary>
        public static double[,] DecodeAccelerometerGyroscope(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            int channelCount = Constants.MUSE_ATHENA_IMU_CHANNEL_COUNT;
            int sampleCount = Constants.MUSE_ATHENA_IMU_SAMPLE_COUNT;
            int needed = channelCount * sampleCount * 2;
            if (payload.Length < needed) throw new ArgumentException($"IMU payload must be at least {needed} bytes.", nameof(payload));

            var samples = new double[sampleCount, channelCount];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int j = 0; j < channelCount; j++)
                {
                    int offset = ((i * channelCount) + j) * 2;
                    short raw = (short)(payload[offset] | (payload[offset + 1] << 8));
                    samples[i, j] = raw * (j < Constants.MUSE_ACCELEROMETER_CHANNEL_COUNT
                        ? Constants.MUSE_ACCELEROMETER_SCALE_FACTOR
                        : Constants.MUSE_ATHENA_GYROSCOPE_SCALE_FACTOR);
                }
            }
            return samples;
        }

        /// <summary>
        /// Optics / fNIRS (tags 0x34 / 0x35 / 0x36): 20-bit unsigned LSB-first fields
        /// laid out sample-major. These are raw detector counts - no scaling.
        /// </summary>
        public static double[,] DecodeOptics(byte[] payload, int channelCount, int sampleCount)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var samples = new double[sampleCount, channelCount];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int j = 0; j < channelCount; j++)
                {
                    samples[i, j] = ExtractLsbBits(payload, ((i * channelCount) + j) * OPTICS_BITS, OPTICS_BITS);
                }
            }
            return samples;
        }

        /// <summary>
        /// Battery (tags 0x88 / 0x98): uint16 LE at data[0..2], in 1/256ths of a percent.
        /// </summary>
        public static double DecodeBatteryPercent(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length < 2) throw new ArgumentException("Battery payload must be at least 2 bytes.", nameof(payload));
            return (payload[0] | (payload[1] << 8)) / 256d;
        }
    }
}
