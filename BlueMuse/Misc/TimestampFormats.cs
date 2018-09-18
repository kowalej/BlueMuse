using BlueMuse.Helpers;
using System;
using System.Collections.Generic;

namespace BlueMuse.Misc
{
    public static class TimestampFormatsContainer
    {
        // Primary timestamp formats - no dummy "none" timestamp.
        public static List<BaseTimestampFormat> TimestampFormats = new List<BaseTimestampFormat>()
        {
            new BlueMuseUnixTimestampFormat(),
            new LSLLocalClockTimestampFormat()
        };
        // Secondary timestamp formats - includes dummy "none" type since we can toggle it off.
        public static List<BaseTimestampFormat> TimestampFormats2 = new List<BaseTimestampFormat>()
        {
            new DummyTimestampFormat(),
            new BlueMuseUnixTimestampFormat(),
            new LSLLocalClockTimestampFormat()
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
            return Timestamps.GetNow().ToUnixTimeMilliseconds();
        }
    }

    public sealed class LSLLocalClockTimestampFormat : BaseTimestampFormat
    {
        public LSLLocalClockTimestampFormat()
        {
            DisplayName = "LSL Local Clock (System Uptime Seconds)";
            Key = Constants.TIMESTAMP_FORMAT_LSL_LOCAL_CLOCK;
        }
        public sealed override double GetNow()
        {
            return LSLClock.GetNow() * 1000;
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
