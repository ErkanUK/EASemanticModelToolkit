using System.Globalization;
using System.Text.Json.Nodes;

namespace EAJsonModelImporter;

// Dependency-free YAML reader for the JSON-compatible YAML used by schemas and data files.
internal static class SimpleYaml
{
    public static JsonNode Parse(string text)
    {
        var lines = text.Replace("\r", "").Split('\n')
            .Select(CleanLine).Where(x => x.Text.Length > 0 && x.Text is not "---" and not "...").ToList();
        if (lines.Count == 0) throw new InvalidDataException("The YAML document is empty.");
        int index = 0;
        return ParseBlock(lines, ref index, lines[0].Indent);
    }

    private static JsonNode ParseBlock(List<Line> lines, ref int index, int indent) =>
        lines[index].Indent == indent && lines[index].Text.StartsWith("- ", StringComparison.Ordinal)
            ? ParseArray(lines, ref index, indent) : ParseObject(lines, ref index, indent);

    private static JsonObject ParseObject(List<Line> lines, ref int index, int indent)
    {
        var obj = new JsonObject();
        while (index < lines.Count && lines[index].Indent == indent && !lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
        {
            var line = lines[index++];
            AddMapping(obj, line.Text, lines, ref index, indent);
        }
        return obj;
    }

    private static JsonArray ParseArray(List<Line> lines, ref int index, int indent)
    {
        var array = new JsonArray();
        while (index < lines.Count && lines[index].Indent == indent && lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
        {
            string content = lines[index++].Text[2..].Trim();
            if (content.Length == 0)
            {
                array.Add(index < lines.Count && lines[index].Indent > indent
                    ? ParseBlock(lines, ref index, lines[index].Indent) : null);
                continue;
            }
            if (FindColon(content) >= 0)
            {
                var item = new JsonObject();
                AddMapping(item, content, lines, ref index, indent + 2);
                while (index < lines.Count && lines[index].Indent == indent + 2 && !lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
                {
                    var continuation = lines[index++];
                    AddMapping(item, continuation.Text, lines, ref index, indent + 2);
                }
                array.Add(item);
            }
            else array.Add(Scalar(content));
        }
        return array;
    }

    private static void AddMapping(JsonObject obj, string text, List<Line> lines, ref int index, int indent)
    {
        int colon = FindColon(text);
        if (colon < 0) throw new InvalidDataException("Invalid YAML mapping: " + text);
        string key = Unquote(text[..colon].Trim());
        string value = text[(colon + 1)..].Trim();
        if (value is "|" or ">")
        {
            var parts = new List<string>();
            while (index < lines.Count && lines[index].Indent > indent) parts.Add(lines[index++].Text);
            obj[key] = string.Join(value == ">" ? " " : "\n", parts);
        }
        else if (value.Length > 0) obj[key] = Scalar(value);
        else if (index < lines.Count && lines[index].Indent > indent)
            obj[key] = ParseBlock(lines, ref index, lines[index].Indent);
        else obj[key] = null;
    }

    private static JsonNode? Scalar(string value)
    {
        value = value.Trim();
        if ((value.StartsWith('[') && value.EndsWith(']')) || (value.StartsWith('{') && value.EndsWith('}')))
        {
            try { return JsonNode.Parse(value.Replace("'", "\"")); } catch { /* treat as text */ }
        }
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            var array = new JsonArray();
            string content = value[1..^1].Trim();
            if (content.Length == 0) return array;
            foreach (var item in content.Split(',')) array.Add(Scalar(item.Trim()));
            return array;
        }
        if (value is "null" or "Null" or "NULL" or "~") return null;
        if (bool.TryParse(value, out var boolean)) return JsonValue.Create(boolean);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return JsonValue.Create(integer);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return JsonValue.Create(number);
        return JsonValue.Create(Unquote(value));
    }

    private static Line CleanLine(string source)
    {
        int indent = source.TakeWhile(char.IsWhiteSpace).Count();
        string text = source[indent..];
        bool quoted = false; char quote = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            if ((text[i] is '\'' or '"') && (i == 0 || text[i - 1] != '\\'))
            { if (!quoted) { quoted = true; quote = text[i]; } else if (quote == text[i]) quoted = false; }
            if (text[i] == '#' && !quoted && (i == 0 || char.IsWhiteSpace(text[i - 1]))) { text = text[..i]; break; }
        }
        return new Line(indent, text.TrimEnd());
    }

    private static int FindColon(string value)
    {
        bool quoted = false; char quote = '\0';
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] is '\'' or '"') { if (!quoted) { quoted = true; quote = value[i]; } else if (quote == value[i]) quoted = false; }
            if (value[i] == ':' && !quoted && (i + 1 == value.Length || char.IsWhiteSpace(value[i + 1]))) return i;
        }
        return -1;
    }

    private static string Unquote(string value) => value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;
    private readonly record struct Line(int Indent, string Text);
}
