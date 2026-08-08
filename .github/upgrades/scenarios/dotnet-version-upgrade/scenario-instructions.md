# .NET Version Upgrade — BlueMuse

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: master
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Key Decisions Log

### LSLBridge Consolidation & App Simplification
**Date**: 2024-12-19  
**Decision**: APPROVED — Consolidate LSLBridge into BlueMuse.App; eliminate LSL clock utility app  
**Context**: 
- LSLBridge originally existed as a workaround — UWP apps in .NET Framework couldn't call native DLLs directly. Modern .NET allows direct LSL library calls.
- Additional utility app (LSL clock) is redundant and can be removed.
- This simplifies architecture from 3 projects to 1 main app + 1 package.

**Strategy**: Change from Bottom-Up to **All-at-Once** (no tier dependency complexity after consolidation)

**Scope Changes**:
- Merge LSLBridge code into BlueMuse.App (direct native interop)
- Remove LSL clock app from solution
- Delete LSLBridge and clock app projects
- Upgrade BlueMuse.App + WAPPROJ to .NET 10 as single atomic pass

**Risk**: Medium (consolidation + TFM migration in one pass) — mitigated by clear dependency structure and small codebase size

## Strategy

**Selected**: All-at-Once  
**Rationale**: LSLBridge consolidation eliminates the .NET Framework→Core tier boundary. Remaining projects (BlueMuse.App + WAPPROJ) are both modern .NET, enabling safe atomic upgrade.

### Execution Constraints
- All projects upgraded together in single atomic pass (no incremental buildability between tasks)
- Consolidation of LSLBridge must complete before TFM updates to avoid reference conflicts
- Binding redirects reviewed and documented before removal (per user option: "Document and Review Before Removing")
- Full solution validation required before acceptance — no partial success
- Simplified architecture (1 main app + 1 package) reduces complexity vs original 3-project structure
