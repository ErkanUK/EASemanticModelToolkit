# Version metadata and YAML escaping fixes

Source patch adapted to the supplied current repository checkout (reported commit 869de20). Newer domain/package placement code is preserved. No rebuilt DLLs are included.

- Recognize class-level `version` as metadata, retain it on ImportClass, and write it to EA Element.Version. Explicit attributes named `version` remain normal data attributes.
- Decode JSON-compatible YAML double-quoted scalar escapes exactly once. Preserve single-quoted literal backslashes and decode doubled apostrophes.
- Serialize exported quoted scalars with System.Text.Json, including tabs and control characters.
- Parse JSON flow collections before attempting the legacy single-quote fallback, preserving apostrophes in valid JSON annotations.
- Recognize closing quotes after an even number of backslashes when scanning comments and mapping separators.

## Validation

Added tests/roundtrip/RoundTrip.Tests.csproj linking the actual importer converter/parser and exporter writer sources. It checks five successive export/import cycles, package paths, version metadata, explicit version attributes, multiline descriptions, tabs, paths, literal backslashes, quotes, Unicode and inline annotations. It simulates EA's default element version; it does not invoke EA Automation.

The patch environment has no .NET SDK or Windows/EA runtime. C# tests and compilation have NOT been run. Run on a machine with .NET 9 SDK:

```powershell
dotnet run --project .\tests\roundtrip\RoundTrip.Tests.csproj -c Release
dotnet run --project .\components\importer\tests\EAJsonModelImporter.Tests.csproj -c Release
dotnet run --project .\components\exporter\tests\EA17LinkMLExporter.Tests.csproj -c Release
dotnet build .\EASemanticModelToolkit.csproj -c Release
```

Close EA and build/install the corrected source:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -BuildFromSource
```

Then repeat the three-class import/export/import in a fresh EA test package. Attribute counts should remain 1, 1 and 2, respectively.

This prevents new corruption. It deliberately does not delete existing duplicate attributes or repeatedly unescape previously corrupted descriptions: those require a separate, reviewed cleanup.
