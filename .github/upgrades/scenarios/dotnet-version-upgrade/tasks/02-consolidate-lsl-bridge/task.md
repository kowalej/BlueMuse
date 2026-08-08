# 02-consolidate-lsl-bridge: Merge LSLBridge functionality into BlueMuse.App

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
