using System.Text.Json.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace EA17LinkMLExporter;

internal static class LinkMlWriter
{
    public static string Write(ModelSnapshot model)
    {
        var duplicate = model.Classes.Select(c => c.Name).Concat(model.Enums.Select(e => e.Name))
            .GroupBy(Identifier, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException("Ambiguous LinkML class/enum key '" + duplicate.Key + "'. Names must be unique across the selected package tree.");
        var schemaName = Identifier(model.LinkMlName.Length > 0 ? model.LinkMlName : model.Name);
        var declaredRanges = model.Classes.Select(x => Identifier(x.Name))
            .Concat(model.Enums.Select(x => Identifier(x.Name)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mixinNames = model.Classes.SelectMany(x => x.Parents.Skip(1))
            .Select(Identifier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = new StringBuilder();
        if (model.SourceComments.Length > 0)
        {
            b.AppendLine(model.SourceComments.TrimEnd());
            b.AppendLine();
        }
        b.AppendLine("id: " + Scalar(model.OntologyIri.Length > 0 ? model.OntologyIri : "https://example.org/" + schemaName));
        b.AppendLine("name: " + Scalar(schemaName));
        if (!model.Name.Equals(schemaName, StringComparison.Ordinal)) b.AppendLine("title: " + Scalar(model.Name));
        if (model.Version.Length > 0) b.AppendLine("version: " + Scalar(model.Version));
        if (model.Notes.Length > 0) b.AppendLine("description: " + Scalar(model.Notes));
        WriteAnnotations(b, 0, Annotation(model, "schema"));
        b.AppendLine("prefixes:");
        var prefixes = model.LinkMlPrefixes.Count > 0
            ? model.LinkMlPrefixes
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["linkml"] = "https://w3id.org/linkml/", [schemaName] = "https://example.org/" + schemaName + "/" };
        foreach (var prefix in prefixes) b.AppendLine("  " + Identifier(prefix.Key) + ": " + Scalar(prefix.Value));
        string defaultPrefix = model.LinkMlDefaultPrefix.Length > 0 ? model.LinkMlDefaultPrefix : schemaName;
        b.AppendLine("default_prefix: " + Identifier(defaultPrefix));
        b.AppendLine("imports:");
        foreach (string import in model.LinkMlImports.Count > 0 ? model.LinkMlImports : ["linkml:types"])
            b.AppendLine("  - " + Scalar(import));

        if (model.Enums.Count > 0)
        {
            b.AppendLine("enums:");
            foreach (var item in model.Enums.OrderBy(x => x.Name))
            {
                b.AppendLine("  " + Identifier(item.Name) + ":");
                if (item.Notes.Length > 0) b.AppendLine("    description: " + Scalar(item.Notes));
                WriteAnnotations(b, 4, PackageAnnotation(model, "enum:" + item.Name, item.PackagePath));
                b.AppendLine("    permissible_values:");
                foreach (var value in item.Values)
                {
                    b.AppendLine("      " + Scalar(value) + ":");
                    if (item.ValueDescriptions.TryGetValue(value, out var description) && description.Length > 0)
                        b.AppendLine("        description: " + Scalar(description));
                    WriteAnnotations(b, 8, Annotation(model, "enumvalue:" + item.Name + "." + value));
                }
            }
        }

        b.AppendLine("classes:");
        foreach (var cls in model.Classes.OrderBy(x => x.Name))
        {
            b.AppendLine("  " + Identifier(cls.Name) + ":");
            if (cls.Notes.Length > 0) b.AppendLine("    description: " + Scalar(cls.Notes));
            WriteAnnotations(b, 4, PackageAnnotation(model, "class:" + cls.Name, cls.PackagePath));
            if (!string.IsNullOrWhiteSpace(cls.Version)) b.AppendLine("    version: " + Scalar(cls.Version));
            if (cls.Abstract) b.AppendLine("    abstract: true");
            if (mixinNames.Contains(Identifier(cls.Name))) b.AppendLine("    mixin: true");
            if (cls.Parents.Count > 0) b.AppendLine("    is_a: " + Identifier(cls.Parents[0]));
            if (cls.Parents.Count > 1)
            {
                b.AppendLine("    mixins:");
                foreach (var parent in cls.Parents.Skip(1)) b.AppendLine("      - " + Identifier(parent));
            }

            var associations = model.Relations.Where(x => IsExportedDirection(x, cls.Id)).ToList();
            if (cls.Properties.Count == 0 && associations.Count == 0) continue;
            b.AppendLine("    attributes:");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in cls.Properties)
                WriteAttribute(b, Unique(property.Name, names), property.Type, property.Notes, property.Lower,
                    property.Upper, property.ReadOnly, property.Identifier, declaredRanges,
                    Annotation(model, "attribute:" + cls.Name + "." + property.Name));
            foreach (var relation in associations)
            {
                bool forward = relation.SourceId == cls.Id;
                string role = forward ? relation.TargetRole : relation.SourceRole;
                string other = forward ? relation.TargetName : relation.SourceName;
                string multiplicity = forward ? relation.TargetMultiplicity : relation.SourceMultiplicity;
                WriteAttribute(b, Unique(string.IsNullOrWhiteSpace(role) ? LowerCamel(other) : role, names), other, relation.Notes,
                    Lower(multiplicity), Upper(multiplicity), false, false, declaredRanges,
                    Annotation(model, "attribute:" + cls.Name + "." + role));
            }
        }
        return b.ToString();
    }

    private static void WriteAttribute(StringBuilder b, string name, string type, string notes, string lower,
        string upper, bool readOnly, bool identifier, IReadOnlySet<string> declaredRanges, string annotations = "")
    {
        b.AppendLine("      " + Identifier(name) + ":");
        b.AppendLine("        range: " + Range(type, declaredRanges));
        if (notes.Length > 0) b.AppendLine("        description: " + Scalar(notes));
        if (lower != "0") b.AppendLine("        required: true");
        bool many = upper == "*" || upper.Equals("n", StringComparison.OrdinalIgnoreCase) || (int.TryParse(upper, out var max) && max > 1);
        if (many) b.AppendLine("        multivalued: true");
        if (many && int.TryParse(lower, out var min) && min > 0) b.AppendLine("        minimum_cardinality: " + min);
        if (many && int.TryParse(upper, out max)) b.AppendLine("        maximum_cardinality: " + max);
        if (readOnly) b.AppendLine("        readonly: true");
        if (identifier) b.AppendLine("        identifier: true");
        WriteAnnotations(b, 8, annotations);
    }

    private static bool IsExportedDirection(UmlRelation relation, short classId)
    {
        // EA stores the role/cardinality for Client -> Supplier on SupplierEnd.
        // Explicit navigability wins. Older toolkit imports did not set navigability,
        // so an unspecified connector safely falls back to Client -> Supplier only.
        if (classId == relation.SourceId)
            return relation.TargetNavigable || (!relation.SourceNavigable && !relation.TargetNavigable);
        return classId == relation.TargetId && relation.SourceNavigable;
    }

    private static string PackageAnnotation(ModelSnapshot model, string key, string? path)
    {
        string original = Annotation(model, key);
        if (path is null) return original;
        // Actual EA ownership wins over any stale, previously imported metadata.
        var annotations = original.Length > 0 ? JsonNode.Parse(original) as JsonObject
            ?? throw new InvalidDataException("Invalid annotations for " + key) : new JsonObject();
        annotations["ea_package_path"] = path;
        return annotations.ToJsonString();
    }

    private static string Annotation(ModelSnapshot model, string key) =>
        model.LinkMlAnnotations.TryGetValue(key, out var value) ? value : "";

    private static void WriteAnnotations(StringBuilder b, int indent, string json)
    {
        if (json.Length > 0) b.Append(' ', indent).Append("annotations: ").AppendLine(json);
    }

    private static string Range(string type, IReadOnlySet<string> declaredRanges)
    {
        if (string.IsNullOrWhiteSpace(type)) return "string";
        string? primitive = type.Trim().ToLowerInvariant() switch
        {
            "string" or "char" or "varchar" or "text" => "string",
            "int" or "integer" or "short" or "long" => "integer",
            "float" or "double" or "decimal" or "real" => "float",
            "bool" or "boolean" => "boolean",
            "date" => "date",
            "datetime" or "date-time" => "datetime",
            "uri" or "url" => "uri",
            _ => null
        };
        if (primitive is not null) return primitive;
        string identifier = Identifier(type);
        return declaredRanges.Contains(identifier) ? identifier : "string";
    }

    internal static string Identifier(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), "[^A-Za-z0-9_]", "_");
        if (cleaned.Length == 0) return "unnamed";
        return char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
    }

    private static string LowerCamel(string value) => value.Length == 0 ? "related" : char.ToLowerInvariant(value[0]) + value[1..];
    private static string Unique(string value, HashSet<string> names)
    {
        var candidate = Identifier(value);
        var root = candidate;
        int suffix = 2;
        while (!names.Add(candidate)) candidate = root + "_" + suffix++;
        return candidate;
    }
    private static string Lower(string value) => value.Contains("..") ? value.Split('.')[0] : value == "*" ? "0" : value;
    private static string Upper(string value) => value.Contains("..") ? value.Split('.')[2] : value;
    private static string Scalar(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
