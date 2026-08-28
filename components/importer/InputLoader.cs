using System.Text.Json.Nodes;

namespace EAJsonModelImporter;

internal static class InputLoader
{
    public static JsonNode Load(string path)
    {
        var text = File.ReadAllText(path);
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return JsonNode.Parse(text) ?? throw new InvalidDataException("The JSON document is empty.");

        return SimpleYaml.Parse(text);
    }

    public static string ExtractYamlComments(string path)
    {
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) return "";
        return string.Join("\n", File.ReadLines(path)
            .Where(line => line.TrimStart().StartsWith('#')));
    }
}
