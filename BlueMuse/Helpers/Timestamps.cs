using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BlueMuse.Helpers
{
    public class Timestamps
    {
        // High precision, high accuracy function -> see https://msdn.microsoft.com/en-us/library/windows/desktop/hh706895(v=vs.85).aspx.
        // Also see http://www.windowstimestamp.com/description
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern void GetSystemTimePreciseAsFileTime(out long filetime);
        public static DateTimeOffset GetNow()
        {
            long fileTime;
            GetSystemTimePreciseAsFileTime(out fileTime);
            return DateTimeOffset.FromFileTime(fileTime);
        }
    }
}
