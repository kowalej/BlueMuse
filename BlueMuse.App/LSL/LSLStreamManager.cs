using System;
using System.Linq;

namespace BlueMuse.LSL
{
    /// <summary>
    /// Manages in-process LSL stream outlets for the app. This replaces the old cross-process
    /// LSLBridge AppService IPC protocol - streams are now opened, closed, and pushed to directly
    /// from within BlueMuse.App instead of being serialized and sent to a separate bridge process.
    /// </summary>
    public class LSLStreamManager
    {
        private readonly ObservableCollection<LSLStream> streams;
        private readonly Action<int> streamCountSetter;

        public LSLStreamManager(ObservableCollection<LSLStream> streams, Action<int> streamCounterSetter)
        {
            this.streams = streams;
            streamCountSetter = streamCounterSetter;
        }

        public void OpenStream(LSLBridgeStreamInfo streamInfo)
        {
            if (streamInfo.SendSecondaryTimestamp)
            {
                if (streamInfo.ChannelDataType == LSLBridgeDataType.FLOAT)
                {
                    streamInfo.Channels.Add(new LSLBridgeChannelInfo { Label = "Secondary Timestamp (Base)", Type = "timestamp", Unit = "seconds" });
                    streamInfo.Channels.Add(new LSLBridgeChannelInfo { Label = "Secondary Timestamp (Remainder)", Type = "timestamp", Unit = "seconds" });
                    streamInfo.ChannelCount += 2;
                }
                else if (streamInfo.ChannelDataType == LSLBridgeDataType.DOUBLE)
                {
                    streamInfo.Channels.Add(new LSLBridgeChannelInfo { Label = "Secondary Timestamp", Type = "timestamp", Unit = "seconds" });
                    streamInfo.ChannelCount += 1;
                }
            }
            if (!streams.Any(x => x.StreamInfo.StreamName == streamInfo.StreamName))
            {
                streams.Add(new LSLStream(streamInfo));
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
            if (stream == null) return;
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);
            timestamps2 = ResolveTimestamps(streamInfo, timestamps2, streamInfo.SendSecondaryTimestamp);

            stream.PushChunkLSL(data2D, timestamps, timestamps2);
            stream.UpdateSampleRate(timestamps.Length);
        }

        public void SendChunk(string streamName, double[,] data2D, double[] timestamps, double[] timestamps2 = null)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null) return;
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);
            timestamps2 = ResolveTimestamps(streamInfo, timestamps2, streamInfo.SendSecondaryTimestamp);

            stream.PushChunkLSL(data2D, timestamps, timestamps2);
            stream.UpdateSampleRate(timestamps.Length);
        }

        public void SendChunk(string streamName, int[,] data2D, double[] timestamps)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null) return;
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);

            stream.PushChunkLSL(data2D, timestamps);
            stream.UpdateSampleRate(timestamps.Length);
        }

        public void SendChunk(string streamName, string[,] data2D, double[] timestamps)
        {
            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
            if (stream == null) return;
            var streamInfo = stream.StreamInfo;

            timestamps = ResolveTimestamps(streamInfo, timestamps);

            stream.PushChunkLSL(data2D, timestamps);
            stream.UpdateSampleRate(timestamps.Length);
        }

        private static double[] ResolveTimestamps(LSLBridgeStreamInfo streamInfo, double[] timestamps, bool required = true)
        {
            if (!required || timestamps == null) return timestamps;
            if (double.IsNegativeInfinity(timestamps[0])) // Caller requested native LSL clock timestamps be generated.
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