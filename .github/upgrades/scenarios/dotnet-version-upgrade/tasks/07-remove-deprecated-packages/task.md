# 07-remove-deprecated-packages: Clean up and document binding redirects

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
