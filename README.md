# BlueMuse
* Windows app to stream data from Muse EEG headsets via LSL (Lab Streaming Layer).

# Features
* Auto detects Muse headsets and provides a visual interface to manage streams.
* Supports Muse 2016, Muse 2, Muse S, and Smith Lowdown Focus glasses (device models are auto detected).
* Supports EEG, PPG, accelerometer, gyroscope, and telemetry data. *Note: PPG is only available on Muse 2 and Muse S*.
* Can stream from multiple Muses simultaneously (see notes).
* Choose between timestamp formats - LSL "local clock" or Unix Epoch.
* LSL streams in 64-bit or 32-bit.
* Shows latest timestamp received and the current sample rate for each stream.
* Protocol control from Command Line / PowerShell.

# Screenshots
![Screenshots of BlueMuse Desktop App, Main Page and List Items](./screenshots.png "Screenshots")


# Command Line Interface
**All commands will launch BlueMuse if it isn't already open.**

### Basic Operations
Start BlueMuse:
```powershell
start bluemuse:
```
Refresh Bluetooth list (used only while running): 
```powershell
start bluemuse://refresh
```
Close the program: 
```powershell
start bluemuse://shutdown
```

### Streaming
Start streaming first connected (online) Muse: 
```powershell
start bluemuse://start?streamfirst=true
```
Start streaming specific Muse(s) - by MAC address or device name: 
```powershell
start bluemuse://start?addresses={MAC1 or Name1},{MAC2 or Name2},{MAC3 or Name3}....
```
Start streaming all Muses: 
```powershell
start bluemuse://start?startall
```
Stop streaming specific Muse(s) - by MAC address or device name: 
```powershell
start bluemuse://stop?addresses={MAC1 or Name1},{MAC2 or Name2},{MAC3 or Name3},....
```
Stop streaming all Muses: 
```powershell
start bluemuse://stop?stopall
```

**"startall" can be used at launch (e.g. `start bluemuse://start?startall`) or while BlueMuse is already running -
it will automatically apply to any Muse discovered/connected after the command is received. "stopall" is intended
for use only while BlueMuse is already running.**

### Settings
Change primary timestamp format: 
```powershell
 start bluemuse://setting?key=primary_timestamp_format!value=<BLUEMUSE|LSL_LOCAL_CLOCK_BLUEMUSE|LSL_LOCAL_CLOCK_NATIVE>
```
Change secondary timestamp format: 
```powershell
 start bluemuse://setting?key=secondary_timestamp_format!value=<BLUEMUSE|LSL_LOCAL_CLOCK_BLUEMUSE|LSL_LOCAL_CLOCK_NATIVE|NONE>
```
Change channel data type: 
```powershell
 start bluemuse://setting?key=channel_data_type!value=<float32|double64>
```
Enable / disable EEG data (applies when opening streams):
```powershell
 start bluemuse://setting?key=eeg_enabled!value=<true|false>
```
Enable / disable accelerometer data (applies when opening streams):
```powershell
 start bluemuse://setting?key=accelerometer_enabled!value=<true|false>
```
Enable / disable gyroscope data (applies when opening streams):
```powershell
 start bluemuse://setting?key=gyroscope_enabled!value=<true|false>
```
Enable / disable PPG data (applies when opening streams):
```powershell
 start bluemuse://setting?key=ppg_enabled!value=<true|false>
```
Enable / disable telemetry data (applies when opening streams):
```powershell
 start bluemuse://setting?key=telemetry_enabled!value=<true|false>
```
Toggle "always pair": 
```powershell
 start bluemuse://setting?key=always_pair!value=<true|false>
```

