using BlueMuse.Helpers;
using Serilog;
using System;
using System.Linq;

namespace BlueMuse.LSL
{
    /// <summary>
    /// Manages in-process LSL stream outlets for the app. This replaces the old cross-process
    /// LSL AppService IPC protocol - streams are now opened, closed, and pushed to directly
    /// from within BlueMuse.App instead of being serialized and sent to a separate bridge process.
    /// </summary>
    public class BlueMuseLSLStreamManager
    {
        private readonly ObservableCollection<BlueMuseLSLStream> streams;
        private readonly Action<int> streamCountSetter;

        public BlueMuseLSLStreamManager(ObservableCollection<BlueMuseLSLStream> streams, Action<int> streamCounterSetter)
        {
            this.streams = streams;
            streamCountSetter = streamCounterSetter;
        }

        public void OpenStream(BlueMuseLSLStreamInfo streamInfo)
        {
            if (streamInfo.SendSecondaryTimestamp)
            {
                if (streamInfo.ChannelDataType == BlueMuseLSLDataType.FLOAT)
                {
                    streamInfo.Channels.Add(new BlueMuseLSLChannelInfo { Label = "Secondary Timestamp (Base)", Type = "timestamp", Unit = "seconds" });
                    streamInfo.Channels.Add(new BlueMuseLSLChannelInfo { Label = "Secondary Timestamp (Remainder)", Type = "timestamp", Unit = "seconds" });
                    streamInfo.ChannelCount += 2;
                }
                else if (streamInfo.ChannelDataType == BlueMuseLSLDataType.DOUBLE)
                {
                    streamInfo.Channels.Add(new BlueMuseLSLChannelInfo { Label = "Secondary Timestamp", Type = "timestamp", Unit = "seconds" });
                    streamInfo.ChannelCount += 1;
                }
            }
            if (!streams.Any(x => x.StreamInfo.StreamName == streamInfo.StreamName))
            {
                streams.Add(new BlueMuseLSLStream(streamInfo));
                streamCountSetter(streams.Count);
            }
        }

        public void CloseStream(string streamName)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream != null)
            {
                streams.Remove(stream);
                stream.Dispose();
                streamCountSetter(streams.Count);
            }
        }

        public void SendChunk(string streamName, float[,] data2D, double[] timestamps, double[] timestamps2 = null)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null)
            {
                Log.Warning("SendChunk (float): No LSL stream found for name '{StreamName}'. Chunk with {SampleCount} samples dropped.", streamName, data2D.GetLength(0));
                return;
            }
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);
            timestamps2 = ResolveTimestamps(streamInfo, timestamps2);

            Log.Debug("SendChunk (float) -> Stream: '{StreamName}', Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}, HasSecondaryTimestamps: {HasSecondary}",
                streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0, timestamps2 != null);

            try
            {
                stream.PushChunkLSL(data2D, timestamps, timestamps2);
                stream.UpdateSampleRate(timestamps.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendChunk (float) failed pushing chunk to LSL stream '{StreamName}'. Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                    streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);
                throw;
            }
        }

        public void SendChunk(string streamName, double[,] data2D, double[] timestamps, double[] timestamps2 = null)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null)
            {
                Log.Warning("SendChunk (double): No LSL stream found for name '{StreamName}'. Chunk with {SampleCount} samples dropped.", streamName, data2D.GetLength(0));
                return;
            }
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);
            timestamps2 = ResolveTimestamps(streamInfo, timestamps2);

            Log.Debug("SendChunk (double) -> Stream: '{StreamName}', Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}, HasSecondaryTimestamps: {HasSecondary}",
                streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0, timestamps2 != null);

            try
            {
                stream.PushChunkLSL(data2D, timestamps, timestamps2);
                stream.UpdateSampleRate(timestamps.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendChunk (double) failed pushing chunk to LSL stream '{StreamName}'. Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                    streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);
                throw;
            }
        }

        public void SendChunk(string streamName, int[,] data2D, double[] timestamps, double[] timestamps2 = null)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null)
            {
                Log.Warning("SendChunk (int): No LSL stream found for name '{StreamName}'. Chunk with {SampleCount} samples dropped.", streamName, data2D.GetLength(0));
                return;
            }
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);

            if (timestamps2 != null)
                Log.Warning("SendChunk (int): Secondary timestamps are not supported for int channel data and will be ignored. Stream: '{StreamName}'.", streamName);

            Log.Debug("SendChunk (int) -> Stream: '{StreamName}', Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);

            try
            {
                stream.PushChunkLSL(data2D, timestamps);
                stream.UpdateSampleRate(timestamps.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendChunk (int) failed pushing chunk to LSL stream '{StreamName}'. Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                    streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);
                throw;
            }
        }

        public void SendChunk(string streamName, string[,] data2D, double[] timestamps, double[] timestamps2 = null)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null)
            {
                Log.Warning("SendChunk (string): No LSL stream found for name '{StreamName}'. Chunk with {SampleCount} samples dropped.", streamName, data2D.GetLength(0));
                return;
            }
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);

            if (timestamps2 != null)
                Log.Warning("SendChunk (string): Secondary timestamps are not supported for string channel data and will be ignored. Stream: '{StreamName}'.", streamName);

            Log.Debug("SendChunk (string) -> Stream: '{StreamName}', Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);

            try
            {
                stream.PushChunkLSL(data2D, timestamps);
                stream.UpdateSampleRate(timestamps.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendChunk (string) failed pushing chunk to LSL stream '{StreamName}'. Samples: {SampleCount}, Channels: {ChannelCount}, Timestamps: {TimestampCount}",
                    streamName, data2D.GetLength(0), data2D.GetLength(1), timestamps?.Length ?? 0);
                throw;
            }
        }

        // Null (no timestamps to resolve, e.g. secondary timestamps disabled) passes through unchanged.
        // A leading negative-infinity sentinel means the caller wants native LSL clock timestamps generated.
        private static double[] ResolveTimestamps(BlueMuseLSLStreamInfo streamInfo, double[] timestamps)
        {
            if (timestamps == null) return null;
            if (double.IsNegativeInfinity(timestamps[0]))
            {
                return StreamHelper.GenerateLSLNativeTimestamps(streamInfo);
            }
            return timestamps;
        }

        public void CloseAllStreams()
        {
            foreach (var stream in streams.ToList())
            {
                streams.Remove(stream);
                stream.Dispose();
            }
        }
    }
}