using System;

namespace BlueMuse.Athena
{
    /// <summary>
    /// Payload decoders for the Muse S Athena.
    ///
    /// Athena packs its bit fields LSB-first, which is the opposite of the older
    /// headbands (see <see cref="BlueMuse.Helpers.PacketConversion"/>, which renders
    /// each byte MSB-first into a string and reads from there). EEG samples are
    /// unsigned 14-bit centered on 2^13, so the midpoint is subtracted the way the
    /// legacy 12-bit samples subtract 2048.
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
        /// sample-major, centered on <see cref="Constants.MUSE_ATHENA_EEG_MIDPOINT"/>
        /// and scaled to microvolts over a 1450 uV full scale.
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
                    samples[i, j] = ((double)raw - Constants.MUSE_ATHENA_EEG_MIDPOINT) * Constants.MUSE_ATHENA_EEG_SCALE_FACTOR;
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
        /// laid out sample-major. These are raw detector counts - no scaling. The
        /// narrower tags carry a subset of the same 16 canonical channels, so the
        /// result is always 16 channels wide with the unused slots left at zero.
        /// </summary>
        public static double[,] DecodeOptics(byte[] payload, byte tag, int channelCount, int sampleCount)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var samples = new double[sampleCount, Constants.MUSE_ATHENA_OPTICS_CHANNEL_COUNT];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int j = 0; j < channelCount; j++)
                {
                    int canonical = OpticsCanonicalIndex(tag, j);
                    if (canonical < 0) continue;
                    samples[i, canonical] = ExtractLsbBits(payload, ((i * channelCount) + j) * OPTICS_BITS, OPTICS_BITS);
                }
            }
            return samples;
        }

        /// <summary>
        /// Maps a per-tag optics channel index onto canonical channel 0..15, or -1
        /// when the tag does not carry that channel. Tag 0x34's four channels are the
        /// canonical channels 4..7.
        /// </summary>
        public static int OpticsCanonicalIndex(byte tag, int channel)
        {
            if (channel < 0) return -1;
            switch (tag)
            {
                case AthenaPacketParser.TAG_OPTICS_4CH: return channel < 4 ? channel + 4 : -1;
                case AthenaPacketParser.TAG_OPTICS_8CH: return channel < 8 ? channel : -1;
                case AthenaPacketParser.TAG_OPTICS_16CH: return channel < 16 ? channel : -1;
                default: return -1;
            }
        }

        /// <summary>
        /// Battery (tags 0x88 / 0x98): uint16 LE at the start of the payload, in
        /// 1/512ths of a percent.
        /// </summary>
        public static double DecodeBatteryPercent(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length < 2) throw new ArgumentException("Battery payload must be at least 2 bytes.", nameof(payload));
            return (payload[0] | (payload[1] << 8)) * Constants.MUSE_ATHENA_BATTERY_SCALE_FACTOR;
        }
    }
}
