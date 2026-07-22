# EA JSON/YAML Model Importer

Sparx Enterprise Architect JSON/YAML Model Importer is a 64-bit add-in for Sparx Enterprise Architect 17. It converts JSON data, JSON Schema, YAML data, and YAML-based JSON Schema into an editable UML class model, with optional OWL ontology exports for Protégé and other semantic-web tools.

The importer creates native EA packages, classes, attributes, enumerations, associations, generalizations, multiplicities, notes, and a class diagram. The result can be edited using normal Enterprise Architect modelling tools.

## Requirements

- Sparx Enterprise Architect 17, 64-bit
- Windows x64
- .NET 9 Desktop Runtime, x64

The included `prebuilt` directory means the .NET SDK is not required for normal installation.

## Installation

1. Close Enterprise Architect.
2. Extract the release ZIP to a local directory.
3. Open PowerShell in the extracted directory.
4. Run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

5. Restart Enterprise Architect.
6. Confirm that **EA JSON/YAML Model Importer** appears under **Specialize > Manage Add-Ins**.

The installer registers the add-in for the current Windows user under EA's 64-bit `EAAddins64` registry location. Administrator rights are not normally required.

## Importing a model

1. Open an EA repository.
2. Select the package that should contain the imported model.
3. Open **Specialize > Add-Ins > JSON/YAML Model Importer**.
4. Choose **Import into selected package…**.
5. Select a `.json`, `.schema.json`, `.yaml`, or `.yml` file.
6. Review the preview showing the number of classes and enumerations.
7. Optionally select **Export OWL (.owl, RDF/XML)** and/or **Export OWL Turtle (.ttl)**.
8. Select **Import** to create the model.

The importer creates a new child package and a class diagram. It never overwrites an existing package. If the generated package name already exists, a numeric suffix is added.

Generated diagrams use a deterministic, relationship-aware layout. Connected classes are arranged in graph layers, disconnected components are grouped, box sizes reflect their contents, and enumerations are placed separately. The algorithm uses model topology only and has no domain-specific class-name rules.

## Optional OWL ontology exports

The import confirmation contains two unchecked export options. Selected ontology files are written beside the source JSON or YAML file, using the same base filename. Existing files with those names are replaced.

- `.owl` uses RDF/XML.
- `.ttl` uses Turtle.

Both serializations describe the same OWL ontology. UML classes become `owl:Class` resources, primitive attributes become datatype properties, associations become object properties, generalizations become `rdfs:subClassOf` axioms, multiplicities become cardinality restrictions, and enumerations become OWL classes containing named individuals. Descriptions, model versions, and EA identifier attributes are preserved as ontology annotations.

## Mappings

| JSON, JSON Schema or YAML | Enterprise Architect UML |
|---|---|
| Root object | Root class and package |
| Object definition | Class |
| Primitive property | Attribute |
| Nested object | Class and association |
| `$ref` | Association to the referenced class |
| Array of primitives | Multivalued attribute |
| Array of objects or references | Association with upper multiplicity `*` |
| Required property | Lower multiplicity `1` |
| Optional property | Lower multiplicity `0` |
| `enum` | UML Enumeration and literals |
| LinkML `enums` / `permissible_values` | UML Enumeration and literals |
| LinkML `identifier` / `unique_keys` | EA identifier attributes, including composite keys |
| LinkML `annotations.ea_domains` | Domain overview and focused EA diagrams |
| LinkML `annotations.ea_order` | Stable class order within generated domain diagrams |
| `allOf` reference | Generalization |
| `oneOf` or `anyOf` references | Choice class |
| `title` | Model or class name |
| `description` | EA Notes |
| `version` | EA package version |
| JSON number | `Real` attribute |
| JSON integer | `Integer` attribute |
| JSON boolean | `Boolean` attribute |
| JSON string | `String` attribute |
| `date` / `date-time` format | `Date` / `DateTime` attribute |

For ordinary JSON or YAML data without a schema, the importer infers classes and types from the available values. For arrays of objects, the first non-null item is used as the structural sample.

## Example

Input:

```yaml
title: Product Catalogue
type: object
required:
  - products
properties:
  products:
    type: array
    items:
      type: object
      title: Product
      properties:
        code:
          type: string
        price:
          type: number
```

This produces a `ProductCatalogue` class associated with a `Product` class. The association has multiplicity `1..*`, while `code` and `price` become attributes of `Product`.

## Structured EA diagram layouts

