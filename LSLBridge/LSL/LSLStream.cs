using LSL;
using LSLBridge.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace LSLBridge.LSL
{
    public class LSLStream : ObservableObject, IDisposable
    {
        private liblsl.StreamOutlet lslStream;

        private LSLBridgeStreamInfo streamInfo;
        public LSLBridgeStreamInfo StreamInfo { get { return streamInfo; } private set { SetProperty(ref streamInfo, value); } }

        public string StreamDisplayInfo { get { return string.Format("Name: {0} - Nominal Rate: {1} - Channels ({2}): {3}", streamInfo.StreamName, streamInfo.NominalSRate, streamInfo.ChannelCount, string.Join(",", streamInfo.Channels.Select(x => x.Label).ToList())); } }

        private double latestTimestamp;
        public double LatestTimestamp { get { return latestTimestamp; } set { SetProperty(ref latestTimestamp, value); } }

        // Live rate update.
        private int rate = 0;
        public int Rate { get { return rate; } set { SetProperty(ref rate, value); } }

        private Stopwatch stopWatch;
        int sampleCountSec = 0;

        public LSLStream(LSLBridgeStreamInfo streamInfo)
        {
            StreamInfo = streamInfo;
            liblsl.channel_format_t channelFormat;
            
            if (streamInfo.ChannelDataType == LSLBridgeDataType.FLOAT)
            {
                channelFormat = liblsl.channel_format_t.cf_float32;
            }
            else if (streamInfo.ChannelDataType == LSLBridgeDataType.DOUBLE)
            {
                channelFormat = liblsl.channel_format_t.cf_double64;
            }
            else if (streamInfo.ChannelDataType == LSLBridgeDataType.INT)
            {
                channelFormat = liblsl.channel_format_t.cf_int32;
            }
            else if (streamInfo.ChannelDataType == LSLBridgeDataType.STRING)
            {
                channelFormat = liblsl.channel_format_t.cf_string;
            }
            else
            {
                throw new InvalidOperationException("Unsupported channel data type.");
            }

            var lslStreamInfo = new liblsl.StreamInfo(streamInfo.StreamName, streamInfo.StreamType, streamInfo.ChannelCount, streamInfo.NominalSRate, channelFormat, Application.ResourceAssembly.GetName().Name);
            lslStreamInfo.desc().append_child_value("manufacturer", streamInfo.DeviceManufacturer);
            lslStreamInfo.desc().append_child_value("device", streamInfo.DeviceName);
            lslStreamInfo.desc().append_child_value("type", streamInfo.StreamType);
            var channels = lslStreamInfo.desc().append_child("channels");
            foreach (var c in streamInfo.Channels)
            {
                channels.append_child("channel")
                .append_child_value("label", c.Label)
                .append_child_value("unit", c.Unit)
                .append_child_value("type", c.Type);
            }

            OnPropertyChanged(nameof(StreamDisplayInfo));
            lslStream = new liblsl.StreamOutlet(lslStreamInfo, streamInfo.ChunkSize, streamInfo.BufferLength);
            stopWatch = new Stopwatch();
            stopWatch.Restart();
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
                if(lslStream != null)
                    lslStream.Dispose(); // Destory lsl native stream outlet.
            }

            // Free any unmanaged objects here.
            //

            disposed = true;
        }

        public void UpdateSampleRate(int timestampsLength)
        {
            // Update sample rate.
            sampleCountSec += timestampsLength;
            if (stopWatch.ElapsedMilliseconds >= 1000)
            {
                var elapsed = stopWatch.ElapsedMilliseconds;
                var elapsedAdjusted = 1000f / elapsed;
                Rate = (int)(sampleCountSec * elapsedAdjusted);
                sampleCountSec = 0;
                stopWatch.Restart();
            }
        }

        // Only double[] and float[] chunks can support appending secondary timestamp.
        public void PushChunkLSL(float[,] data, double[] timestamps, double[] timestamps2 = null)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            if (timestamps2 != null) // Append timestamp data to final column.
            {
                float[,] dataRevised = new float[data.GetLength(0), data.GetLength(1) + 2]; // Add extra 2 columns for timestamp.
                int lastColIndex = data.GetLength(1) + 1;
                for (int rowIndex = 0; rowIndex < data.GetLength(0); rowIndex++)
                {
                    for (int colIndex = 0; colIndex < data.GetLength(1); colIndex++)
                    {
                        dataRevised[rowIndex, colIndex] = data[rowIndex, colIndex];
                    }
                    float timestampBase = (float)timestamps2[rowIndex];
                    float remainder = (float)(timestamps2[rowIndex] - timestampBase);
                    dataRevised[rowIndex, lastColIndex - 1] = timestampBase;
                    dataRevised[rowIndex, lastColIndex] = remainder;
                }
                lslStream.push_chunk(dataRevised, timestamps);
            }
            else lslStream.push_chunk(data, timestamps);
        }

        // Only double[] and float[] chunks can support appending secondary timestamp.
        public void PushChunkLSL(double[,] data, double[] timestamps, double[] timestamps2 = null)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            if (timestamps2 != null) // Append timestamp data to final column.
            {
                double[,] dataRevised = new double[data.GetLength(0), data.GetLength(1) + 1]; // Add extra column for timestamp.
                int lastColIndex = data.GetLength(1);
                for (int rowIndex = 0; rowIndex < data.GetLength(0); rowIndex++)
                {
                    for (int colIndex = 0; colIndex < data.GetLength(1); colIndex++)
                    {
                        dataRevised[rowIndex, colIndex] = data[rowIndex, colIndex];
                    }
                    dataRevised[rowIndex, lastColIndex] = timestamps2[rowIndex];
                }
                lslStream.push_chunk(dataRevised, timestamps);
            }
            else lslStream.push_chunk(data, timestamps);
        }

        public void PushChunkLSL(int[,] data, double[] timestamps)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            lslStream.push_chunk(data, timestamps);
        }
        
        public void PushChunkLSL(string[,] data, double[] timestamps)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            lslStream.push_chunk(data, timestamps);
        }
    }
}
