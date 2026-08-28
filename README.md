# EA Semantic Model Toolkit

A unified 64-bit add-in for Sparx Enterprise Architect 17. It provides one **Semantic Model Toolkit** menu for:

- importing JSON, JSON Schema, and YAML into editable EA packages; and
- exporting selected EA packages to LinkML YAML, JSON Schema, Markdown, draw.io, PlantUML, SVG, OWL/RDF-XML, and OWL/Turtle.

- What is different compared to the Sparx ODM Export ?

Use Sparx ODM to author a formal ODM ontology. 
Use Semantic Model Toolkit when one business-friendly EA model must serve many audiences and technical ecosystems—including LinkML, JSON Schema, documentation, diagrams and OWL.

## Requirements

- Enterprise Architect 17, 64-bit
- .NET 9 Desktop Runtime, x64
- .NET 9 SDK when building from source

## Capabilities
| Capability | Sparx ODM | Semantic Model Toolkit |
|---|---|---|
| Source model | Requires an ODM `owlOntology` or `rdfDocument` package | Works from ordinary EA/UML-style semantic models |
| Import formats | OWL/RDF XML | LinkML YAML, YAML, JSON and JSON Schema |
| Export formats | OWL/RDF XML | LinkML YAML, JSON Schema, Markdown, draw.io, PlantUML, SVG, OWL/RDF-XML and OWL/Turtle |
| Round-trip updates | Imports into an ODM package | Updates an existing model while preserving manually improved diagram positions and sizes |
| Documentation | Ontology file is the main output | Produces human-readable Markdown and SVG documentation |
| Visual sharing | Primarily inside EA | Exports draw.io and SVG for users without EA |
| LinkML workflow | Not built in | LinkML is a first-class import/export format |
| Customisation | Built-in, proprietary EA feature | Open-source and adaptable to project conventions |

PlantUML export writes a self-contained `.puml` class diagram containing classes, enumerations, attributes,
inheritance, associations, roles and multiplicities. Missing EA attribute datatypes are displayed as `unnamed`
without preventing the PlantUML document from rendering. In LinkML output, a missing datatype falls back to
`string` so the generated schema does not contain an unresolved `range: unnamed` reference.

## LinkML import into EA

| LinkML feature | Status | Current behaviour |
| --- | --- | --- |
| `classes` | Implemented | Creates EA classes |
| Inline `attributes` | Implemented | Creates attributes or associations according to `range` |
| Top-level reusable `slots` | Implemented | Resolved when named in a class `slots` list |
| `slot_usage` | Partial | Supports overrides for range, description, identifier, required and exact cardinalities |
| Class-valued ranges | Implemented | Creates EA associations |
| Enum-valued ranges | Implemented | Remain EA attributes typed by an enumeration |
| `enums` / `permissible_values` | Implemented | Creates EA enumerations and imports value descriptions |
| `is_a` | Implemented | Creates an EA generalization |
| `mixins` | Partial | Creates generalizations, but mixin identity is not preserved separately |
| `abstract` | Not implemented on import | Recognised as metadata but not applied to the EA class |
| `mixin: true` | Not implemented on import | Does not mark or visually distinguish the EA class as a mixin |
| `identifier: true` | Implemented | Sets the EA attribute’s ID property and is preserved during LinkML export |
| `unique_keys` | Partial | Marks every member as an EA ID attribute, but loses the named composite-key structure |
| `required` | Implemented | Converted to the appropriate EA lower bound |
| `multivalued` | Implemented | Converted to upper bound `*`, unless an exact maximum is specified |
| Exact cardinalities | Implemented | Values such as `2..5` are retained as EA lower and upper bounds |
| Description | Implemented | Imported into EA notes |
| Enumeration-value descriptions | Implemented | Imported into enumeration literal notes and preserved during LinkML export |
| Schema `id` and `version` | Implemented | Stored as model metadata and preserved during LinkML export |
| Schema `name` and `title` | Implemented | `title` is used as the EA display/package name while the LinkML `name` is retained for export |
| `prefixes` and `default_prefix` | Implemented | Stored as LinkML metadata on the EA package and restored during export |
| `imports` | Partial | Import declarations are preserved for export, but external schemas are not loaded or resolved |
| Unsupported-feature reporting | Implemented | Displays a warning listing unsupported LinkML constructs before import and allows the user to continue or cancel |
| EA colour/domain annotations | Implemented extension | Supports toolkit-specific diagram domains, colours and ordering |
| Custom `relationships` | Implemented extension | Imported like class-valued attributes; this is a toolkit convention rather than core LinkML |

## Install

Close Enterprise Architect, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Release and test ZIPs install their included prebuilt binaries. When running from a full source checkout,
add `-BuildFromSource` to compile before installation. If that switch is accidentally used with a binary-only
package, the installer reports a warning and safely uses the included prebuilt add-in.

Restart EA and open **Specialize > Add-Ins > Semantic Model Toolkit**.

The toolkit intentionally uses a new COM and installer identity, so it can be tested alongside the legacy importer and exporter. Uninstall or disable the legacy add-ins once the combined toolkit is verified.

## YAML compatibility

The importer supports LinkML YAML descriptions and comments written with folded or literal block scalars, including the standard chomping forms `>-`, `>+`, `|-`, and `|+`. These forms are supported both as mapping values and as list items such as `- >-`, so later classes and enumerations are not omitted during parsing.

Reusable top-level `slots` are resolved when named in a class `slots` list. Per-class `slot_usage` values override range, description, identifier, required and cardinality metadata; a class-valued range becomes an EA association. LinkML `mixins` are represented as generalizations so their shared slots remain inherited rather than being misread as separate attribute classes.

## Updating an imported model

Import the revised JSON or YAML while either its parent package or the previously imported model package is selected. When a package with the same model name exists, the confirmation dialog offers:

- **Update** — updates matching classes and enumerations by name, adds new content, and preserves the coordinates and sizes of existing diagram objects.
- **Create a new package** — performs an independent import with a numeric suffix.

Updates are deliberately non-destructive: EA elements, attributes, connectors, and literals that are absent from the revised source are retained so manual EA work is not silently deleted. Renaming a source class is therefore treated as adding a new class. Keep generated diagram names unchanged if their layouts should be reused.

## Diagram defaults

Newly imported diagrams use landscape orientation and Orthogonal - Square connector lines. These values can be changed in `semantic-model-toolkit.settings.json` beside the installed add-in:

```json
{
  "diagramDefaults": {
    "connectorLineStyle": "OrthogonalSquare",
    "orientation": "Landscape"
  }
}
```

Enterprise Architect does not expose a writable paper-size setting through its Automation API. New diagrams therefore inherit EA's application-wide default paper size. To use A3, set it in **Start > Appearance > Preferences > Diagram > Default paper size** before importing. Existing diagrams are not reformatted during update imports, preserving their manually adjusted page, layout, and connector routing.

## Development

```powershell
dotnet build .\EASemanticModelToolkit.csproj -c Release
dotnet run --project .\components\importer\tests\EAJsonModelImporter.Tests.csproj -c Release
dotnet run --project .\components\exporter\tests\EA17LinkMLExporter.Tests.csproj -c Release
```

The complete histories of both original projects are retained under `components/importer` and `components/exporter`.
