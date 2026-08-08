# 04-delete-lslbridge-project: Remove LSLBridge from solution

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
