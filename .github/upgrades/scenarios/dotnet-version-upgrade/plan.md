# Upgrade Plan — BlueMuse to .NET 10

**Strategy**: All-at-Once  
**Target Framework**: .NET 10  
**Approach**: Consolidate LSLBridge into BlueMuse.App, remove LSL clock utility, upgrade both main projects atomically to .NET 10

## Project Overview

| Project | Current | Target | Type | Status |
|---------|---------|--------|------|--------|
| BlueMuse.App | .NET 5.0 | .NET 10 | WPF + UWP | Primary app — consolidate LSL interop here |
| LSLBridge | .NET 4.8 | DELETE | WPF Utility | LSL stream monitor — merge into BlueMuse.App |
| BlueMuse | .NET Core | .NET 10 | WAPPROJ | UWP wrapper — upgrade to match BlueMuse.App |
| NativeHelpers | — | — | C++ DLL | No changes needed |

## Upgrade Execution

### 01-prerequisites: Verify environment and prepare

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

**Done when**: 
- Environment verified (.NET 10 SDK installed, Visual Studio ready)
- Consolidation scope documented (which files/classes move from LSLBridge → BlueMuse.App)
- NativeHelpers builds without errors
- All binding redirect files (.config) identified and reviewed

---

### 02-consolidate-lsl-bridge: Merge LSLBridge functionality into BlueMuse.App

Move LSL stream management logic from LSLBridge into BlueMuse.App. Copy relevant source files (LSL folder, Helpers) into BlueMuse.App. Integrate the stream monitoring UI (MainWindow.xaml) as a new view or merge its functionality into MainPage.

