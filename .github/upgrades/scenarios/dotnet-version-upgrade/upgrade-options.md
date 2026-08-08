# Upgrade Options — BlueMuse

Assessment: 4 projects (mixed .NET Framework 4.8, .NET 5.0, WAPPROJ), 218 issues (156 mandatory), ClassLibrary + WPF + native interop, .NET Framework→Core boundary requires tier-by-tier validation

## Strategy

### Upgrade Strategy
LSLBridge consolidation into BlueMuse.App eliminates the .NET Framework→Core tier dependency. Remaining projects (BlueMuse.App + WAPPROJ) are both modern .NET, enabling atomic all-at-once upgrade.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade all projects simultaneously to .NET 10. Consolidate LSLBridge functionality into BlueMuse.App. Remove LSLBridge and clock utility from solution. Single atomic pass modernizes and simplifies architecture. |

## Modernization

### Assembly Binding Redirects
30 binding redirect issues detected across projects (22 missing, 4 conflicts, 4 downgrades). These require review to understand underlying version conflicts before removal, as they may indicate real package incompatibilities.

| Value | Description |
|-------|-------------|
| **Document and Review Before Removing** (selected) | Generate a report of all binding redirects and their purposes during the upgrade. Review before removal to understand if conflicts need special handling. |
| Remove Binding Redirects | Remove all binding redirects immediately (fast, but may hide underlying conflicts). |