# Installation
> **NOTE:** As of version 2.5.0.0 (the .NET 10 / WinUI 3 modernization), BlueMuse is now a single self-contained
> MSIX-packaged application (the separate "LSL Bridge" Win32 process used in 2.4.0.0 and earlier has been merged
> directly into the main app - see the [Architecture](#architecture) note below). The distribution method
> (Microsoft Store vs. sideload) is **still being finalized** - this section will be updated once that is
> confirmed. In the meantime, sideloading as described below will continue to work.

***Requires Windows 10 version 1809 (10.0.17763.0) or later, or Windows 11. Built/targeted against the Windows 11 24H2 SDK (10.0.26100.0).***

### Sideload Install (Current Method)
1. **Download the [latest release](https://github.com/kowalej/BlueMuse/releases)** and unzip it.
2. Double-click the `.cer` certificate file included in the release and install it to the **Local Machine** ->
   **Trusted Root Certification Authorities** store (you'll need administrator rights for this step).
3. Double-click the `.msix` / `.msixbundle` package to install BlueMuse.
4. If Windows blocks the install because "Developer Mode" or sideloading isn't enabled, turn on
   **Settings -> Update & Security -> For Developers -> Developer Mode** (or **Sideload apps**) and retry.

### Microsoft Store (Possible In Future)
A Microsoft Store listing is being evaluated for a future release, which would remove the need for manual
certificate/sideload steps entirely. This section will be updated if a store link becomes available.

<details>
<summary><strong>Legacy Install Instructions (2.4.0.0 and earlier, UWP + separate LSL Bridge)</strong></summary>

*The instructions below apply to older releases (2.4.0.0 and earlier), which shipped as a UWP app paired with
a separate "LSL Bridge" Win32 process and .NET Native runtime dependencies. Kept here for reference in case
you need to install an older version.*

***Requires Windows 10 with Fall 2017 Creators Update - Version 10.0.15063 aka Windows 10 (1703).***

#### First Step
**Download [latest version](https://github.com/kowalej/BlueMuse/releases/download/v2.4.0.0/BlueMuse_2.4.0.0.zip) from the [releases page](https://github.com/kowalej/BlueMuse/releases)** and unzip, then follow one of the methods below.
#### Auto Install (Recommended)
1. Navigate to the unzipped app folder and run the `.\InstallBlueMuse.ps1` PowerShell command (right click and choose Run with PowerShell or execute from terminal directly): 

2. Follow the prompts - the script should automatically install the security certificate, all dependencies, and the BlueMuse app.

#### Manual Install
1. Double click BlueMuse_xxx.cer then click "Install Certificate".
2. Select current user or local machine depending on preference and click "Next".
3. Select "Place all certificates in the following store".
4. Press "Browse...".
5. Select install for Local Machine.
6. Select "Trusted Root Certification Authorities" and click "OK".
7. Click "Next" and click "Finish" to install certificate.

8. Open Dependencies folder and appropriate folder for your machine architecture.
9. Double click and install Microsoft.NET.Native.Framework.1.7 and Microsoft.NET.Native.Runtime.1.7.

10. Finally, double click and install BlueMuse_xxx.appxbundle.
</details>
<br>

# Versions
### Latest
* 2.5.0.0 (stable)
	* Modernized to .NET 10 / WinUI 3, converted to SDK-style project.
	* Merged the separate "LSL Bridge" Win32 process directly into the main app (single-process architecture,
	  see [Architecture](#architecture)).
	* UI refresh: settings moved to a slide-out side panel, improved main list layout, compact per-stream
	  info display with latest sample values and a one-click copy button.
	* Single-instance app: the `bluemuse://` command line interface now redirects into the already-running
	  instance instead of launching a duplicate window, and `startall` correctly applies to Muses discovered
	  after the command is sent.
	* Added Esc key support to collapse/deselect the currently selected Muse in the list.
	* Fixed intermittent Bluetooth/GATT communication issues (JSON parsing errors, spurious device removal,
	  and connection churn) via reentrancy guards and per-device GATT serialization.
	* Window size is now persisted between launches, and the window/taskbar title correctly shows "BlueMuse".
* 2.4.0.0 (stable)
	* Misc package updates.
		* Support Windows 11.
		* Last release built on UWP before the .NET 10 / WinUI 3 modernization (see 2.5.0.0 above).

See [CHANGELOG.md](./CHANGELOG.md) for the full version history.


# Notes
* **Requires Windows 10 version 1809 (10.0.17763.0) or later, or Windows 11. Built/targeted against the Windows 11 24H2 SDK (10.0.26100.0).**
* **Streaming multiple Muses simultaneously -** maintaining consistent data rates for multiple devices may be difficult on some machines, depending on Bluetooth and compute hardware.
* Uses both 32-bit and 64-bit LSL binaries (liblsl32.dll / liblsl64.dll), selected automatically at runtime based on process architecture. Acquired from: ftp://sccn.ucsd.edu/pub/software/LSL/SDK/liblsl-All-Languages-1.11.zip
* liblsl32.dll and liblsl64.dll are dependent on MSVCP90.dll and MSVCR90.dll, both of which I included in the project since these may not be available in the System32 folder on your machine (they weren't on mine).
* The full dependencies of liblsl32.dll are: KERNEL32.dll, WINMM.dll, MSVCP90.dll, WS2_32.dll, MSWSOCK.dll, and MSVCR90.dll. Generated with dumpbin utility.

### Architecture
BlueMuse previously ran the LSL streaming logic in a separate "LSL Bridge" Win32 process, because UWP apps ran
in a network-isolated sandbox that prevented LSL streams from being visible on the local network. As of the
.NET 10 / WinUI 3 modernization, BlueMuse runs as a single unpackaged-network-capable desktop app, and the LSL
streaming logic (`LSLStreamManager`) now runs in-process - there is no longer a separate bridge executable, and
no firewall exception is required for a second process.

### Timestamp Formats:

* BlueMuse High Accuracy (Unix Epoch Seconds UTC-0)
    * Recommended as primary timestamp if you don't have to time sync with other LSL streams.
    * Generates timestamps that you can use to determine date and very accurate time.
    * Settings value = BLUEMUSE.
* BlueMuse LSL Local Clock (System Uptime Seconds)
    * Recommended if syncing with other LSL streams.
    * Called exactly when packet comes in from Muse.
    * Generates timestamp by calling an equivalent function to LSL local_clock but utilizes the C++ Standard Library instead of the underlying Boost library call used by the LSL .dll.
    * Should produce the exact same value as LSL native local clock with less jitter.
    * Settings value = LSL_LOCAL_CLOCK_BLUEMUSE.
* Native LSL Local Clock - Via Bridge (System Uptime Seconds)
    * Generates timestamp by calling local_clock function from LSL .dll function directly on bridge.
    * May produce jitter.
    * Settings value = LSL_LOCAL_CLOCK_NATIVE.
* None
    * Don't send secondary timestamp.
    * Settings value = NONE.


# Troubleshooting
### If your Muse is not showing up after searching for awhile: 
  1. Ensure Muse is removed from "Bluetooth & other devices" list in control panel.
  2. Reset Muse - hold down power button until device turns off then back on.
  3. Make sure Muse is within reasonable range of your computer. Some built in Bluetooth antennas are not very powerful.
  
### Logs:
BlueMuse writes a log file for various events and exceptions, which may help in troubleshooting issues. The file can be found within AppData:

*C:\Users\\{Username}\AppData\Local\Packages\\{PackageFamilyName}\LocalState\Logs\BlueMuse-Log-{Timestamp}.log*

You can also open the log folder directly from the app via **Settings -> Open Log Folder**.
