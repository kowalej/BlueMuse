using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueMuse.Athena;

namespace BlueMuse.Athena.Tests
{
    /// <summary>
    /// Asserts the Muse S Athena wire format against the protocol reference: bit
    /// ordering, scale factors, gyroscope sign, packet framing, command framing and
    /// device tick timestamps. These are the parts that fail silently on real
    /// hardware - a wrong bit order still produces plausible looking numbers.
    /// </summary>
    public static class Program
    {
        private static int failures;

        public static int Main()
        {
            ExtractLsbBitsReadsLeastSignificantBitFirst();
            EegDecodesUnsigned14BitLsbFirstToMicrovolts();
            AccGyroSplitsSixChannelsAndNegatesGyroscope();
            OpticsDecodesUnsigned20BitLsbFirstRawCounts();
            BatteryDecodesUint16LeOver256();
            ParserWalksPrimaryAndSubPackets();
            ParserWalksConcatenatedPackets();
            ParserStopsOnMalformedLength();
            ParserGivesVariableLengthTagsTheRemainder();
            CommandFramingMatchesProtocol();
            SessionEmitsInitThenStartSequence();
            TickClockMapsTicksToSecondsAndWraps();
            TickClockReanchorsOnDrift();

            Console.WriteLine(failures == 0 ? "PASS - all Athena protocol checks green." : $"FAIL - {failures} check(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        // --- Checks ---------------------------------------------------------

        private static void ExtractLsbBitsReadsLeastSignificantBitFirst()
        {
            // 0b1010_0101 = 0xA5. Low nibble read first is 0x5, next nibble 0xA.
            var data = new byte[] { 0xA5, 0x00 };
            Check(AthenaDecoders.ExtractLsbBits(data, 0, 4) == 0x5u, "LSB nibble");
            Check(AthenaDecoders.ExtractLsbBits(data, 4, 4) == 0xAu, "MSB nibble");

            // A field straddling a byte boundary takes its low bits from the earlier byte.
            var straddle = new byte[] { 0xFF, 0x01 };
            Check(AthenaDecoders.ExtractLsbBits(straddle, 4, 8) == 0x1Fu, "cross-byte field");

            Throws(() => AthenaDecoders.ExtractLsbBits(new byte[1], 0, 9), "read past end of buffer");
        }

        private static void EegDecodesUnsigned14BitLsbFirstToMicrovolts()
        {
            const int channels = 4, samples = 4;
            // Distinct raw per (sample, channel) so a transposed layout can't pass.
            var fields = new List<(int, uint)>();
            for (int s = 0; s < samples; s++)
                for (int c = 0; c < channels; c++)
                    fields.Add((14, (uint)(s * 100 + c)));

            var payload = PackLsb(fields);
            Check(payload.Length == 28, "0x11 payload is 28 bytes");

            var decoded = AthenaDecoders.DecodeEEG(payload, channels, samples);
            for (int s = 0; s < samples; s++)
                for (int c = 0; c < channels; c++)
                    CheckClose(decoded[s, c], (s * 100 + c) * (1450d / 16383d), $"eeg[{s},{c}]");

            // Full scale and zero, i.e. unsigned with no midpoint offset.
            var extremes = PackLsb(Enumerable.Repeat((14, 16383u), 16).ToList());
            CheckClose(AthenaDecoders.DecodeEEG(extremes, channels, samples)[0, 0], 1450d, "eeg full scale");
            CheckClose(AthenaDecoders.DecodeEEG(new byte[28], channels, samples)[0, 0], 0d, "eeg zero");
        }

        private static void AccGyroSplitsSixChannelsAndNegatesGyroscope()
        {
            // Per sample: [ax, ay, az, gx, gy, gz], int16 LE.
            var raws = new short[]
            {
                1, 2, 3, 1000, -1000, 4,
                -1, -2, -3, 5, 6, 7,
                100, 200, 300, 8, 9, 10,
            };
            var decoded = AthenaDecoders.DecodeAccelerometerGyroscope(Int16Le(raws));

            for (int s = 0; s < 3; s++)
            {
                for (int c = 0; c < 6; c++)
                {
                    double scale = c < 3 ? 0.0000610352d : -0.0074768d;
                    CheckClose(decoded[s, c], raws[s * 6 + c] * scale, $"imu[{s},{c}]");
                }
            }

            // The gyroscope scale is negated relative to the legacy headbands - the one
            // difference most likely to be missed, so pin the sign explicitly.
            Check(decoded[0, 3] < 0, "positive gyro raw decodes negative");
            Check(decoded[0, 4] > 0, "negative gyro raw decodes positive");
            Check(decoded[0, 0] > 0, "positive accel raw stays positive");

            Throws(() => AthenaDecoders.DecodeAccelerometerGyroscope(new byte[35]), "short IMU payload");
        }

        private static void OpticsDecodesUnsigned20BitLsbFirstRawCounts()
        {
            var fields = Enumerable.Range(0, 16).Select(i => (20, (uint)(i * 1000))).ToList();
            var payload = PackLsb(fields);
            Check(payload.Length == 40, "0x36 payload is 40 bytes");

            var decoded = AthenaDecoders.DecodeOptics(payload, 16, 1);
            for (int c = 0; c < 16; c++) CheckClose(decoded[0, c], c * 1000, $"optics[{c}]");

            // 20 bits is unsigned - the top of the range must not come back negative.
            var max = PackLsb(Enumerable.Repeat((20, 0xFFFFFu), 16).ToList());
            CheckClose(AthenaDecoders.DecodeOptics(max, 16, 1)[0, 0], 1048575d, "optics full scale");
        }

        private static void BatteryDecodesUint16LeOver256()
        {
            CheckClose(AthenaDecoders.DecodeBatteryPercent(new byte[] { 0x80, 0x31 }), 12672 / 256d, "battery 49.5%");
            CheckClose(AthenaDecoders.DecodeBatteryPercent(new byte[] { 0x00, 0x64 }), 100d, "battery 100%");
            Throws(() => AthenaDecoders.DecodeBatteryPercent(new byte[1]), "short battery payload");
        }

        private static void ParserWalksPrimaryAndSubPackets()
        {
            var eeg = Fill(28, 0x11);
            var imu = Fill(36, 0x47);
            var packet = Concat(
                PrimaryHeader(14 + 28 + 5 + 36, packetIndex: 7, deviceTick: 123456, tag: 0x11),
                eeg,
                SubHeader(0x47, subIndex: 2),
                imu);

            var packets = AthenaPacketParser.Parse(packet);
            Check(packets.Count == 2, "primary + one sub packet");
            Check(packets[0].Tag == 0x11 && packets[0].PacketIndex == 7, "primary tag/index");
            Check(packets[0].DeviceTick == 123456u, "device tick decoded LE");
            Check(packets[0].Payload.SequenceEqual(eeg), "primary payload");
            Check(packets[1].Tag == 0x47 && packets[1].PacketIndex == 2, "sub tag/index");
            Check(packets[1].DeviceTick == 123456u, "sub packet inherits primary tick");
            Check(packets[1].Payload.SequenceEqual(imu), "sub payload");
        }

        private static void ParserWalksConcatenatedPackets()
        {
            var first = Concat(PrimaryHeader(14 + 36, 1, 1000, 0x47), Fill(36, 0xAA));
            var second = Concat(PrimaryHeader(14 + 28, 2, 2000, 0x11), Fill(28, 0xBB));

            var packets = AthenaPacketParser.Parse(Concat(first, second));
            Check(packets.Count == 2, "two concatenated packets");
            Check(packets[0].DeviceTick == 1000u && packets[1].DeviceTick == 2000u, "per-packet ticks");
            Check(packets[1].Tag == 0x11, "second packet tag");
        }

        private static void ParserStopsOnMalformedLength()
        {
            // Length claims more bytes than the notification holds.
            var truncated = Concat(PrimaryHeader(200, 1, 1, 0x11), Fill(20, 0));
            Check(AthenaPacketParser.Parse(truncated).Count == 0, "over-long length bails out");

            // Length below the header size.
            var runt = Concat(PrimaryHeader(3, 1, 1, 0x11), Fill(20, 0));
            Check(AthenaPacketParser.Parse(runt).Count == 0, "undersized length bails out");

            Check(AthenaPacketParser.Parse(new byte[5]).Count == 0, "notification shorter than a header");
        }

        private static void ParserGivesVariableLengthTagsTheRemainder()
        {
            // 0x88 has no fixed size - it takes whatever is left in the packet.
            var packet = Concat(PrimaryHeader(14 + 4, 1, 1, 0x88), new byte[] { 0x80, 0x31, 0x00, 0x00 });
            var packets = AthenaPacketParser.Parse(packet);
            Check(packets.Count == 1 && packets[0].Payload.Length == 4, "0x88 consumes the packet remainder");
            CheckClose(AthenaDecoders.DecodeBatteryPercent(packets[0].Payload), 49.5d, "battery via parser");
        }

        private static void CommandFramingMatchesProtocol()
        {
            Check(AthenaSession.FrameCommand("dc001").SequenceEqual(
                new byte[] { 0x06, (byte)'d', (byte)'c', (byte)'0', (byte)'0', (byte)'1', 0x0A }), "frame dc001");
            Check(AthenaSession.FrameCommand("h").SequenceEqual(new byte[] { 0x02, (byte)'h', 0x0A }), "frame h");

            // Same framing the hardcoded legacy commands already use.
            Check(AthenaSession.FrameCommand("s").SequenceEqual(BlueMuse.Constants.MUSE_CMD_ASK_CONTROL_STATUS), "matches legacy 's'");
            Check(AthenaSession.FrameCommand("v6").SequenceEqual(BlueMuse.Constants.MUSE_CMD_ASK_DEVICE_INFO_ATHENA), "matches Athena 'v6'");
        }

        private static void SessionEmitsInitThenStartSequence()
        {
            var sent = new List<string>();
            var session = new AthenaSession(command =>
            {
                sent.Add(Unframe(command));
                return Task.FromResult(true);
            });

            session.Start().GetAwaiter().GetResult();
            var expected = new[] { "v6", "s", "h", "p1041", "s", "dc001", "dc001", "L1", "s" };
            Check(sent.SequenceEqual(expected), $"start sequence, got [{string.Join(",", sent)}]");

            sent.Clear();
            session.Stop().GetAwaiter().GetResult();
            Check(sent.SequenceEqual(new[] { "h" }), "stop sends halt");

            sent.Clear();
            new AthenaSession(c => { sent.Add(Unframe(c)); return Task.FromResult(true); }, "p50")
                .Initialize().GetAwaiter().GetResult();
            Check(sent.Contains("p50") && !sent.Contains("p1041"), "explicit preset overrides the default");
        }

        private static void TickClockMapsTicksToSecondsAndWraps()
        {
            var clock = new DeviceTickClock();

            // First packet anchors: its timestamp is the host clock exactly.
            CheckClose(clock.PacketTimestamp(1000, 500d), 500d, "first packet anchors to host");

            // One second of ticks later. Host clock agrees, so the tick prediction stands.
            CheckClose(clock.PacketTimestamp(1000 + 256000, 501d), 501d, "256000 ticks == 1 second");

            // A quarter second of ticks, with the host clock jittered inside the resync
            // threshold - the device spacing wins over the jitter.
            CheckClose(clock.PacketTimestamp(1000 + 320000, 501.3d), 501.25d, "device spacing beats host jitter");

            // Tick counter wraps past 2^32 - the delta must stay small and positive.
            var wrapping = new DeviceTickClock();
            wrapping.PacketTimestamp(uint.MaxValue - 127999, 10d);
            CheckClose(wrapping.PacketTimestamp(128000, 10.9d), 11d, "tick wraparound is modulo 2^32");
        }

        private static void TickClockReanchorsOnDrift()
        {
            var clock = new DeviceTickClock { ResyncThresholdSeconds = 0.5d };
            clock.PacketTimestamp(0, 100d);

            // Device tick says +1s, host says +5s: past the threshold, so trust the host
            // and re-anchor. Without this the two clocks drift apart over a long session.
            CheckClose(clock.PacketTimestamp(256000, 105d), 105d, "drift beyond threshold re-anchors");
            CheckClose(clock.PacketTimestamp(256000 + 256000, 106d), 106d, "prediction resumes from the new anchor");
        }

        // --- Helpers --------------------------------------------------------

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            failures++;
            Console.WriteLine($"  FAILED: {what}");
        }

        private static void CheckClose(double actual, double expected, string what)
        {
            Check(Math.Abs(actual - expected) < 1e-9, $"{what} (expected {expected}, got {actual})");
        }

        private static void Throws(Action action, string what)
        {
            try
            {
                action();
                failures++;
                Console.WriteLine($"  FAILED: expected a throw for {what}");
            }
            catch (Exception) { }
        }

        /// <summary>Inverse of ExtractLsbBits, so expected raw values stay hand-checkable.</summary>
        private static byte[] PackLsb(List<(int width, uint value)> fields)
        {
            int totalBits = fields.Sum(f => f.width);
            var bytes = new byte[(totalBits + 7) / 8];
            int bitPos = 0;
            foreach (var (width, value) in fields)
            {
                for (int i = 0; i < width; i++)
                {
                    if (((value >> i) & 1u) != 0)
                    {
                        int absBit = bitPos + i;
                        bytes[absBit >> 3] |= (byte)(1 << (absBit & 7));
                    }
                }
                bitPos += width;
            }
            return bytes;
        }

        private static byte[] Int16Le(short[] values)
        {
            var bytes = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                bytes[i * 2] = (byte)(values[i] & 0xFF);
                bytes[i * 2 + 1] = (byte)((values[i] >> 8) & 0xFF);
            }
            return bytes;
        }

        private static byte[] PrimaryHeader(int packetLength, byte packetIndex, uint deviceTick, byte tag)
        {
            var header = new byte[14];
            header[0] = (byte)packetLength;
            header[1] = packetIndex;
            header[2] = (byte)(deviceTick & 0xFF);
            header[3] = (byte)((deviceTick >> 8) & 0xFF);
            header[4] = (byte)((deviceTick >> 16) & 0xFF);
            header[5] = (byte)((deviceTick >> 24) & 0xFF);
            header[9] = tag;
            return header;
        }

        private static byte[] SubHeader(byte tag, byte subIndex)
        {
            return new byte[5] { tag, subIndex, 0, 0, 0 };
        }

        private static byte[] Fill(int length, byte value)
        {
            var bytes = new byte[length];
            for (int i = 0; i < length; i++) bytes[i] = value;
            return bytes;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var result = new byte[parts.Sum(p => p.Length)];
            int pos = 0;
            foreach (var part in parts) { part.CopyTo(result, pos); pos += part.Length; }
            return result;
        }

        private static string Unframe(byte[] framed)
        {
            return System.Text.Encoding.ASCII.GetString(framed, 1, framed.Length - 2);
        }
    }
}
