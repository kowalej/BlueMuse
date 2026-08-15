# BlueMuse Release Notes

Full version history for BlueMuse. See the [README](./README.md) for current features, installation, and usage instructions.

## 2.5.0.0 (stable)
* Modernized to .NET 10 / WinUI 3, converted to SDK-style project.
* Merged the separate "LSL Bridge" Win32 process directly into the main app (single-process architecture,
  see [Architecture](./README.md#architecture)).
* UI refresh: settings moved to a slide-out side panel, improved main list layout, compact per-stream
  info display with latest sample values and a one-click copy button.
* Added Esc key support to collapse/deselect the currently selected Muse in the list.
* Fixed intermittent Bluetooth/GATT communication issues (JSON parsing errors, spurious device removal,
  and connection churn) via reentrancy guards and per-device GATT serialization.
* Window size is now persisted between launches, and the window/taskbar title correctly shows "BlueMuse".

## 2.4.0.0 (stable)
* Misc package updates.
	* Support Windows 11.
	* Last release built on UWP before the .NET 10 / WinUI 3 modernization (see 2.5.0.0 above).

## Older
*Note: version 2.0.0.0 and older are available in the [DistArchived folder](https://github.com/kowalej/BlueMuse/tree/master/DistArchived). Version 2.0.0.0 and above are published to the [releases page](https://github.com/kowalej/BlueMuse/releases).*

### 2.3.0.0
* AUX supported on Muse 2.
* Refresh option available via command line.

### 2.2.0.0
* Bumped Windows platform version up to 10.0.19041.0.
* _May have with Bluetooth and stability issues._

### 2.1.0.0
* Detect Muse S as separate device (previously detected as Muse 2).
* Muse S - enable PPG.
* Handle more exceptions during stream shutdown.

### 2.0.0.0
* Stream PPG, accelerometer, gyroscope, and telemetry data.
* Muse 2 (and other model) auto detection. Removed "Assume Muse 2" setting.
* Battery level indicator in the UI.
* Added support for "tech info" which will show some device and control status information from the Muse. This data includes firmware information, serial number, battery info, and more.
* Added button to "hard reset" the Muse. *This can sometimes help resolve connectivity issues.*
* Cleaned up UI (improved button colours and important text is now bolded).
* Added a lot more logging for Bluetooth and other processing errors. This will hopefully lead to remaining issues being resolved in the future.
* Utilizing generated UWP package Install.ps1 Powershell install script (instead of calling Add-AppDevPackage directly).

### 1.1.1.0
* **Muse 2 support (experimental) - for now, for this to work you have to go to Settings > Assume Muse 2 > Toggle On. Finally, hit Force Refresh (if your Muse was already in the list, otherwise it should work when your device is first found).** It will assume all you devices with "Muse" in the name are Muse 2's and will set the parameters accordingly. *In the future I hope to have Muse vs Muse 2 differentiation be auto detected*.
* Added "always pair" option which may help with some people's Bluetooth issues. It is set as Off by default, you can toggle it On in the settings menu.

### 1.1.0.0
* Choose between 32-bit (float32) or 64-bit (double64) LSL stream data formats.

### 1.0.9.0 (Note - forces streams to use double64 data format.)
* Offering choice of timestamp format(s) (Unix Epoch or LSL local_clock).*
* Optionally send secondary timestamp (for comparison to primary timestamp) - sent as additional LSL channel.
* Improved UI to include settings menu. Settings menu allows user to choose timestamp formats and displays log file locations.
* Should automatically add firewall rules when LSLBridge launches for the first time.

### 1.0.8.0
* Increased timestamp accuracy by using a more precise API on Windows.
* Added logging. See Troubleshooting -> Logs section for details.
* LSLBridge won't falsely show stream if GATT problems occurred.

### 1.0.7.0
* Added new install script `InstallBlueMuse.ps1`.
* Refreshed the install certificate which was about to expire.

### 1.0.6.0 - stable
* Changed timestamp format to Unix epoch **seconds** format.
* Improved UI - it is now re-sizable and more compact (better for low resolution screens).
* Added version number to main screen.

### 1.0.5.0 - stable
* Corrected timestamps timezone issue (timestamps were meant to be GMT based, but were actually in EST). Timestamps formatted as Unix epoch **milliseconds**.

### 1.0.4.0 - stable
* LSLBridge is auto hidden if no streams active. BlueMuse also polls to keep LSL bridge open if not currently streaming, therefore LSLBridge has proper auto closing mechanism that won't prematurely trigger. This process may seem strange and convoluted but it appears to be the only good method to manage this trusted process with the current Windows UWP API.
* Bad timestamps.

### 1.0.3.0 - unstable
* Issues with LSLBridge closing.
* Bad timestamps.
