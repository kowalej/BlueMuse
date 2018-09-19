using BlueMuse.Helpers;
using System;
using System.Collections.Generic;
using NativeHelpers;

namespace BlueMuse.Misc
{
    public static class TimestampFormatsContainer
    {
        // Primary timestamp formats - no dummy "none" timestamp.
        public static List<BaseTimestampFormat> TimestampFormats = new List<BaseTimestampFormat>()
        {
            new BlueMuseUnixTimestampFormat(),
            new LSLLocalClockBlueMuseTimestampFormat(),
            new LSLLocalClockNativeTimestampFormat()
        };
        // Secondary timestamp formats - includes dummy "none" type since we can toggle it off.
        public static List<BaseTimestampFormat> TimestampFormats2 = new List<BaseTimestampFormat>()
        {
            new DummyTimestampFormat(),
            new BlueMuseUnixTimestampFormat(),
            new LSLLocalClockBlueMuseTimestampFormat(),
            new LSLLocalClockNativeTimestampFormat()
        };
    }

    public interface ITimestampFormat
    {
        /// <summary>
        /// Get current time in milliseconds.
        /// </summary>
        /// <returns>Current time in milliseconds</returns>
        double GetNow();
    }

    public abstract class BaseTimestampFormat : ITimestampFormat
    {
        public string DisplayName;
        public string Key;
        public virtual double GetNow()
        {
            throw new NotImplementedException();
        }
    }
    
    public sealed class BlueMuseUnixTimestampFormat : BaseTimestampFormat
    {
       public BlueMuseUnixTimestampFormat()
        {
            DisplayName = "BlueMuse High Accuracy (Unix Epoch Seconds UTC-0)";
            Key = Constants.TIMESTAMP_FORMAT_BLUEMUSE_UNIX;
        }
        public sealed override double GetNow()
        {
            return Timestamps.GetNow().ToUnixTimeMilliseconds() / 1000d; // Converted to seconds.
        }
    }

    public sealed class LSLLocalClockBlueMuseTimestampFormat : BaseTimestampFormat
    {
        public LSLLocalClockBlueMuseTimestampFormat()
        {
            DisplayName = "BlueMuse LSL Local Clock (System Uptime Seconds)";
            Key = Constants.TIMESTAMP_FORMAT_LSL_LOCAL_CLOCK;
        }
        public sealed override double GetNow() {
            return LSLLocalClock.GetNow();
        }
    }

    public sealed class LSLLocalClockNativeTimestampFormat : BaseTimestampFormat
    {
        public LSLLocalClockNativeTimestampFormat()
        {
            DisplayName = "Native LSL Local Clock - Via Bridge (System Uptime Seconds)";
            Key = Constants.TIMESTAMP_FORMAT_LSL_LOCAL_CLOCK;
        }
        public sealed override double GetNow()
        {
            return double.NegativeInfinity; // Spoofed value for now so LSL bridge knows to generate timestamps since I do not currently have a solution to call local_clock from UWP reliably.
        }
    }


    public sealed class DummyTimestampFormat : BaseTimestampFormat
    {
        public DummyTimestampFormat()
        {
            DisplayName = "None";
            Key = Constants.TIMESTAMP_FORMAT_NONE;
        }
        public sealed override double GetNow()
        {
            return 0;
        }
    }
}
