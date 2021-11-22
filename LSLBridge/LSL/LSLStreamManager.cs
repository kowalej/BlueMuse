using LSLBridge.Helpers;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LSLBridge.LSL
{
    public class LSLStreamManager
    {
        private volatile ObservableCollection<LSLStream> streams;
        private readonly Action<int> streamCountSetter;
        private AppServiceConnection lslStreamService;
        private readonly Timer keepAliveTimer;
        private DateTime lastMessageTime = DateTime.MinValue;

        public LSLStreamManager(ObservableCollection<LSLStream> streams, Action<int> streamCounterSetter)
        {
            this.streams = streams;
            streamCountSetter = streamCounterSetter;

            lslStreamService = new AppServiceConnection
            {
                PackageFamilyName = Package.Current.Id.FamilyName,
                AppServiceName = "LSLService"
            };
            lslStreamService.RequestReceived += LSLService_RequestReceived;
            OpenService();
            //keepAliveTimer = new Timer(CheckLastMessage, null, 0, 1000); // Check if we're running every 1000ms.
        }

        private async void OpenService()
        {
            await lslStreamService.OpenAsync();
        }

        private void CheckLastMessage(object state)
        {
            // Auto close off bridge if we aren't receiving any data. This fixes LSLBridge not being shut down after closing main app.
            // The main application should send a "keep alive" message every 1000ms.
            if (lastMessageTime != DateTime.MinValue && (DateTime.UtcNow - lastMessageTime).TotalMilliseconds > 5000)
            {
                Log.Information("Closing LSL bridge (timeout - no messages from BlueMuse).");
                CloseBridge();
            }
        }

        private void CloseBridge()
        {
            lslStreamService.RequestReceived -= LSLService_RequestReceived;
            lslStreamService.Dispose();
            Process.GetCurrentProcess().Kill();
        }

        private void LSLService_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            lastMessageTime = DateTime.UtcNow;
            ValueSet message = args.Request.Message;
            if (message.TryGetValue(Constants.LSL_MESSAGE_TYPE, out object value))
            {
                string commandType = (string)value;
                switch (commandType)
                {
                    case Constants.LSL_MESSAGE_TYPE_KEEP_ACTIVE:
                        return;
                    case Constants.LSL_MESSAGE_TYPE_OPEN_STREAM:
                        {
                            LSLBridgeStreamInfo streamInfo = JsonConvert.DeserializeObject<LSLBridgeStreamInfo>((string)message[Constants.LSL_MESSAGE_STREAM_INFO]);
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
                        break;

                    case Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_STREAM_NAME];
                            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
                            if (stream != null)
                            {
                                streams.Remove(stream);
                                stream.Dispose();
                                streamCountSetter(streams.Count);
                            }
                        }
                        break;

                    case Constants.LSL_MESSAGE_TYPE_SEND_CHUNK:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_STREAM_NAME];
                            var stream = streams.FirstOrDefault(x => x.StreamInfo.StreamName == streamName);
                            if (stream != null)
                            {
                                var streamInfo = stream.StreamInfo;

                                // Get our stream timestamps.
                                double[] nativeTimestamps = null;
                                double[] timestamps = ((double[])message[Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS]);
                                if (double.IsNegativeInfinity(timestamps[0])) // Hack since main app can't call native lsl_local_clock().
                                {
                                    nativeTimestamps = StreamHelper.GenerateLSLNativeTimestamps(streamInfo);
                                    timestamps = nativeTimestamps;
                                }

                                // Potentially add in secondary timestamps to be pushed with data chunk if we are using float/double format.
                                double[] timestamps2 = null;
                                if (streamInfo.SendSecondaryTimestamp)
                                {
                                    timestamps2 = ((double[])message[Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2]);
                                    if (double.IsNegativeInfinity(timestamps2[0])) // Hack since main app can't call native lsl_local_clock().
                                    {
                                        timestamps2 = nativeTimestamps ?? StreamHelper.GenerateLSLNativeTimestamps(streamInfo);
                                    }
                                }

                                // Get our stream data, figure out data type, get secondary timestamps if needed, and push chunk.                                
                                if (streamInfo.ChannelDataType == LSLBridgeDataType.FLOAT)
                                {
                                    float[] data1D = (float[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                    float[,] data2D = data1D.To2DArray(streamInfo.ChunkSize, streamInfo.ChannelCount - (streamInfo.SendSecondaryTimestamp ? 2 : 0)); // Two extra channels for float timestamp, so subtract 2 from length to get actual data part.
                                    stream.PushChunkLSL(data2D, timestamps, timestamps2);
                                }
                                else if (streamInfo.ChannelDataType == LSLBridgeDataType.DOUBLE)
                                {
                                    double[] data1D = (double[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                    double[,] data2D = data1D.To2DArray(streamInfo.ChunkSize, streamInfo.ChannelCount - (streamInfo.SendSecondaryTimestamp ? 1 : 0));
                                    stream.PushChunkLSL(data2D, timestamps, timestamps2);
                                }
                                else if (streamInfo.ChannelDataType == LSLBridgeDataType.INT)
                                {
                                    int[] data1D = (int[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                    int[,] data2D = data1D.To2DArray(streamInfo.ChunkSize, streamInfo.ChannelCount);
                                    stream.PushChunkLSL(data2D, timestamps);
                                }
                                else if (streamInfo.ChannelDataType == LSLBridgeDataType.STRING)
                                {
                                    string[] data1D = (string[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                    string[,] data2D = data1D.To2DArray(streamInfo.ChunkSize, streamInfo.ChannelCount);
                                    stream.PushChunkLSL(data2D, timestamps);
                                }

                                stream.UpdateSampleRate(timestamps.Length);
                            }
                        }
                        break;

                    // Should not be called until application is closing.
                    case Constants.LSL_MESSAGE_TYPE_CLOSE_BRIDGE:
                        {
                            Log.Information("Closing LSL bridge (requested by BlueMuse).");
                            CloseBridge();
                        }
                        break;
                }
            }
        }
    }
}
