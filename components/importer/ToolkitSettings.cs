using System.Text.Json;

namespace EAJsonModelImporter;

internal sealed class ToolkitSettings
{
    private const string FileName = "semantic-model-toolkit.settings.json";

    public DiagramDefaults DiagramDefaults { get; set; } = new();

    public static ToolkitSettings Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path)) return new ToolkitSettings();

        try
        {
            return JsonSerializer.Deserialize<ToolkitSettings>(File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new ToolkitSettings();
        }
        catch
        {
            // A malformed optional settings file must not prevent EA from importing a model.
            return new ToolkitSettings();
        }
    }
}

internal sealed class DiagramDefaults
{
    public string ConnectorLineStyle { get; set; } = "OrthogonalSquare";
    public string Orientation { get; set; } = "Landscape";

    public EA.LinkLineStyle LineStyle() =>
        ConnectorLineStyle.Trim().ToLowerInvariant() switch
        {
            "direct" => EA.LinkLineStyle.LineStyleDirect,
            "autorouting" or "auto-routing" => EA.LinkLineStyle.LineStyleAutoRouting,
            "custom" or "customline" => EA.LinkLineStyle.LineStyleCustomLine,
            "treevertical" => EA.LinkLineStyle.LineStyleTreeVertical,
            "treehorizontal" => EA.LinkLineStyle.LineStyleTreeHorizontal,
            "lateralvertical" => EA.LinkLineStyle.LineStyleLateralVertical,
            "lateralhorizontal" => EA.LinkLineStyle.LineStyleLateralHorizontal,
            "orthogonalrounded" or "orthogonal-rounded" => EA.LinkLineStyle.LineStyleOrthogonalRounded,
            _ => EA.LinkLineStyle.LineStyleOrthogonalSquare
        };

    public bool IsLandscape() =>
        Orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase)
        || Orientation.Equals("L", StringComparison.OrdinalIgnoreCase);
}
