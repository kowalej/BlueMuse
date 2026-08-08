# 03-convert-bluemuse-app-to-sdk-style: Convert BlueMuse.App to SDK-style project

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
