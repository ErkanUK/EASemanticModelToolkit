# EA17 LinkML Exporter

A 64-bit Sparx Enterprise Architect 17 add-in that exports the package selected in the Browser as:

- a LinkML YAML schema;
- a JSON Schema document;
- Markdown documentation, similar in spirit to EA's F8 document generation;
- an editable draw.io class diagram;
- every existing EA diagram in the selected package tree, exported natively as SVG and embedded in the Markdown;
- an OWL ontology serialized as RDF/XML; and
- an OWL ontology serialized as Turtle.

Classes, attributes, notes, enumerations, generalizations, associations, aggregations/compositions, roles, multiplicities, the selected EA package version, and explicit diagram colours are included. Child packages are traversed recursively. Native diagram export preserves EA's saved element positions, connector routing, visible content, colours, and diagram styling. If the selected package tree has no diagrams, the exporter creates its original generated SVG preview as a fallback.

## Install

Prerequisites: Enterprise Architect 17 64-bit and the .NET 9 Desktop Runtime (x64). The .NET 9 SDK is needed only when rebuilding from source.

1. Close Enterprise Architect.
2. Open PowerShell in this folder.
3. Run `powershell -ExecutionPolicy Bypass -File .\install.ps1`.
4. Restart Enterprise Architect.

If an earlier package was installed but the add-in is missing from EA, close EA and run `powershell -ExecutionPolicy Bypass -File .\repair-ea17-registration.ps1`, then restart EA.

The script installs the included prebuilt add-in for the current Windows user and adds the required 64-bit EA and COM registry entries. Administrator rights are not required. To compile it again, add `-BuildFromSource`; if EA is installed elsewhere, also use `-EAInstallDir 'D:\path\to\EA'`.

## Use

1. Select a package in EA's Browser.
2. Open **Specialize > Add-Ins > LinkML Documentation** (the exact ribbon location can vary with workspace layout).
3. Choose **Export selected package…**.
4. Keep or clear the checkboxes for LinkML, JSON Schema, Markdown, draw.io, SVG, OWL/RDF-XML and OWL/Turtle. All formats are checked by default.
5. Select a destination folder.

The add-in creates a subfolder named after the package and writes only the selected formats. Existing EA diagrams are written to its `diagrams` subfolder as numbered SVG files and embedded in the generated `.md` document. Open the `.drawio` file in draw.io/diagrams.net for an editable generated model overview, or open the `.owl`/`.ttl` files in Protégé.

## Ontology and JSON Schema mapping

The `.owl` and `.ttl` files describe the same OWL ontology using different serializations. UML classes become OWL classes, primitive attributes become datatype properties, associations become object properties, generalizations become subclass axioms, multiplicities become cardinality restrictions, and enumerations become classes containing named individuals.

The `.schema.json` output uses JSON Schema draft 2020-12. Classes and enumerations are written under `$defs`, associations use `$ref`, multivalued properties become arrays, required lower bounds become `required` entries, and EA identifier, derived and version details are retained as `x-ea-*` extensions.

## LinkML mapping

| UML | LinkML |
|---|---|
| Class | `classes` entry |
| Attribute | Inline class `attributes` entry |
| Generalization | `is_a` |
| Enumeration | `enums` / `permissible_values` |
| Association end | Attribute whose range is the related class |
| Lower bound > 0 | `required: true` |
| Upper bound > 1 or `*` | `multivalued: true` |

The generated schema uses `https://example.org/<package>` as a placeholder namespace. Change it to the project's canonical URI after export.

## SVG export behavior

The exporter asks EA to render each saved diagram through the Automation Interface using the `.svg` filename extension. This uses the diagram as stored in EA; it does not auto-layout or reconstruct the diagram. Diagram frames, printable-element settings, themes, and other image preferences therefore follow the current EA configuration.

Diagram filenames are prefixed with a stable three-digit sequence to avoid collisions when different packages contain diagrams with the same name. The Markdown uses each package path as the diagram heading.

## Uninstall

Close EA, then run `powershell -ExecutionPolicy Bypass -File .\uninstall.ps1`.
