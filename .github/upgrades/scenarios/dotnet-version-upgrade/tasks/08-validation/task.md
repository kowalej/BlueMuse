# 08-validation: Verify full upgrade and integration

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
