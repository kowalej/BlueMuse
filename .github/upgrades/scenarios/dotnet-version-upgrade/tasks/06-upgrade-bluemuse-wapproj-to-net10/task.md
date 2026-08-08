# 06-upgrade-bluemuse-wapproj-to-net10: Update BlueMuse WAPPROJ target framework to .NET 10

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
