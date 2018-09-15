using BlueMuse.Helpers;
using System;

namespace BlueMuse.Misc
{
    public interface ITimestampFormat
    {
        /// <summary>
        /// Get current time in milliseconds.
        /// </summary>
        /// <returns>Current time in milliseconds</returns>
        long GetNow();
    }

    public abstract class BaseTimestampFormat : ITimestampFormat
    {
        public string DisplayName;
        public string Key;
        public virtual long GetNow()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
    
    public class BlueMuseUnixTimestampFormat : BaseTimestampFormat
    {
       public BlueMuseUnixTimestampFormat()
        {
            DisplayName = "BlueMuse High Accuracy (Unix Epoch UTC±00:00)";
            Key = nameof(BlueMuseUnixTimestampFormat);
        }
        public override long GetNow()
        {
            return Timestamps.GetNow().ToUnixTimeMilliseconds();
        }
    }

    public class LSLTimestampFormat : BaseTimestampFormat
    {
        public LSLTimestampFormat()
        {
            DisplayName = "LSL Local Clock (system uptime).";
            Key = nameof(LSLTimestampFormat);
        }
        public override long GetNow()
        {
            return (long)LSL.liblsl.local_clock();
        }
    }
}
