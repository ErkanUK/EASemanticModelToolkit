using System.Security;
using System.Text;
using System.Xml.Linq;

namespace EA17LinkMLExporter;

internal static class DiagramWriter
{
    private const int Width = 260;
    private const int Header = 34;
    private const int Row = 22;

    public static string WriteDrawIo(ModelSnapshot model)
    {
        var root = new XElement("root", new XElement("mxCell", new XAttribute("id", "0")),
            new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0")));
        root.Add(new XElement("mxCell", new XAttribute("id", "model-title"),
            new XAttribute("value", Esc(model.Name + " — EA package version " + VersionLabel(model))),
            new XAttribute("style", "text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontStyle=1;fontSize=16;"),
            new XAttribute("vertex", "1"), new XAttribute("parent", "1"),
            new XElement("mxGeometry", new XAttribute("x", "60"), new XAttribute("y", "10"),
                new XAttribute("width", "600"), new XAttribute("height", "30"), new XAttribute("as", "geometry"))));
        var positions = Positions(model);
        foreach (var cls in model.Classes)
        {
            var p = positions[cls.Id];
           
            var title = string.IsNullOrWhiteSpace(cls.Version)
            ? cls.Name
            : $"{cls.Name} (v{cls.Version})";

            var label = "<b>" + Esc(title) + "</b><hr>" +
            string.Join("<br>", cls.Properties.Select(a => Esc(a.Name + ": " + a.Type)));

            root.Add(new XElement("mxCell", new XAttribute("id", "c" + cls.Id), new XAttribute("value", label),
                new XAttribute("style", ClassStyle(cls.FillColor, cls.BorderColor, cls.FontColor)),
                new XAttribute("vertex", "1"), new XAttribute("parent", "1"),
                new XElement("mxGeometry", new XAttribute("x", p.X), new XAttribute("y", p.Y), new XAttribute("width", Width),
                    new XAttribute("height", Height(cls)), new XAttribute("as", "geometry"))));
        }
        foreach (var item in model.Enums)
        {
            var p = positions[item.Id];
            var label = "<b>«enumeration» " + Esc(item.Name) + "</b><hr>" + string.Join("<br>", item.Values.Select(Esc));
            
            root.Add(new XElement("mxCell", new XAttribute("id", "c" + item.Id), new XAttribute("value", label),
                new XAttribute("style", ClassStyle(item.FillColor, item.BorderColor, item.FontColor)),
                new XAttribute("vertex", "1"), new XAttribute("parent", "1"),
                new XElement("mxGeometry", new XAttribute("x", p.X), new XAttribute("y", p.Y), new XAttribute("width", Width),
                    new XAttribute("height", EnumHeight(item)), new XAttribute("as", "geometry"))));
        }
        int edgeId = 1;
        foreach (var relation in model.Relations)
        {
            var label = (relation.TargetRole.Length > 0 ? relation.TargetRole + " " : "") + relation.TargetMultiplicity;
            var style = (relation.Composition ? "endArrow=none;startArrow=diamondThin;startFill=1;html=1;" : "endArrow=none;startArrow=none;html=1;")
                + "strokeColor=" + relation.LineColor + ";fontColor=" + relation.LineColor + ";";
            root.Add(Edge("e" + edgeId++, relation.SourceId, relation.TargetId, label, style));
        }
        foreach (var cls in model.Classes)
        foreach (var parent in cls.Parents)
        {
            var target = model.Classes.FirstOrDefault(x => x.Name == parent);
            if (target is not null) root.Add(Edge("e" + edgeId++, cls.Id, target.Id, "", "endArrow=block;endFill=0;html=1;"));
        }
        var graph = new XElement("mxGraphModel", new XAttribute("dx", "1200"), new XAttribute("dy", "800"),
            new XAttribute("grid", "1"), new XAttribute("gridSize", "10"), new XAttribute("page", "1"), root);
        return new XDocument(new XElement("mxfile", new XAttribute("host", "Electron"), new XAttribute("agent", "EA17 LinkML Exporter"),
            new XElement("diagram", new XAttribute("id", "uml"),
                new XAttribute("name", "UML Class Model — version " + VersionLabel(model)), graph))).ToString();
    }

    public static string WriteSvg(ModelSnapshot model)
    {
        var positions = Positions(model);
        int count = model.Classes.Count + model.Enums.Count;
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        int rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        int canvasWidth = columns * 340 + 40, canvasHeight = rows * 280 + 40;
        var b = new StringBuilder();
        b.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{canvasWidth}\" height=\"{canvasHeight}\" viewBox=\"0 0 {canvasWidth} {canvasHeight}\">");
        b.AppendLine("<defs><marker id=\"triangle\" markerWidth=\"12\" markerHeight=\"12\" refX=\"11\" refY=\"6\" orient=\"auto\"><path d=\"M 0 0 L 12 6 L 0 12 z\" fill=\"white\" stroke=\"#475569\"/></marker></defs>");
        b.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\"/>");
        b.AppendLine($"<text x=\"20\" y=\"28\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" font-weight=\"600\">{Esc(model.Name + " — EA package version " + VersionLabel(model))}</text>");
        foreach (var rel in model.Relations) AddLine(b, positions[rel.SourceId], positions[rel.TargetId], false, rel.LineColor);
        foreach (var cls in model.Classes) foreach (var parent in cls.Parents)
        { var target = model.Classes.FirstOrDefault(x => x.Name == parent); if (target is not null) AddLine(b, positions[cls.Id], positions[target.Id], true, "#475569"); }
        foreach (var cls in model.Classes)
        {
            var title = string.IsNullOrWhiteSpace(cls.Version) ? cls.Name : $"{cls.Name} (v{cls.Version})";
            AddBox(b, title, cls.Properties.Select(x => x.Name + ": " + x.Type), positions[cls.Id], Height(cls), cls.FillColor, cls.BorderColor, cls.FontColor);
        }
        foreach (var item in model.Enums) AddBox(b, "«enumeration» " + item.Name, item.Values, positions[item.Id], EnumHeight(item), item.FillColor, item.BorderColor, item.FontColor);
        b.AppendLine("</svg>");
        return b.ToString();
    }

    private static XElement Edge(string id, int source, int target, string label, string style) =>
        new("mxCell", new XAttribute("id", id), new XAttribute("value", label), new XAttribute("style", style),
            new XAttribute("edge", "1"), new XAttribute("parent", "1"), new XAttribute("source", "c" + source),
            new XAttribute("target", "c" + target), new XElement("mxGeometry", new XAttribute("relative", "1"), new XAttribute("as", "geometry")));

    private static Dictionary<int, (int X, int Y)> Positions(ModelSnapshot model)
    {
        // EA interop versions expose element IDs as either Int16 or Int32.
        // Normalize at the diagram boundary so dictionary keys are stable in local and CI builds.
        var ids = model.Classes.Select(x => Convert.ToInt32(x.Id))
            .Concat(model.Enums.Select(x => Convert.ToInt32(x.Id)))
            .ToList();
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(ids.Count)));
        return ids.Select((id, i) => (id, Position: (X: 60 + i % columns * 340, Y: 60 + i / columns * 280)))
            .ToDictionary(x => x.id, x => x.Position);
    }

    private static int Height(UmlClass value) => Math.Max(80, Header + value.Properties.Count * Row + 12);
    private static int EnumHeight(UmlEnum value) => Math.Max(80, Header + value.Values.Count * Row + 12);
    private static string VersionLabel(ModelSnapshot model) => string.IsNullOrWhiteSpace(model.Version) ? "not set" : model.Version;
    private static string ClassStyle(string fill, string border, string font) =>
        "swimlane;fontStyle=1;childLayout=stackLayout;horizontal=1;startSize=30;html=1;rounded=0;" +
        $"fillColor={fill};strokeColor={border};fontColor={font};swimlaneFillColor={fill};";
    private static string Esc(string value) => SecurityElement.Escape(value) ?? "";
    private static void AddLine(StringBuilder b, (int X, int Y) a, (int X, int Y) z, bool inheritance, string color) =>
        b.AppendLine($"<line x1=\"{a.X + Width / 2}\" y1=\"{a.Y + 60}\" x2=\"{z.X + Width / 2}\" y2=\"{z.Y + 60}\" stroke=\"{color}\" stroke-width=\"2\" {(inheritance ? "marker-end=\"url(#triangle)\"" : "")}/>");

    private static void AddBox(StringBuilder b, string title, IEnumerable<string> rows, (int X, int Y) p, int height,
        string fillColor, string borderColor, string fontColor)
    {
        b.AppendLine($"<rect x=\"{p.X}\" y=\"{p.Y}\" width=\"{Width}\" height=\"{height}\" rx=\"3\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"2\"/>");
        b.AppendLine($"<line x1=\"{p.X}\" y1=\"{p.Y + Header}\" x2=\"{p.X + Width}\" y2=\"{p.Y + Header}\" stroke=\"{borderColor}\"/>");
        b.AppendLine($"<text x=\"{p.X + Width / 2}\" y=\"{p.Y + 22}\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" font-weight=\"600\" fill=\"{fontColor}\">{Esc(title)}</text>");
        int y = p.Y + Header + 18;
        foreach (var row in rows) { b.AppendLine($"<text x=\"{p.X + 10}\" y=\"{y}\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\" fill=\"{fontColor}\">{Esc(row)}</text>"); y += Row; }
    }
}
