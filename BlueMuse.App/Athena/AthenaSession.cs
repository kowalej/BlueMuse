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
    /// Smooth, host anchored timestamps for one fixed rate Athena stream.
    ///
    /// Athena's packet header carries a packet index but no device clock, so
    /// timestamps come from the host arrival time - the same place the legacy
    /// streams get theirs. Rather than back-date every packet from its own arrival
    /// time (which stamps Bluetooth delivery jitter onto the samples), a recursive
    /// least squares fit maps a monotonic sample index onto host time, tracking the
    /// real sample rate as it drifts. This mirrors muse-lsl's RLSTimestampCorrector
    /// and the dejittering the legacy muse-lsl EEG path uses.
    ///
    /// Athena multiplexes several streams at different rates onto one
    /// characteristic, so each stream needs its own corrector.
    /// </summary>
    public class AthenaTimestampCorrector
    {
        private readonly double intercept; // Host time of sample index 0.
        private double slope;              // Seconds per sample, refit on every packet.
        private double p = 1e-4d;
        private long sampleIndex;

        public AthenaTimestampCorrector(double sampleRate, double hostTime)
        {
            if (sampleRate <= 0d) throw new ArgumentOutOfRangeException(nameof(sampleRate), "sampleRate must be > 0");
            intercept = hostTime;
            slope = 1d / sampleRate;
        }

        /// <summary>
        /// Consumes the next <paramref name="sampleCount"/> sample indices, refits
        /// against the packet's host arrival time and returns their timestamps.
        /// </summary>
        public double[] Timestamps(int sampleCount, double hostTime)
        {
            if (sampleCount < 1) throw new ArgumentOutOfRangeException(nameof(sampleCount), "sampleCount must be >= 1");

            var indices = new double[sampleCount];
            for (int i = 0; i < sampleCount; i++) indices[i] = sampleIndex + i;
            sampleIndex += sampleCount;

            Update(indices[sampleCount - 1], hostTime);

            var timestamps = new double[sampleCount];
            for (int i = 0; i < sampleCount; i++) timestamps[i] = (slope * indices[i]) + intercept;
            return timestamps;
        }

        /// <summary>Recursive least squares step, as in muse-lsl's _update_timestamp_correction.</summary>
        private void Update(double sourceIndex, double receiverTime)
        {
            receiverTime -= intercept;
            double sourceSquared = sourceIndex * sourceIndex;
            double denominator = 1d - (p * sourceSquared);
            if (denominator == 0d) return; // Degenerate fit - keep the previous slope.

            p = p - (((p * p) * sourceSquared) / denominator);
            slope = slope + (p * sourceIndex * (receiverTime - (sourceIndex * slope)));
        }
    }
}
