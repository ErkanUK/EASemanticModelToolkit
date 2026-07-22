using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EA17LinkMLExporter;

internal static class JsonSchemaWriter
{
    public static string Write(ModelSnapshot model)
    {
        var classNames = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enumNames = model.Enums.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var properties = Properties(model);
        var definitions = new JsonObject();

        foreach (var item in model.Enums.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var values = new JsonArray();
            foreach (string value in item.Values) values.Add(value);
            definitions[item.Name] = new JsonObject
            {
                ["title"] = item.Name,
                ["description"] = Optional(item.Notes),
                ["type"] = "string",
                ["enum"] = values
            };
        }

        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var objectProperties = new JsonObject();
            var required = new JsonArray();
            foreach (var property in properties.Where(x => x.Owner.Equals(cls.Name, StringComparison.OrdinalIgnoreCase)))
            {
                JsonObject schema = PropertySchema(property, classNames, enumNames);
                objectProperties[property.Name] = schema;
                if (Lower(property.Lower) > 0) required.Add(property.Name);
            }
            var definition = new JsonObject
            {
                ["title"] = cls.Name,
                ["description"] = Optional(cls.Notes),
                ["type"] = "object",
                ["properties"] = objectProperties
            };
            if (required.Count > 0) definition["required"] = required;
            if (cls.Abstract) definition["x-ea-abstract"] = true;
            if (!string.IsNullOrWhiteSpace(cls.Version)) definition["x-ea-version"] = cls.Version;
            if (cls.Parents.Count > 0)
            {
                var parents = new JsonArray();
                foreach (var parent in cls.Parents.Where(classNames.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
                    parents.Add(new JsonObject { ["$ref"] = Reference(parent) });
                if (parents.Count > 0) definition["allOf"] = parents;
            }
            definitions[cls.Name] = definition;
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = model.OntologyIri,
            ["title"] = model.Name,
            ["description"] = Optional(model.Notes),
            ["$defs"] = definitions
        };
        if (model.Version.Length > 0) root["version"] = model.Version;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject PropertySchema(SchemaProperty property, ISet<string> classes, ISet<string> enums)
    {
        bool reference = property.Object || classes.Contains(property.Type) || enums.Contains(property.Type);
        JsonObject item = reference
            ? new JsonObject { ["$ref"] = classes.Contains(property.Type) || enums.Contains(property.Type)
                ? Reference(property.Type) : null }
            : Primitive(property.Type);
        if (item["$ref"] is null) item.Remove("$ref");
        if (property.Notes.Length > 0) item["description"] = property.Notes;
        if (property.ReadOnly) item["readOnly"] = true;
        if (property.Derived) item["x-ea-derived"] = true;
        if (property.Identifier) item["x-ea-identifier"] = true;
        if (!Many(property.Upper)) return item;
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = item,
            ["description"] = Optional(property.Notes)
        };
    }

    private static JsonObject Primitive(string type) => type.ToLowerInvariant() switch
    {
        "integer" or "int" or "long" => new JsonObject { ["type"] = "integer" },
        "real" or "float" or "double" or "decimal" or "number" => new JsonObject { ["type"] = "number" },
        "boolean" or "bool" => new JsonObject { ["type"] = "boolean" },
        "date" => new JsonObject { ["type"] = "string", ["format"] = "date" },
        "datetime" or "date_time" or "date-time" => new JsonObject { ["type"] = "string", ["format"] = "date-time" },
        "time" => new JsonObject { ["type"] = "string", ["format"] = "time" },
        "object" or "any" => new JsonObject(),
        _ => new JsonObject { ["type"] = "string" }
    };

    private static List<SchemaProperty> Properties(ModelSnapshot model)
    {
        var result = new List<SchemaProperty>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in model.Classes)
        foreach (var property in cls.Properties)
            Add(cls.Name, property.Name, property.Type, false, property.Notes, property.Lower, property.Upper,
                property.Derived, property.ReadOnly, property.Identifier);
        foreach (var relation in model.Relations)
        {
            Add(relation.SourceName, Role(relation.TargetRole, relation.TargetName), relation.TargetName, true,
                relation.Notes, Bound(relation.TargetMultiplicity, false), Bound(relation.TargetMultiplicity, true),
                false, false, false);
            if (!string.IsNullOrWhiteSpace(relation.SourceRole))
                Add(relation.TargetName, relation.SourceRole, relation.SourceName, true, relation.Notes,
                    Bound(relation.SourceMultiplicity, false), Bound(relation.SourceMultiplicity, true),
                    false, false, false);
        }
        return result;

        void Add(string owner, string preferredName, string type, bool isObject, string notes, string lower,
            string upper, bool derived, bool readOnly, bool identifier)
        {
            string name = preferredName;
            int suffix = 2;
            while (!used.Add(owner + "|" + name)) name = preferredName + "_" + suffix++;
            result.Add(new SchemaProperty(owner, name, type, isObject, notes, lower, upper, derived, readOnly, identifier));
        }
    }

    private static string Bound(string multiplicity, bool upper)
    {
        if (!multiplicity.Contains("..")) return multiplicity;
        string[] parts = multiplicity.Split("..", StringSplitOptions.None);
        return parts[upper ? 1 : 0];
    }
    private static int Lower(string value) => int.TryParse(value, out int number) ? number : 0;
    private static bool Many(string value) => value == "*" || (int.TryParse(value, out int number) && number > 1);
    private static string Role(string role, string target) => string.IsNullOrWhiteSpace(role) ? "has" + Local(target) : role;
    private static string Local(string value)
    {
        string local = Regex.Replace(value, "[^A-Za-z0-9]+", " ").Trim();
        return string.Concat(local.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
    }
    private static string Reference(string name) => "#/$defs/" + name.Replace("~", "~0").Replace("/", "~1");
    private static JsonNode? Optional(string value) => value.Length == 0 ? null : JsonValue.Create(value);

    private sealed record SchemaProperty(string Owner, string Name, string Type, bool Object, string Notes,
        string Lower, string Upper, bool Derived, bool ReadOnly, bool Identifier);
}
