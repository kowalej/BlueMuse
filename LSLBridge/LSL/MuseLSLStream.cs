using BlueMuse;
using LSLBridge.Helpers;
using System;
using System.Windows;

namespace LSLBridge.LSLManagement
{
    public class MuseLSLStream : ObservableObject, IDisposable
    {
        private static readonly Object syncLock = new object();

        public LSL.liblsl.StreamInfo LSLStreamInfo { get; private set; }

        private LSL.liblsl.StreamOutlet lslStream;

        private string name;
        public string Name { get { return name; } set { SetProperty(ref name, value); } }

        public string StreamDisplayInfo { get { return string.Format("Name: {0} - Nominal Rate: {1} - Channels: {2}", LSLStreamInfo.name(), LSLStreamInfo.nominal_srate(), string.Join(",", Constants.MUSE_CHANNEL_LABELS)); } }

        private double latestTimestamp;
        public double LatestTimestamp { get { return latestTimestamp; } set { SetProperty(ref latestTimestamp, value); } }

        int channelCount;

        public MuseLSLStream(string name)
        {
            Name = name;
            string[] channelLabels;
            if (name.Contains(Constants.DeviceNameFilter[0]))
            {
                channelCount = Constants.MUSE_CHANNEL_COUNT;
                channelLabels = Constants.MUSE_CHANNEL_LABELS;
            }
            else
            {
                channelCount = Constants.MUSE_SMITH_CHANNEL_COUNT;
                channelLabels = Constants.MUSE_SMITH_CHANNEL_LABELS;
            }

            LSLStreamInfo = new LSL.liblsl.StreamInfo(name, "EEG", channelCount, Constants.MUSE_SAMPLE_RATE, LSL.liblsl.channel_format_t.cf_float32, Application.ResourceAssembly.GetName().Name);
            LSLStreamInfo.desc().append_child_value("manufacturer", "Muse");
            LSLStreamInfo.desc().append_child_value("manufacturer", "Muse");
            LSLStreamInfo.desc().append_child_value("type", "EEG");
            var channels = LSLStreamInfo.desc().append_child("channels");
            foreach (var c in channelLabels)
            {
                channels.append_child("channel")
                .append_child_value("label", c)
                .append_child_value("unit", "microvolts")
                .append_child_value("type", "EEG");
            }
            OnPropertyChanged(nameof(StreamDisplayInfo));
            lslStream = new LSL.liblsl.StreamOutlet(LSLStreamInfo, Constants.MUSE_SAMPLE_COUNT, Constants.MUSE_LSL_BUFFER_LENGTH);
        }

        // Flag: Has Dispose already been called?
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if(LSLStreamInfo != null)
                    LSLStreamInfo.Dispose();
                if(lslStream != null)
                    lslStream.Dispose();
            }

            // Free any unmanaged objects here.
            //

            disposed = true;
        }

        public void PushChunkLSL(float[,] data, double[]timestamps)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            lslStream.push_chunk(data, timestamps);
        }
    }
}
