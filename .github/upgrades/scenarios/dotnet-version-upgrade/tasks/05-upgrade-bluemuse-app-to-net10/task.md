# 05-upgrade-bluemuse-app-to-net10: Update BlueMuse.App target framework to .NET 10

Change BlueMuse.App's TargetFramework property from .NET 5.0 to net10.0. Update all deprecated packages and remove NuGet packages now included in .NET 10.

**Assessment Context**:
- BlueMuse.App has 29 issues:
  - 2 mandatory: SDK-style conversion (done in task 03), TFM update (this task)
  - 7 API issues: WindowsRuntimeBuffer removed (must replace with modern Windows.Storage.Streams APIs)
  - Package issues: Serilog.Sinks.RollingFile deprecated, Microsoft.NETCore.UniversalWindowsPlatform obsolete
- Target: net10.0-windows10.0.26100.0 (modern Windows desktop)

**Known Risks**:
- WindowsRuntimeBuffer API removal (7 occurrences) requires code changes in Muse.cs and PacketConversion.cs
- Microsoft.NETCore.UniversalWindowsPlatform (UWP NuGet) removed → requires Windows App SDK or compatibility packages
- Behavioral changes in System.Uri class — review protocol handling in App.xaml.cs
- Missing binding redirects after removing old packages → may surface hidden version conflicts

**Research Starting Points**:
- Migrate WindowsRuntimeBuffer → Windows.Storage.Streams.IBuffer (available via Windows App SDK or native APIs)
- Evaluate Windows App SDK 2.3.1 vs compatibility packages for UWP features
- Review each of the 7 WindowsRuntimeBuffer usages — understand what each does (buffer creation, byte access, array conversion)
- Check System.Uri behavioral changes in .NET 10

**Done when**:
- TargetFramework set to net10.0
- All packages updated to latest versions compatible with .NET 10
- WindowsRuntimeBuffer usages replaced with modern APIs (no compilation errors)
- Solution builds without errors
- Unit tests pass (if any)
