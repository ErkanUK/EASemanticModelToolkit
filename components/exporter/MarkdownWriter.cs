using System.Text;

namespace EA17LinkMLExporter;

internal static class MarkdownWriter
{
    public static string Write(ModelSnapshot model, string? drawIoName, string? fallbackSvgName, string? linkMlName,
        string? jsonSchemaName, string? owlName, string? turtleName, IReadOnlyList<ExportedDiagram> eaDiagrams)
    {
        var b = new StringBuilder();
        b.AppendLine("# " + model.Name).AppendLine();
        b.AppendLine("EA package version: `" + (model.Version.Length > 0 ? Cell(model.Version) : "not set") + "`").AppendLine();
        if (model.Notes.Length > 0) b.AppendLine(model.Notes).AppendLine();
        var links = new List<string>();
        if (linkMlName is not null) links.Add("[LinkML schema](" + linkMlName + ")");
        if (jsonSchemaName is not null) links.Add("[JSON Schema](" + jsonSchemaName + ")");
        if (drawIoName is not null) links.Add("[Editable draw.io diagram](" + drawIoName + ")");
        if (owlName is not null) links.Add("[OWL/RDF-XML ontology](" + owlName + ")");
        if (turtleName is not null) links.Add("[OWL Turtle ontology](" + turtleName + ")");
        if (links.Count > 0) b.AppendLine(string.Join(" · ", links)).AppendLine();
        if (eaDiagrams.Count > 0)
        {
            b.AppendLine("## EA diagrams").AppendLine();
            foreach (var diagram in eaDiagrams)
            {
                b.AppendLine("### " + diagram.Name).AppendLine();
                b.AppendLine("![" + Alt(diagram.Name) + "](" + diagram.RelativePath.Replace(" ", "%20") + ")").AppendLine();
            }
        }
        else if (fallbackSvgName is not null)
        {
            b.AppendLine("![Generated UML class diagram](" + fallbackSvgName.Replace(" ", "%20") + ")").AppendLine();
        }
        b.AppendLine("## Classes").AppendLine();
        foreach (var cls in model.Classes.OrderBy(x => x.Name))
        {
            b.AppendLine("### " + cls.Name).AppendLine();
            if (cls.Notes.Length > 0) b.AppendLine(cls.Notes).AppendLine();
            b.AppendLine("Qualified name: `" + cls.QualifiedName + "`  ");
            if (!string.IsNullOrWhiteSpace(cls.Version))
            b.AppendLine("Version: `" + cls.Version + "`  ");
            if (cls.Parents.Count > 0) b.AppendLine("Extends: " + string.Join(", ", cls.Parents.Select(x => "`" + x + "`")) + "  ");
            if (cls.Abstract) b.AppendLine("Abstract: yes  ");
            b.AppendLine();
            if (cls.Properties.Count > 0)
            {
                b.AppendLine("| Attribute | Type | Multiplicity | Description |");
                b.AppendLine("|---|---|---:|---|");
                foreach (var p in cls.Properties)
                    b.AppendLine("| `" + Cell(p.Name) + "` | `" + Cell(p.Type) + "` | " + Cell(Multiplicity(p.Lower, p.Upper)) + " | " + Cell(p.Notes) + " |");
                b.AppendLine();
            }
        }
        if (model.Enums.Count > 0)
        {
            b.AppendLine("## Enumerations").AppendLine();
            foreach (var item in model.Enums.OrderBy(x => x.Name))
            {
                b.AppendLine("### " + item.Name).AppendLine();
                if (item.Notes.Length > 0) b.AppendLine(item.Notes).AppendLine();
                foreach (var value in item.Values) b.AppendLine("- `" + value + "`");
                b.AppendLine();
            }
        }
        if (model.Relations.Count > 0)
        {
            b.AppendLine("## Relationships").AppendLine();
            b.AppendLine("| Source | Relationship | Target | Multiplicity |");
            b.AppendLine("|---|---|---|---|");
            foreach (var r in model.Relations)
                b.AppendLine("| " + Cell(r.SourceName) + " | " + (r.Composition ? "Composition" : r.Kind) + " | " + Cell(r.TargetName) + " | " + Cell(r.SourceMultiplicity + " ↔ " + r.TargetMultiplicity) + " |");
        }
        b.AppendLine().AppendLine("---").AppendLine("Generated from Sparx Enterprise Architect by EA17 LinkML Exporter.");
        return b.ToString();
    }

    private static string Multiplicity(string lower, string upper) => lower == upper ? lower : lower + ".." + upper;
    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", "").Replace("\n", "<br>");
    private static string Alt(string value) => value.Replace("[", "(").Replace("]", ")").Replace("\r", " ").Replace("\n", " ");
}
