# 01-prerequisites: Verify environment and prepare

Review the BlueMuse.App and LSLBridge projects to understand the consolidation scope. Ensure NativeHelpers (C++ native interop) is available and compiling correctly. Verify .NET 10 SDK is installed.

**Assessment Context**: 
- BlueMuse.App has 29 issues (2 mandatory: SDK-style conversion, TFM update)
- LSLBridge has 188 issues (153 mandatory: mostly deprecated NuGet packages included in .NET 10)
- LSLBridge is a standalone utility app (WinExe) that displays LSL stream metadata — functionality to merge into BlueMuse.App
- 30 binding redirect issues across projects due to .NET Framework dependencies

**Known Risks**:
- WindowsRuntimeBuffer APIs removed in modern .NET (7 occurrences in BlueMuse.App/Muse.cs) — need replacement
- Deprecated Serilog.Sinks.RollingFile package — should migrate to Serilog.Sinks.File
- Large number of NuGet packages included in .NET 10 framework → will remove during consolidation
- Binding redirects in app.config files → will review and remove as part of SDK-style conversion

**Research Starting Points**:
- Review LSLBridge/LSL folder structure and LSLStreamManager logic — understand what needs to move into BlueMuse.App
- Review BlueMuse.App/Helpers and ViewModels — identify where LSL stream monitoring can be integrated
- Inventory all System.Net, System.IO, System.Collections packages in both projects (candidates for removal)
- Check for any configuration-specific logic in LSLBridge (settings, initialization) that must merge

## Research Findings

### Projects Affected
- **BlueMuse.App** — Primary app, will receive LSLBridge functionality
- **LSLBridge** — .NET 4.8 WPF application, being consolidated into BlueMuse.App
- **BlueMuse.wapproj** — UWP wrapper, will have LSLBridge reference removed
- **NativeHelpers** — C++ DLL providing native LSL interop (unchanged)

### LSLBridge Structure & Consolidation Scope

LSLBridge is organized as follows:
```
LSLBridge/
├── LSL/
│   ├── LSL.cs — LSL constants and low-level interop
│   ├── LSLStream.cs — Single stream wrapper
│   ├── LSLStreamInfo.cs — Stream metadata (name, type, channel count, rate)
│   ├── LSLStreamManager.cs — Central stream discovery/monitoring manager
│   └── (depends on liblsl32.dll, liblsl64.dll)
├── Helpers/
│   ├── ArrayConversion.cs — Byte array ↔ value conversion utilities
│   ├── CommandHandler.cs — ICommand pattern helper
│   ├── ObservableCollection.cs — Custom ObservableCollection variant
│   ├── ObservableObject.cs — INotifyPropertyChanged base class
│   ├── StreamHelper.cs — Stream-specific utilities
│   └── Converters.cs — XAML value converters
├── ViewModels/
│   └── MainWindowVM.cs — ViewModel for stream monitoring grid display
├── Windows/
│   ├── MainWindow.xaml — UI for stream monitor display grid
│   ├── MainWindow.xaml.cs — Code-behind for stream monitor
├── Binaries/
│   ├── liblsl32.dll, liblsl64.dll — LSL library binaries
│   ├── msvcp90.dll, msvcr90.dll — VC++ runtime dependencies
├── App.config — Contains 6 binding redirects (Serilog, System.* assemblies)
├── App.xaml — Application startup configuration
├── App.xaml.cs — Application event handlers
└── packages.config — Old-style NuGet references (~20+ packages)
```

**To merge into BlueMuse.App**:
1. **LSL/* folder** — Move entirely to BlueMuse.App/LSL/
2. **Helpers/* files** — Merge with BlueMuse.App/Helpers/ (check for conflicts with existing helpers like CommandHandler, ObservableCollection, ObservableObject)
3. **Binaries/* folder** — Move LSL DLLs to BlueMuse.App/Binaries/
4. **ViewModels/MainWindowVM.cs** — Move to BlueMuse.App/ViewModels/
5. **MainWindow UI** — Integrate stream monitoring UI into BlueMuse.App/Pages/ (combine with or replace existing MainPage if appropriate)
6. **App.config binding redirects** — Document before removal (will be eliminated after SDK-style conversion)
7. **Namespace updates** — Change `LSLBridge.*` → `BlueMuse.App.LSL.*` and `BlueMuse.App.Helpers.*`

**NOT merging**:
- LSLBridge App.xaml (BlueMuse.App already has its own App.xaml)
- LSLBridge packages.config (will be migrated during SDK-style conversion in task 03)

### Environment Verification

✅ **.NET 10 SDK Installed**: Confirmed (SDK Version 10.0.302)  
✅ **NativeHelpers Builds**: Confirmed with MSBuild (produces NativeHelpers.dll)  
✅ **Visual Studio Community 2026 (18.8.2)**: Ready

### Binding Redirects Identified

**LSLBridge/App.config** contains 6 binding redirects:
1. `Serilog.Sinks.File` 0.0.0.0-7.0.0.0 → 7.0.0.0
2. `System.Diagnostics.DiagnosticSource` 0.0.0.0-10.0.0.10 → 10.0.0.10
3. `System.Numerics.Vectors` 0.0.0.0-4.1.6.0 → 4.1.6.0
4. `System.Runtime.CompilerServices.Unsafe` 0.0.0.0-6.0.3.0 → 6.0.3.0
5. `Serilog` 0.0.0.0-4.4.0.0 → 4.4.0.0
6. `System.Memory` 0.0.0.0-4.0.5.0 → 4.0.5.0

**Status**: Standard binding redirects for Framework libraries → will be documented and removed during SDK-style conversion (task 03).

### Files & Namespaces to Move

| File Path | New Location | Namespace Change |
|-----------|--------------|------------------|
| LSLBridge/LSL/*.cs | BlueMuse.App/LSL/ | `LSLBridge.LSL` → `BlueMuse.App.LSL` |
| LSLBridge/Helpers/*.cs | BlueMuse.App/Helpers/ | `LSLBridge.Helpers` → `BlueMuse.App.Helpers` |
| LSLBridge/ViewModels/MainWindowVM.cs | BlueMuse.App/ViewModels/ | `LSLBridge.ViewModels` → `BlueMuse.App.ViewModels` |
| LSLBridge/Windows/MainWindow.xaml* | BlueMuse.App/Pages/ or merge | Update x:Class, xmlns, bindings |
| LSLBridge/Binaries/*.dll | BlueMuse.App/Binaries/ | (no namespace change) |

**Done when**: 
- Environment verified (.NET 10 SDK installed, Visual Studio ready) ✅
- Consolidation scope documented (which files/classes move from LSLBridge → BlueMuse.App) ✅
- NativeHelpers builds without errors ✅
- All binding redirect files (.config) identified and reviewed ✅
