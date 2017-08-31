# BlueMuse
* Windows 10 UWP app to stream data from Muse EEG headsets via LSL (Lab Streaming Layer).

# Features
* Auto detects Muse headsets and provides a visual interface to open and close data streams. 
* Can stream from multiple Muses simultaneously.
* Shows latest timestamp received and the current sample rate for each stream.

# Notes
* Requires Windows 10 with creators update (build 10.0.15063.0).
* Application requires side loading a Win32 application which does the LSL streaming. This is because UWP apps run in a restricted environment with network isolation. This prevented LSL streams from being seen across the local network. To get around this issue, the data is shuffled through the "LSL Bridge", a Win32 application which can run in a normal environment. Note: when you first start a stream, you may need to add a firewall exception for LSLBridge.exe.
* Uses 32-bit binaries for LSL. Acquired from: ftp://sccn.ucsd.edu/pub/software/LSL/SDK/liblsl-All-Languages-1.11.zip
* liblsl32.dll was dependent on MSVCP90.dll and MSVCR90.dll, both of which I included in the project since these may not be available in the System32 folder on your machine (they weren't on mine).
* The full dependencies of liblsl32.dll are: KERNEL32.dll, WINMM.dll, MSVCP90.dll, WS2_32.dll, MSWSOCK.dll, and MSVCR90.dll. Generated with dumpbin utility.

## If your Muse is not showing up after searching for awhile: 
  1. Ensure Muse is removed from "Bluetooth & other devices" list in control panel.
  2. Reset Muse - hold down power button until device turns off then back on.
  3. Make sure Muse is within reasonable range of your computer. Some built in Bluetooth antennas are not very powerful.