using System.Text;
using System.Text.RegularExpressions;

namespace EA17LinkMLExporter;

internal static class LinkMlWriter
{
    public static string Write(ModelSnapshot model)
    {
        var schemaName = Identifier(model.Name);
        var b = new StringBuilder();
        b.AppendLine("id: https://example.org/" + schemaName);
        b.AppendLine("name: " + Scalar(schemaName));
        if (model.Version.Length > 0) b.AppendLine("version: " + Scalar(model.Version));
        if (model.Notes.Length > 0) b.AppendLine("description: " + Scalar(model.Notes));
        b.AppendLine("prefixes:");
        b.AppendLine("  linkml: https://w3id.org/linkml/");
        b.AppendLine("  " + schemaName + ": https://example.org/" + schemaName + "/");
        b.AppendLine("default_prefix: " + schemaName);
        b.AppendLine("imports:");
        b.AppendLine("  - linkml:types");

        if (model.Enums.Count > 0)
        {
            b.AppendLine("enums:");
            foreach (var item in model.Enums.OrderBy(x => x.Name))
            {
                b.AppendLine("  " + Identifier(item.Name) + ":");
                if (item.Notes.Length > 0) b.AppendLine("    description: " + Scalar(item.Notes));
                b.AppendLine("    permissible_values:");
                foreach (var value in item.Values) b.AppendLine("      " + Scalar(value) + ":");
            }
        }

        b.AppendLine("classes:");
        foreach (var cls in model.Classes.OrderBy(x => x.Name))
        {
            b.AppendLine("  " + Identifier(cls.Name) + ":");
            if (cls.Notes.Length > 0) b.AppendLine("    description: " + Scalar(cls.Notes));
            if (!string.IsNullOrWhiteSpace(cls.Version)) b.AppendLine("    version: " + Scalar(cls.Version));
            if (cls.Abstract) b.AppendLine("    abstract: true");
            if (cls.Parents.Count > 0) b.AppendLine("    is_a: " + Identifier(cls.Parents[0]));
            if (cls.Parents.Count > 1)
            {
                b.AppendLine("    mixins:");
                foreach (var parent in cls.Parents.Skip(1)) b.AppendLine("      - " + Identifier(parent));
            }

            var associations = model.Relations.Where(x => x.SourceId == cls.Id || x.TargetId == cls.Id).ToList();
            if (cls.Properties.Count == 0 && associations.Count == 0) continue;
            b.AppendLine("    attributes:");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in cls.Properties)
                WriteAttribute(b, Unique(property.Name, names), property.Type, property.Notes, property.Lower, property.Upper, property.ReadOnly);
            foreach (var relation in associations)
            {
                bool forward = relation.SourceId == cls.Id;
                string role = forward ? relation.TargetRole : relation.SourceRole;
                string other = forward ? relation.TargetName : relation.SourceName;
                string multiplicity = forward ? relation.TargetMultiplicity : relation.SourceMultiplicity;
                WriteAttribute(b, Unique(string.IsNullOrWhiteSpace(role) ? LowerCamel(other) : role, names), other, relation.Notes,
                    Lower(multiplicity), Upper(multiplicity), false);
            }
        }
        return b.ToString();
    }

    private static void WriteAttribute(StringBuilder b, string name, string type, string notes, string lower, string upper, bool readOnly)
    {
        b.AppendLine("      " + Identifier(name) + ":");
        b.AppendLine("        range: " + Range(type));
        if (notes.Length > 0) b.AppendLine("        description: " + Scalar(notes));
        if (lower != "0") b.AppendLine("        required: true");
        bool many = upper == "*" || upper.Equals("n", StringComparison.OrdinalIgnoreCase) || (int.TryParse(upper, out var max) && max > 1);
        if (many) b.AppendLine("        multivalued: true");
        if (many && int.TryParse(lower, out var min) && min > 0) b.AppendLine("        minimum_cardinality: " + min);
        if (many && int.TryParse(upper, out max)) b.AppendLine("        maximum_cardinality: " + max);
        if (readOnly) b.AppendLine("        readonly: true");
    }

    private static string Range(string type) => type.Trim().ToLowerInvariant() switch
    {
        "string" or "char" or "varchar" or "text" => "string",
        "int" or "integer" or "short" or "long" => "integer",
        "float" or "double" or "decimal" or "real" => "float",
        "bool" or "boolean" => "boolean",
        "date" => "date",
        "datetime" or "date-time" => "datetime",
        "uri" or "url" => "uri",
        _ => Identifier(type)
    };

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
    private static string Scalar(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
}
