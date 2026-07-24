# EA Semantic Model Toolkit

A unified 64-bit add-in for Sparx Enterprise Architect 17. It provides one **Semantic Model Toolkit** menu for:

- importing JSON, JSON Schema, and YAML into editable EA packages; and
- exporting selected EA packages to LinkML YAML, JSON Schema, Markdown, draw.io, SVG, OWL/RDF-XML, and OWL/Turtle.

## Requirements

- Enterprise Architect 17, 64-bit
- .NET 9 Desktop Runtime, x64
- .NET 9 SDK when building from source

## Install

Close Enterprise Architect, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -BuildFromSource
```

Restart EA and open **Specialize > Add-Ins > Semantic Model Toolkit**.

The toolkit intentionally uses a new COM and installer identity, so it can be tested alongside the legacy importer and exporter. Uninstall or disable the legacy add-ins once the combined toolkit is verified.

## Updating an imported model

Import the revised JSON or YAML while either its parent package or the previously imported model package is selected. When a package with the same model name exists, the confirmation dialog offers:

- **Update** — updates matching classes and enumerations by name, adds new content, and preserves the coordinates and sizes of existing diagram objects.
- **Create a new package** — performs an independent import with a numeric suffix.

Updates are deliberately non-destructive: EA elements, attributes, connectors, and literals that are absent from the revised source are retained so manual EA work is not silently deleted. Renaming a source class is therefore treated as adding a new class. Keep generated diagram names unchanged if their layouts should be reused.

## Development

```powershell
dotnet build .\EASemanticModelToolkit.csproj -c Release
dotnet run --project .\components\importer\tests\EAJsonModelImporter.Tests.csproj -c Release
dotnet run --project .\components\exporter\tests\EA17LinkMLExporter.Tests.csproj -c Release
```

The complete histories of both original projects are retained under `components/importer` and `components/exporter`.