LinkML classes can opt into one or more generated domain diagrams with EA-specific annotations:

```yaml
annotations:
  ea_domain_colors:
    network_spine: "#DDEBF7"
    load_planning: "#E2EFDA"
    asset_health: "#FFF2CC"
    source_lineage: "#EAD1DC"

classes:
  Transformer:
    annotations:
      ea_domains: "network_spine,load_planning,asset_health"
      ea_order: 10
  UsagePoint:
    annotations:
      ea_domains: "network_spine,load_planning"
      ea_order: 20
```

`ea_domains` accepts either a comma-separated string or an inline YAML array. The first domain is the class's primary domain in the overview. `ea_order` is an integer used to keep placement stable inside each domain.

`ea_domain_colors` is an optional model-level mapping from domain name to a CSS-style hexadecimal colour. Both `#RRGGBB` and shorthand `#RGB` values are accepted. Invalid values fall back to the built-in pastel palette. The importer converts the web colour to EA's decimal BGR representation when creating each diagram object.

When at least one class contains `ea_domains`, the importer creates:

- a relationship-aware overview of all classes, coloured by primary domain;
- one focused diagram per domain, with shared anchor classes included in every applicable view; and
- a separate enumeration diagram.

Domain names are unrestricted. Domains are ordered deterministically using their classes' lowest `ea_order` value and then the domain name; there are no built-in industry or class-name rules. Classes without `ea_domains` are placed in an `Other` domain. Schemas without layout annotations receive one relationship-aware smart diagram.

Additional examples are available in the `samples` directory:

- `library.schema.json`
- `catalogue.yaml`
- `domain-layout.yaml`

## YAML support

The MVP includes a dependency-free reader for the JSON-compatible subset of YAML commonly used by schemas and data files. It supports:

- Indented mappings and sequences
- String, number, boolean and null scalars
- Quoted values
- Comments
- Inline JSON-style arrays and objects
- Literal and folded block text

The following advanced YAML features are not yet supported:

- Anchors and aliases
- Custom tags
- Merge keys
- Multiple documents in one file
- Complex mapping keys

Convert documents using these features to JSON before importing them.

## Repeated imports and model updates

This MVP treats every import as a new model. It does not merge changes into a previously imported package. This protects existing EA content and makes testing reversible: delete the generated child package if the import is not required.

Future versions can add stable source identifiers, change comparison, and controlled model synchronization.

## Troubleshooting

### The add-in is not listed in EA

- Confirm that EA is 64-bit.
- Close EA and run `install.ps1` again.
- Check **Specialize > Manage Add-Ins** after restarting EA.
- Confirm that the .NET 9 Desktop Runtime x64 is installed.

### The import command is disabled

Select a package in EA's Browser before opening the add-in menu.

### The input fails to parse

- Validate JSON documents with a JSON parser.
- Check YAML indentation and ensure spaces are used consistently.
- Convert YAML that uses anchors, aliases, tags, or multiple documents to JSON.

### A generated type name looks different

Names are converted to UML-friendly PascalCase and punctuation or whitespace is removed. Nested types use their property or schema title directly. The owning class name is added only when two generated definitions would otherwise have the same name.

Structural definition containers named `classes`, `definitions`, `$defs`, or `schemas` are flattened. Their child keys become UML class names directly, so a `classes.TransformerLoadForecast` entry is imported as `TransformerLoadForecast`, not `ClassesTransformerLoadForecast`.

Within a class definition, an `attributes` section is also flattened. Primitive ranges become UML attributes on the owning class, while ranges that name another class become associations. The importer therefore creates `Terminal` with its properties and associations rather than a separate `TerminalAttributes` class.

LinkML `relationships` entries are imported as associations on their owning class. Schema metadata such as `prefixes`, `imports`, and `annotations` does not create UML classes. The `ea_domains` and `ea_order` annotation values are additionally used for diagram layout.

## Building from source

The project targets `net9.0-windows` and references `lib/Interop.EA.dll`.

```powershell
dotnet restore .\EAJsonModelImporter.csproj
dotnet build .\EAJsonModelImporter.csproj -c Release
dotnet publish .\EAJsonModelImporter.csproj -c Release -o .\prebuilt
```

Automated converter tests are maintained separately from the installable package and cover JSON Schema, JSON inference, and YAML conversion.

## MVP status

This is a test build. Use a disposable EA package or repository until the generated model has been reviewed and accepted.