**Consolidation Scope**:
- **Move**: LSLBridge/LSL/* (LSL.cs, LSLStream.cs, LSLStreamInfo.cs, LSLStreamManager.cs) → BlueMuse.App/LSL/
- **Move**: LSLBridge/Helpers/* (ArrayConversion.cs, CommandHandler.cs, ObservableCollection.cs, ObservableObject.cs, StreamHelper.cs) → BlueMuse.App/Helpers/ (check for duplicates with existing helpers)
- **Move**: LSLBridge/Binaries/* (liblsl32.dll, liblsl64.dll, etc.) → BlueMuse.App/Binaries/
- **Integrate**: LSLBridge stream monitoring UI into BlueMuse.App (merge with existing MainPage.xaml or create new LSLMonitorPage)
- **Update namespaces** from LSLBridge.* to BlueMuse.App.LSL and BlueMuse.App.Helpers
- **Update project references**: BlueMuse.wapproj currently references LSLBridge → remove, add direct reference to BlueMuse.App

**Assessment Context**:
- LSLBridge contains ~400-500 LOC of LSL-specific logic (LSLStreamManager, stream monitoring)
- No circular dependencies with BlueMuse.App
- Binding redirects in LSLBridge/App.config → will be removed during SDK-style conversion
- Both projects already use XAML/WPF patterns (no architectural conflicts)

**Known Risks**:
- Namespace conflicts if Helpers classes are duplicated (CommandHandler, ObservableCollection already exist in BlueMuse.App) → merge or rename carefully
- LSLBridge UI assumptions (standalone window) vs BlueMuse.App model (page-based) — may need refactoring

**Research Starting Points**:
- Compare BlueMuse.App/Helpers with LSLBridge/Helpers — identify which files are unique vs duplicates
- Review LSLBridge/ViewModels/MainWindowVM.cs — understand binding logic for stream display grid
- Check if BlueMuse.App already handles LSL integration or if this is entirely new

**Done when**:
- All LSLBridge source files (LSL, Helpers, Binaries) copied to BlueMuse.App
- Namespaces updated throughout
- LSL stream monitoring UI integrated and compiling
- BlueMuse.wapproj reference updated (LSLBridge removed, BlueMuse.App reference confirmed)
- No build errors in BlueMuse.App (warnings acceptable for now, will fix in later tasks)

---

### 03-convert-bluemuse-app-to-sdk-style: Convert BlueMuse.App to SDK-style project

Convert BlueMuse.App.csproj from old-style to SDK-style format. This is required for .NET 10 compatibility and enables modern package management.

**Assessment Context**:
- BlueMuse.App currently targets .NET 5.0 with old-style project format (ToolsVersion="15.0")
- Uses packages.config (old NuGet format) — needs migration to PackageReference
- Project type GUIDs indicate WPF app with UWP support
- Binding redirects in App.config must be documented before removal

**Known Risks**:
- packages.config contains ~20+ packages, many now included in .NET 10 (NETStandard.Library, System.* packages)
- App.config binding redirects may hide real version conflicts — document before removal
- Custom build properties or targets in old project file — ensure they're preserved or modernized
- UWP-specific project configuration must be preserved in new format

**Research Starting Points**:
- Read all of BlueMuse.App.csproj (entire file, including Import statements)
- Document all custom PropertyGroups and ItemGroups in the project file
- Inventory all binding redirects in App.config and their purposes
- Check for post-build events or custom targets that must be preserved

**Done when**:
- BlueMuse.App.csproj converted to SDK-style format (Microsoft.NET.Sdk.WindowsDesktop or Microsoft.NET.Sdk)
- packages.config migrated to PackageReference entries in the project file
- Old binding redirects documented and removed
- Project still builds (warnings acceptable)

---

### 04-delete-lslbridge-project: Remove LSLBridge from solution

Delete LSLBridge project folder and remove all references from the solution. Since functionality is now in BlueMuse.App, this project is no longer needed.

**Assessment Context**:
- LSLBridge was a workaround for UWP limitations (native DLL calls) — no longer needed with modern .NET
- Only consumer was BlueMuse.wapproj (which will reference BlueMuse.App instead)
- Standalone utility app (not a library) — no shared API surface

**Known Risks**:
- Breaking change: external users of LSLBridge.exe (if any) — document removal
- Settings or configuration specific to LSLBridge.exe — may need to be migrated to BlueMuse.App

**Research Starting Points**:
- Check LSLBridge/App.config for any application settings or features
- Verify no external tools or scripts depend on LSLBridge.exe
- Look for any deployment or installation scripts that reference LSLBridge

**Done when**:
- LSLBridge project removed from solution file
- LSLBridge folder deleted from disk
- Solution opens without errors or unresolved references
- Git history preserved (git rm, not manual delete)

---

### 05-upgrade-bluemuse-app-to-net10: Update BlueMuse.App target framework to .NET 10

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

---

### 06-upgrade-bluemuse-wapproj-to-net10: Update BlueMuse WAPPROJ target framework to .NET 10

Update the BlueMuse WAPPROJ (UWP/Windows package) to target .NET 10. This wraps BlueMuse.App for deployment.

**Assessment Context**:
- BlueMuse WAPPROJ had 1 issue: TFM update required
- Currently depends on LSLBridge (via BlueMuse.App) — after consolidation, only depends on BlueMuse.App
- WAPPROJ is primarily a packaging/deployment artifact

**Known Risks**:
- WAPPROJ may have hardcoded references to LSLBridge binaries — update or remove
- Manifest settings may need adjustment for modern Windows 10/11 capabilities
- UAC, app identity, or signing configuration might need review

**Research Starting Points**:
- Review BlueMuse.wapproj for any LSLBridge references
- Check Package.appxmanifest for Windows 10 capability declarations
- Verify signing certificate is still valid

**Done when**:
- BlueMuse WAPPROJ TargetFramework updated to .NET 10
- All LSLBridge references removed
- Solution builds without errors
- WAPPROJ can generate app package (.msix or .appx)

---

### 07-remove-deprecated-packages: Clean up and document binding redirects

Remove all binding redirects from App.config files. Document any version conflicts discovered. Clean up deprecated or redundant NuGet packages (e.g., NETStandard.Library, legacy System.* packages).

**Assessment Context**:
- 30 binding redirect issues detected (22 missing, 4 conflicts, 4 downgrades)
- 48 NuGet packages have functionality included in .NET 10 framework
- 2 deprecated packages: Serilog.Sinks.RollingFile, possibly others
- User option selected: "Document and Review Before Removing" — so generate a report

**Known Risks**:
- Removing binding redirects may expose underlying version conflicts at runtime
- Aggressive package cleanup might remove dependencies of indirect references
- Some legacy packages may be needed for backward compatibility

**Research Starting Points**:
- Parse all App.config files for <assemblyBinding> sections
- Generate a matrix: {Assembly} → {Current binding} → {New version in .NET 10} → {Conflict notes}
- Test application thoroughly after binding redirect removal to catch runtime load failures

**Done when**:
- Binding redirect report generated and reviewed
- All binding redirects removed from App.config
- Deprecated NuGet packages removed or migrated
- Solution builds and runs without FileLoadException or MissingMethodException
- Test suite passes

---

### 08-validation: Verify full upgrade and integration

Build the entire solution for .NET 10. Run any tests. Verify BlueMuse.App with integrated LSL monitoring works correctly.

**Assessment Context**:
- All previous tasks must complete before validation
- Solution should now have 0 mandatory issues (all APIs, packages, and frameworks updated)
- Architecture is simplified: 1 main app (BlueMuse.App) + 1 package (WAPPROJ) instead of 3

**Known Risks**:
- Runtime errors in LSL stream monitoring after consolidation (different threading or binding contexts)
- UWP/Windows App SDK compatibility issues
- Performance regression from consolidation or package changes

**Research Starting Points**:
- Create test scenario: launch BlueMuse.App, verify LSL monitoring panel displays stream data
- Stress-test with multiple LSL streams simultaneously
- Test protocol activation (App.xaml.cs Uri handling)
- Verify Bluetooth device enumeration still works

**Done when**:
- Solution builds with 0 errors, 0 warnings
- BlueMuse.App launches successfully
- LSL stream monitoring displays correctly
- All protocol commands work (URI schemes in App.xaml.cs)
- Bluetooth Muse enumeration works
- WAPPROJ builds app package without errors
- Test suite passes (if any)
- Git commit: "Upgrade to .NET 10 and consolidate LSLBridge"

---

## Strategy Declaration

**All-at-Once Atomic Upgrade**

All projects upgraded together in a single atomic pass. LSLBridge is consolidated into BlueMuse.App to eliminate the .NET Framework → Core tier boundary complexity. The architecture is simplified from 4 projects to 3, improving maintainability and reducing the risk of version misalignment between layers.

**Rationale**: 
- Clear dependency hierarchy after consolidation (no circular deps)
- Small codebase (no 15+ project complexity)
- Consolidation opportunity reduces future maintenance burden
- Modern .NET 10 enables direct native DLL calls (removes original UWP workaround)

**Execution Constraints**:
- All projects must be updated together — no incremental buildability between tasks
- Consolidation must be completed before TFM updates to avoid reference conflicts
- Binding redirects must be reviewed before removal (selected user option)
- Full solution validation at the end before accepting the upgrade

