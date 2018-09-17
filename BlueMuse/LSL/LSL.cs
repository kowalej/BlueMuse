using System;
using System.Runtime.InteropServices;

/**
* C# API for the lab streaming layer.
* 
* The lab streaming layer provides a set of functions to make instrument data accessible 
* in real time within a lab network. From there, streams can be picked up by recording programs, 
* viewing programs or custom experiment applications that access data streams in real time.
*
* The API covers two areas:
* - The "push API" allows to create stream outlets and to push data (regular or irregular measurement 
*   time series, event data, coded audio/video frames, etc.) into them.
* - The "pull API" allows to create stream inlets and read time-synched experiment data from them 
*   (for recording, viewing or experiment control).
*
*/
namespace LSL
{
    public class liblsl
    {
        /**
        * Obtain a local system time stamp in seconds. The resolution is better than a millisecond.
        * This reading can be used to assign time stamps to samples as they are being acquired. 
        * If the "age" of a sample is known at a particular time (e.g., from USB transmission 
        * delays), it can be used as an offset to local_clock() to obtain a better estimate of 
        * when a sample was actually captured. See stream_outlet::push_sample() for a use case.
        */
        public static double local_clock() { return SafeNativeMethods.lsl_local_clock(); }

        // === Internal: C library function definitions. ===
        class SafeNativeMethods
        {
            /// Name of the binary to include -- replace this if you are on a non-Windows platform (e.g., liblsl64.so)
            const string libname = "liblsl32.dll";

            [DllImport(libname, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true, BestFitMapping = false, ExactSpelling = true)]
            public static extern double lsl_local_clock();
        }
    }
}
