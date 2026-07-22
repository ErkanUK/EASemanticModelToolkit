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
}
