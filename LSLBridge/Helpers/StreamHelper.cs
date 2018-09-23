using LSL;
using LSLBridge.LSL;

namespace LSLBridge.Helpers
{
    public static class StreamHelper
    {
        // Get timestamps for a chunk using LSL local_clock.
        public static double[] GenerateLSLNativeTimestamps(LSLBridgeStreamInfo streamInfo)
        {
            int chunkSize = streamInfo.ChunkSize;
            double sampleTimeSeconds = 1.0d / streamInfo.NominalSRate;
            double[] timestamps = new double[chunkSize];
            double baseSeconds = liblsl.local_clock(); // local_clock in seconds.

            for (int i = 0; i < streamInfo.ChunkSize; i++)
            {
                timestamps[i] = baseSeconds - ((chunkSize - i) * sampleTimeSeconds); // Offset times based on sample rate.
                timestamps[i] = timestamps[i]; // Convert to seconds, as this is a more standard timestamp format.
            }

            return timestamps;
        }
    }
}
