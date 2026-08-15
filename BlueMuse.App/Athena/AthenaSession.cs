using System;
using System.Text;
using System.Threading.Tasks;

namespace BlueMuse.Athena
{
    /// <summary>
    /// Drives the Muse S Athena command handshake.
    ///
    /// Athena takes ASCII commands rather than the fixed byte blobs the older
    /// headbands use, and it needs a multi step sequence with specific inter-command
    /// delays - a single "start streaming" write is not enough.
    /// </summary>
    public class AthenaSession
    {
        private readonly Func<byte[], Task<bool>> writeCommand;
        private readonly string preset;

        public AthenaSession(Func<byte[], Task<bool>> writeCommand, string preset = null)
        {
            this.writeCommand = writeCommand ?? throw new ArgumentNullException(nameof(writeCommand));
            this.preset = string.IsNullOrEmpty(preset) ? Constants.MUSE_ATHENA_DEFAULT_PRESET : preset;
        }

        /// <summary>
        /// Frames an ASCII command as [length + 1, ...ascii, 0x0A]. Same framing the
        /// legacy commands in <see cref="Constants"/> are hardcoded with.
        /// </summary>
        public static byte[] FrameCommand(string command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var ascii = Encoding.ASCII.GetBytes(command);
            var framed = new byte[ascii.Length + 2];
            framed[0] = (byte)(ascii.Length + 1);
            Array.Copy(ascii, 0, framed, 1, ascii.Length);
            framed[ascii.Length + 1] = 0x0A;
            return framed;
        }

        /// <summary>Device info, status, halt, select preset, status.</summary>
        public async Task<bool> Initialize()
        {
            foreach (var command in new[] { "v6", "s", "h", preset, "s" })
            {
                if (!await Write(command)) return false;
                await Task.Delay(Constants.MUSE_ATHENA_INIT_COMMAND_INTERVAL_MILLIS);
            }
            return true;
        }

        /// <summary>
        /// Begin data, begin data (sent twice - the device ignores the first on a cold
        /// start), low latency mode, status.
        /// Returns <see langword="false"/> if any command write fails.
        /// </summary>
        public async Task<bool> Start()
        {
            if (!await Initialize()) return false;
            if (!await Write("dc001")) return false;
            await Task.Delay(Constants.MUSE_ATHENA_START_REPEAT_DELAY_MILLIS);
            if (!await Write("dc001")) return false;
            await Task.Delay(Constants.MUSE_ATHENA_START_LOW_LATENCY_DELAY_MILLIS);
            if (!await Write("L1")) return false;
            await Task.Delay(Constants.MUSE_ATHENA_START_STATUS_DELAY_MILLIS);
            if (!await Write("s")) return false;
            await Task.Delay(Constants.MUSE_ATHENA_START_TRAILING_DELAY_MILLIS);
            return true;
        }

        public Task<bool> Stop()
        {
            return Write("h");
        }

        private Task<bool> Write(string command)
        {
            return writeCommand(FrameCommand(command));
        }
    }

    /// <summary>
    /// Turns Athena's free running 32-bit device tick into host timestamps.
    ///
    /// The first packet anchors the tick against the host clock and subsequent
    /// packets are placed relative to that anchor, which keeps the spacing between
    /// packets true to the device instead of to Bluetooth delivery jitter. The
    /// device oscillator is not the host oscillator though, so the anchor is
    /// re-taken whenever prediction and host clock disagree by more than
    /// <see cref="ResyncThresholdSeconds"/> - without that the two drift apart over
    /// a long session.
    /// </summary>
    public class DeviceTickClock
    {
        /// <summary>Tunable: how far prediction may drift from the host clock before re-anchoring.</summary>
        public double ResyncThresholdSeconds = 0.5d;

        private bool anchored;
        private uint anchorTick;
        private double anchorHost;

        public void Anchor(uint deviceTick, double hostTime)
        {
            anchorTick = deviceTick;
            anchorHost = hostTime;
            anchored = true;
        }

        /// <summary>
        /// Host time of the first sample in the packet carrying
        /// <paramref name="deviceTick"/>. <paramref name="hostTime"/> is "now" in the
        /// same format, used to anchor and to bound drift.
        /// </summary>
        public double PacketTimestamp(uint deviceTick, double hostTime)
        {
            if (!anchored)
            {
                Anchor(deviceTick, hostTime);
                return hostTime;
            }

            uint elapsedTicks = unchecked(deviceTick - anchorTick); // Wraps modulo 2^32, as the device does.
            double predicted = anchorHost + (elapsedTicks / Constants.MUSE_ATHENA_TICK_RATE_HZ);

            if (Math.Abs(hostTime - predicted) > ResyncThresholdSeconds)
            {
                Anchor(deviceTick, hostTime);
                return hostTime;
            }
            return predicted;
        }
    }
}
