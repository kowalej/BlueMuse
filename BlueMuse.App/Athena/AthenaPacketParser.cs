using System;
using System.Collections.Generic;

namespace BlueMuse.Athena
{
    /// <summary>
    /// A single primary or sub packet lifted out of a Muse S Athena notification.
    /// Sub packets inherit the device tick of the primary packet they ride in.
    /// </summary>
    public class AthenaPacket
    {
        public readonly byte Tag;
        public readonly byte PacketIndex;
        public readonly uint DeviceTick;
        public readonly byte[] Payload;

        public AthenaPacket(byte tag, byte packetIndex, uint deviceTick, byte[] payload)
        {
            Tag = tag;
            PacketIndex = packetIndex;
            DeviceTick = deviceTick;
            Payload = payload;
        }
    }

    /// <summary>
    /// Walks a Muse S Athena DATA_1 notification into packets.
    ///
    /// Unlike the older headbands (one GATT characteristic per channel, one sample
    /// block per notification), Athena multiplexes every sensor onto a single
    /// characteristic. A notification holds one or more concatenated packets:
    ///
    ///   primary header (14 bytes): [0] total packet length, [1] packet index,
    ///                              [2..5] device tick (uint32 LE), [9] tag
    ///   primary payload:           length determined by the tag
    ///   then, while >= 5 bytes of the packet remain:
    ///   sub header (5 bytes):      [0] tag, [1] sub index, [2..4] unused
    ///   sub payload:               length determined by the tag
    ///
    /// Byte [0] is authoritative for where the next packet starts.
    /// </summary>
    public static class AthenaPacketParser
    {
        public const int PRIMARY_HEADER_LENGTH = 14;
        public const int SUB_HEADER_LENGTH = 5;

        public const byte TAG_EEG_4CH = 0x11;   // 4 channels x 4 samples, 28 bytes.
        public const byte TAG_EEG_8CH = 0x12;   // 8 channels x 2 samples, 28 bytes.
        public const byte TAG_OPTICS_4CH = 0x34;  // 4 channels x 3 samples, 30 bytes.
        public const byte TAG_OPTICS_8CH = 0x35;  // 8 channels x 2 samples, 40 bytes.
        public const byte TAG_OPTICS_16CH = 0x36; // 16 channels x 1 sample, 40 bytes.
        public const byte TAG_ACC_GYRO = 0x47;  // 6 channels x 3 samples, 36 bytes.
        public const byte TAG_UNKNOWN = 0x53;   // 24 bytes, purpose undocumented - consumed and skipped.
        public const byte TAG_BATTERY = 0x88;   // Variable length (consumes the rest of the packet).
        public const byte TAG_BATTERY_20 = 0x98; // 20 bytes.

        /// <summary>
        /// Payload length for a tag, or -1 when the tag is variable length and
        /// therefore consumes the remainder of its packet.
        /// </summary>
        public static int DataLengthForTag(byte tag)
        {
            switch (tag)
            {
                case TAG_EEG_4CH: return 28;
                case TAG_EEG_8CH: return 28;
                case TAG_OPTICS_4CH: return 30;
                case TAG_OPTICS_8CH: return 40;
                case TAG_OPTICS_16CH: return 40;
                case TAG_ACC_GYRO: return 36;
                case TAG_UNKNOWN: return 24;
                case TAG_BATTERY_20: return 20;
                default: return -1;
            }
        }

        public static List<AthenaPacket> Parse(byte[] notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            var packets = new List<AthenaPacket>();

            int offset = 0;
            while (notification.Length - offset >= PRIMARY_HEADER_LENGTH)
            {
                int packetLength = notification[offset];

                // A length that doesn't fit the buffer means we've lost framing. Bail
                // out rather than guess - the next notification re-syncs us.
                if (packetLength < PRIMARY_HEADER_LENGTH || offset + packetLength > notification.Length) break;

                byte packetIndex = notification[offset + 1];
                uint deviceTick = ReadUInt32LE(notification, offset + 2);
                byte primaryTag = notification[offset + 9];

                int dataStart = offset + PRIMARY_HEADER_LENGTH;
                int dataSize = packetLength - PRIMARY_HEADER_LENGTH;
                int dataPos = 0;

                int primaryLength = DataLengthForTag(primaryTag);
                if (primaryLength < 0) primaryLength = dataSize;
                if (primaryLength > dataSize) primaryLength = dataSize;

                packets.Add(new AthenaPacket(primaryTag, packetIndex, deviceTick, Slice(notification, dataStart, primaryLength)));
                dataPos += primaryLength;

                while (dataSize - dataPos >= SUB_HEADER_LENGTH)
                {
                    byte subTag = notification[dataStart + dataPos];
                    byte subIndex = notification[dataStart + dataPos + 1];
                    int remaining = dataSize - dataPos - SUB_HEADER_LENGTH;

                    int subLength = DataLengthForTag(subTag);
                    if (subLength < 0) subLength = remaining;
                    if (subLength <= 0 || subLength > remaining) break;

                    packets.Add(new AthenaPacket(subTag, subIndex, deviceTick, Slice(notification, dataStart + dataPos + SUB_HEADER_LENGTH, subLength)));
                    dataPos += SUB_HEADER_LENGTH + subLength;
                }

                offset += packetLength;
            }

            return packets;
        }

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static byte[] Slice(byte[] data, int offset, int length)
        {
            var slice = new byte[length];
            Array.Copy(data, offset, slice, 0, length);
            return slice;
        }
    }
}
