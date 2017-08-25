using BlueMuse;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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

        private static readonly Object syncLock = new object();

        public MuseLSLStreamManager(ObservableCollection<MuseLSLStream> museStreams, Action<int> museStreamCountSetter)
        {
            this.museStreams = museStreams;
            this.museStreamCountSetter = museStreamCountSetter;

            lslStreamService = new AppServiceConnection();
            lslStreamService.PackageFamilyName = Package.Current.Id.FamilyName;
            lslStreamService.AppServiceName = "LSLService";
            lslStreamService.RequestReceived += LSLService_RequestReceived;
            OpenService();
        }

        private async void OpenService()
        {
            await lslStreamService.OpenAsync();
        }

        private void LSLService_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            ValueSet message = args.Request.Message;
            object value;
            if (message.TryGetValue(Constants.LSL_MESSAGE_TYPE, out value))
            {
                string commandType = (string)value;
                switch (commandType)
                {
                    case Constants.LSL_MESSAGE_TYPE_OPEN_STREAM:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_MUSE_NAME];
                            if (!museStreams.Any(x => x.Name == streamName))
                            {
                                museStreams.Add(new MuseLSLStream(streamName));
                                museStreamCountSetter(museStreams.Count);
                            }
                        }
                        break;

                    case Constants.LSL_MESSAGE_TYPE_CLOSE_STREAM:
                        {
                            string streamName = (string)message[Constants.LSL_MESSAGE_MUSE_NAME];
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
                            string streamName = (string)message[Constants.LSL_MESSAGE_MUSE_NAME];
                            var stream = museStreams.FirstOrDefault(x => x.Name == streamName);
                            if (stream != null)
                            {
                                float[] data1D = (float[])message[Constants.LSL_MESSAGE_CHUNK_DATA];
                                float[,] data2D = new float[Constants.MUSE_SAMPLE_COUNT, Constants.MUSE_CHANNEL_COUNT];
                                for (int i = 0; i < Constants.MUSE_CHANNEL_COUNT; i++)
                                {
                                    for (int j = 0; j < Constants.MUSE_SAMPLE_COUNT; j++)
                                    {
                                        data2D[j, i] = data1D[(i * j) + j];
                                    }
                                }
                                double[] timestamps = ((double[])message[Constants.LSL_MESSAGE_CHUNK_TIMESTAMPS]);                               
                                stream.PushChunkLSL(data2D, timestamps);
                            }
                        }
                        break;

                    // Should not be called until application is closing.
                    case Constants.LSL_MESSAGE_TYPE_CLOSE_BRIDGE:
                        {
                            lslStreamService.RequestReceived -= LSLService_RequestReceived;
                            lslStreamService.Dispose();
                            Process.GetCurrentProcess().Kill();
                        }
                        break;
                }
            }

        }
    }
}
