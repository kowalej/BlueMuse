using BlueMuse;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LSLBridge.LSLManagement
{
    public class MuseLSLStreamManager
    {
        private volatile ObservableCollection<MuseLSLStream> museStreams;
        private Action<int> museStreamCountSetter;
        private AppServiceConnection lslStreamService;
        private Timer keepAliveTimer;
        private static readonly Object syncLock = new object();
        private DateTime lastMessageTime = DateTime.MinValue;

        public MuseLSLStreamManager(ObservableCollection<MuseLSLStream> museStreams, Action<int> museStreamCountSetter)
        {
            this.museStreams = museStreams;
            this.museStreamCountSetter = museStreamCountSetter;

            lslStreamService = new AppServiceConnection();
            lslStreamService.PackageFamilyName = Package.Current.Id.FamilyName;
            lslStreamService.AppServiceName = "LSLService";
            lslStreamService.RequestReceived += LSLService_RequestReceived;
            OpenService();
            keepAliveTimer = new Timer(CheckLastMessage, null, 0, 1); // Check if we're running every second.
        }

        private async void OpenService()
        {
            await lslStreamService.OpenAsync();
        }

        private void CheckLastMessage(object state)
        {
            // Auto close off bridge if we aren't receiving any data. This fixes LSLBridge not being shut down after closing BlueMuse.
            if (lastMessageTime != DateTime.MinValue && (DateTime.Now - lastMessageTime).Seconds > 2)
            {
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
            lastMessageTime = DateTime.Now;
            ValueSet message = args.Request.Message;
            object value;
            if (message.TryGetValue(Constants.LSL_MESSAGE_TYPE, out value))
            {
                string commandType = (string)value;
                switch (commandType)
                {
                    case Constants.LSL_MESSAGE_TYPE_OPEN_STREAM:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_DEVICE_NAME];
                            bool sendSecondaryTimestamp = (bool)message[Constants.LSL_MESSAGE_SEND_SECONDARY_TIMESTAMP];
                            if (!museStreams.Any(x => x.Name == streamName))
                            {
                                museStreams.Add(new MuseLSLStream(streamName, sendSecondaryTimestamp));
                                museStreamCountSetter(museStreams.Count);
                            }
                        }
                        break;

                    case Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_DEVICE_NAME];
                            var stream = museStreams.FirstOrDefault(x => x.Name == streamName);
                            if (stream != null)
                            {
                                museStreams.Remove(stream);
                                stream.Dispose();
                                museStreamCountSetter(museStreams.Count);
                            }
                        }
                        break;

                    case Constants.LSL_MESSAGE_TYPE_SEND_CHUNK:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_DEVICE_NAME];
                            var stream = museStreams.FirstOrDefault(x => x.Name == streamName);
                            if (stream != null)
                            {                                
                                double[] data1D = (double[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                double[,] data2D = new double[Constants.MUSE_SAMPLE_COUNT, stream.ChannelCount];
                                for (int i = 0; i < stream.ChannelCount - (stream.SendSecondaryTimestamp ? 1 : 0); i++)
                                {
                                    for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                                    {
                                        data2D[j, i] = data1D[(i * Constants.MUSE_SAMPLE_COUNT) + j];
                                    }
                                }
                                double[] timestamps = ((double[])message[Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS]);

                                // Add in secondary timestamp to data chunk.
                                if (stream.SendSecondaryTimestamp)
                                {
                                    double[] timestamps2 = ((double[])message[Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS2]);
                                    for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                                    {
                                        data2D[j, stream.channelCount - 1] = timestamps2[j];
                                    }
                                }

                                stream.PushChunkLSL(data2D, timestamps);
                                stream.UpdateSampleRate(timestamps.Length);
                            }
                        }
                        break;

                    // Should not be called until application is closing.
                    case Constants.LSL_MESSAGE_TYPE_CLOSE_BRIDGE:
                        {
                            CloseBridge();
                        }
                        break;
                }
            }
        }
    }
}
