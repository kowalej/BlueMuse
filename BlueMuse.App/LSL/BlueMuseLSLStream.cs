using BlueMuse.Helpers;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace BlueMuse.LSL
{
    public class BlueMuseLSLStream : ObservableObject, IDisposable
    {
        private StreamOutlet lslStream;

        private BlueMuseLSLStreamInfo streamInfo;
        public BlueMuseLSLStreamInfo StreamInfo { get { return streamInfo; } private set { SetProperty(ref streamInfo, value); } }

        public string StreamDisplayInfo
        {
            get
            {
                return string.Format(
                    "{0} ({1} ch) @ {2,3:0} Hz nominal / {3,6:0.00} Hz live" + Environment.NewLine + "  Channels: {4}" + Environment.NewLine + "  Latest Values: {5}" + Environment.NewLine + "  LSL Stream Name: {6}",
                    streamInfo.StreamType,
                    streamInfo.ChannelCount,
                    streamInfo.NominalSRate,
                    rate,
                    string.Join(", ", streamInfo.Channels.Select(x => x.Label).ToList()),
                    latestValues ?? "n/a",
                    streamInfo.StreamName);
            }
        }

        private double latestTimestamp;
        public double LatestTimestamp { get { return latestTimestamp; } set { SetProperty(ref latestTimestamp, value); } }

        // Most recent sample's per-channel values, formatted for display (e.g. "1.23, 4.56, 7.89").
        private string latestValues;
        public string LatestValues { get { return latestValues; } set { SetProperty(ref latestValues, value); OnPropertyChanged(nameof(StreamDisplayInfo)); } }

        // Live rate update.
        private double rate = 0;
        public double Rate { get { return rate; } set { SetProperty(ref rate, value); OnPropertyChanged(nameof(StreamDisplayInfo)); } }

        private Stopwatch stopWatch;
        int sampleCountSec = 0;

        public BlueMuseLSLStream(BlueMuseLSLStreamInfo streamInfo)
        {
            StreamInfo = streamInfo;
            channel_format_t channelFormat;
            
            if (streamInfo.ChannelDataType == BlueMuseLSLDataType.FLOAT)
            {
                channelFormat = channel_format_t.cf_float32;
            }
            else if (streamInfo.ChannelDataType == BlueMuseLSLDataType.DOUBLE)
            {
                channelFormat = channel_format_t.cf_double64;
            }
            else if (streamInfo.ChannelDataType == BlueMuseLSLDataType.INT)
            {
                channelFormat = channel_format_t.cf_int32;
            }
            else if (streamInfo.ChannelDataType == BlueMuseLSLDataType.STRING)
            {
                channelFormat = channel_format_t.cf_string;
            }
            else
            {
                throw new InvalidOperationException("Unsupported channel data type.");
            }

            var lslStreamInfo = new StreamInfo(streamInfo.StreamName, streamInfo.StreamType, streamInfo.ChannelCount, streamInfo.NominalSRate, channelFormat, Assembly.GetExecutingAssembly().GetName().Name);
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
            lslStream = new StreamOutlet(lslStreamInfo, streamInfo.ChunkSize, streamInfo.BufferLength);
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
                var elapsedAdjusted = 1000d / elapsed;
                Rate = sampleCountSec * elapsedAdjusted;
                sampleCountSec = 0;
                stopWatch.Restart();
            }
        }

        // Only double[] and float[] chunks can support appending secondary timestamp.
        public void PushChunkLSL(float[,] data, double[] timestamps, double[] timestamps2 = null)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            LatestValues = FormatLatestValues(data);
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
                LogChunkSanityCheck("float+secondary", dataRevised, timestamps);
                lslStream.push_chunk(dataRevised, timestamps);
            }
            else
            {
                LogChunkSanityCheck("float", data, timestamps);
                lslStream.push_chunk(data, timestamps);
            }
        }

        // Only double[] and float[] chunks can support appending secondary timestamp.
        public void PushChunkLSL(double[,] data, double[] timestamps, double[] timestamps2 = null)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            LatestValues = FormatLatestValues(data);
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
                LogChunkSanityCheck("double+secondary", dataRevised, timestamps);
                lslStream.push_chunk(dataRevised, timestamps);
            }
            else
            {
                LogChunkSanityCheck("double", data, timestamps);
                lslStream.push_chunk(data, timestamps);
            }
        }

        public void PushChunkLSL(int[,] data, double[] timestamps)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            LatestValues = FormatLatestValues(data);
            LogChunkSanityCheck("int", data, timestamps);
            lslStream.push_chunk(data, timestamps);
        }

        public void PushChunkLSL(string[,] data, double[] timestamps)
        {
            LatestTimestamp = timestamps[timestamps.Length - 1];
            LatestValues = FormatLatestValues(data);
            LogChunkSanityCheck("string", data, timestamps);
            lslStream.push_chunk(data, timestamps);
        }

        // Sanity-check logging: logs sample counts, first/last timestamp, a preview of the first row of data,
        // and whether LSL currently has any consumers connected (a common reason data doesn't show up downstream
        // even though push_chunk succeeds - no inlet/recorder is actually connected/pulling).
        private void LogChunkSanityCheck<T>(string variant, T[,] data, double[] timestamps)
        {
            if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Debug)) return;

            int rowCount = data.GetLength(0);
            int colCount = data.GetLength(1);
            string firstRowPreview = null;
            if (rowCount > 0)
            {
                var values = new string[colCount];
                for (int col = 0; col < colCount; col++) values[col] = data[0, col]?.ToString() ?? "null";
                firstRowPreview = string.Join(", ", values);
            }

            bool hasConsumers = lslStream.have_consumers();

            Log.Debug("PushChunkLSL [{Variant}] Stream: '{StreamName}', Rows: {RowCount}, Cols: {ColCount}, FirstTimestamp: {FirstTs}, LastTimestamp: {LastTs}, FirstRow: [{FirstRow}], HasConsumers: {HasConsumers}",
                variant, streamInfo?.StreamName, rowCount, colCount,
                timestamps.Length > 0 ? timestamps[0] : (double?)null,
                timestamps.Length > 0 ? timestamps[timestamps.Length - 1] : (double?)null,
                firstRowPreview, hasConsumers);

            if (!hasConsumers)
            {
                Log.Warning("PushChunkLSL [{Variant}] Stream: '{StreamName}' has NO consumers connected - data is being pushed but nothing is receiving it.", variant, streamInfo?.StreamName);
            }
        }

        // Extracts and formats the last row (most recent sample) of a 2D chunk array for display purposes.
        private static string FormatLatestValues(float[,] data)
        {
            int lastRow = data.GetLength(0) - 1;
            if (lastRow < 0) return null;
            var values = new string[data.GetLength(1)];
            for (int col = 0; col < data.GetLength(1); col++) values[col] = data[lastRow, col].ToString("0.00");
            return string.Join(", ", values);
        }

        private static string FormatLatestValues(double[,] data)
        {
            int lastRow = data.GetLength(0) - 1;
            if (lastRow < 0) return null;
            var values = new string[data.GetLength(1)];
            for (int col = 0; col < data.GetLength(1); col++) values[col] = data[lastRow, col].ToString("0.00");
            return string.Join(", ", values);
        }

        private static string FormatLatestValues(int[,] data)
        {
            int lastRow = data.GetLength(0) - 1;
            if (lastRow < 0) return null;
            var values = new string[data.GetLength(1)];
            for (int col = 0; col < data.GetLength(1); col++) values[col] = data[lastRow, col].ToString();
            return string.Join(", ", values);
        }

        private static string FormatLatestValues(string[,] data)
        {
            int lastRow = data.GetLength(0) - 1;
            if (lastRow < 0) return null;
            var values = new string[data.GetLength(1)];
            for (int col = 0; col < data.GetLength(1); col++) values[col] = data[lastRow, col];
            return string.Join(", ", values);
        }
    }
}
