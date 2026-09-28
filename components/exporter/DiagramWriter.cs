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
        var layout = DomainClusterLayout(model);
        var positions = layout.Positions;
        int canvasWidth = layout.Width, canvasHeight = layout.Height;
        var b = new StringBuilder();
        b.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{canvasWidth}\" height=\"{canvasHeight}\" viewBox=\"0 0 {canvasWidth} {canvasHeight}\">");
        b.AppendLine("<defs><marker id=\"triangle\" markerWidth=\"12\" markerHeight=\"12\" refX=\"11\" refY=\"6\" orient=\"auto\"><path d=\"M 0 0 L 12 6 L 0 12 z\" fill=\"white\" stroke=\"#475569\"/></marker></defs>");
        b.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\"/>");
        b.AppendLine($"<text x=\"20\" y=\"28\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" font-weight=\"600\">{Esc(model.Name + " - EA package version " + VersionLabel(model))}</text>");
        foreach (var cluster in layout.Clusters)
        {
            b.AppendLine($"<g class=\"domain-cluster\" data-domain=\"{Esc(cluster.Name)}\">");
            b.AppendLine($"<rect x=\"{cluster.X}\" y=\"{cluster.Y}\" width=\"{cluster.Width}\" height=\"{cluster.Height}\" rx=\"12\" fill=\"{cluster.Color}\" fill-opacity=\"0.13\" stroke=\"{cluster.Color}\" stroke-width=\"3\"/>");
            b.AppendLine($"<text x=\"{cluster.X + 20}\" y=\"{cluster.Y + 29}\" font-family=\"Segoe UI, sans-serif\" font-size=\"17\" font-weight=\"700\" fill=\"#1e293b\">{Esc(DisplayDomain(cluster.Name))}</text>");
            b.AppendLine("</g>");
        }
        var boxes = model.Classes.Select(cls => new DiagramBox(cls.Id, positions[cls.Id], Height(cls)))
            .Concat(model.Enums.Select(item => new DiagramBox(item.Id, positions[item.Id], EnumHeight(item))))
            .ToList();
        int routeIndex = 0;
        foreach (var rel in model.Relations.Where(rel => IsCrossDomainRelation(model, rel)))
            AddLine(b, positions[rel.SourceId], positions[rel.TargetId], false, rel.LineColor, routeIndex++, boxes);
        foreach (var cls in model.Classes) foreach (var parent in cls.Parents)
        {
            var target = model.Classes.FirstOrDefault(x => x.Name == parent);
            if (target is not null && IsCrossDomainRelation(cls, target))
                AddLine(b, positions[cls.Id], positions[target.Id], true, "#475569", routeIndex++, boxes);
        }
        foreach (var cls in model.Classes)
        {
            var title = string.IsNullOrWhiteSpace(cls.Version) ? cls.Name : $"{cls.Name} (v{cls.Version})";
            AddBox(b, title, cls.Properties.Select(x => x.Name + ": " + x.Type), positions[cls.Id], Height(cls), cls.FillColor, cls.BorderColor, cls.FontColor);
        }
        foreach (var item in model.Enums) AddBox(b, "«enumeration» " + item.Name, item.Values, positions[item.Id], EnumHeight(item), item.FillColor, item.BorderColor, item.FontColor);
        b.AppendLine("</svg>");
        return b.ToString();
    }

    private static SvgLayout DomainClusterLayout(ModelSnapshot model)
    {
        var groups = model.Classes.GroupBy(x => x.Domains.FirstOrDefault() ?? "Other", StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildCluster(group.Key, group.OrderBy(x => x.Name).Cast<object>().ToList(),
                model.DomainDiagramPositions.TryGetValue(group.Key, out var positions) ? positions : null))
            .OrderBy(x => x.Name.Equals("Other", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (model.Enums.Count > 0)
            groups.Add(BuildCluster("Enumerations", model.Enums.OrderBy(x => x.Name).Cast<object>().ToList()));

        const int margin = 40, gap = 90, clustersPerRow = 2;
        var positions = new Dictionary<int, (int X, int Y)>();
        var placed = new List<SvgCluster>();
        int y = 60, canvasRight = margin;
        for (int index = 0; index < groups.Count; index += clustersPerRow)
        {
            var row = groups.Skip(index).Take(clustersPerRow).ToList();
            int x = margin;
            int rowHeight = row.Max(item => item.Height);
            foreach (var cluster in row)
            {
                foreach (var item in cluster.Positions)
                    positions[item.Key] = (item.Value.X + x, item.Value.Y + y);
                placed.Add(new SvgCluster(cluster.Name, cluster.Color, x, y, cluster.Width, cluster.Height));
                canvasRight = Math.Max(canvasRight, x + cluster.Width);
                x += cluster.Width + gap;
            }
            y += rowHeight + gap;
        }
        return new SvgLayout(positions, placed, canvasRight + margin, y - gap + margin);
    }

    private static LocalCluster BuildCluster(string name, IReadOnlyList<object> nodes,
        IReadOnlyDictionary<int, DiagramPosition>? sourcePositions = null)
    {
        const int padding = 28, titleHeight = 48, horizontalGap = 50, verticalGap = 45;
        if (sourcePositions is not null && nodes.All(node => sourcePositions.ContainsKey(NodeId(node))))
        {
            var source = nodes.Select(node => (Id: NodeId(node), Position: sourcePositions[NodeId(node)])).ToList();
            int left = source.Min(item => item.Position.Left), top = source.Min(item => item.Position.Top);
            int right = source.Max(item => item.Position.Right), bottom = source.Max(item => item.Position.Bottom);
            var preservedPositions = source.ToDictionary(item => item.Id,
                item => (X: padding + item.Position.Left - left, Y: titleHeight + padding + item.Position.Top - top));
            string preservedColor = nodes.OfType<UmlClass>().Select(x => x.FillColor).FirstOrDefault() ?? "#E2E8F0";
            return new LocalCluster(name, preservedColor, Math.Max(Width + padding * 2, right - left + padding * 2),
                Math.Max(titleHeight + padding * 2 + 80, titleHeight + bottom - top + padding * 2), preservedPositions);
        }

        int columns = Math.Min(3, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(nodes.Count))));
        int rows = Math.Max(1, (int)Math.Ceiling(nodes.Count / (double)columns));
        var heights = nodes.Select(NodeHeight).ToList();
        var rowHeights = Enumerable.Range(0, rows)
            .Select(row => Enumerable.Range(row * columns, Math.Min(columns, nodes.Count - row * columns))
                .Select(i => heights[i]).DefaultIfEmpty(0).Max()).ToList();
        int width = padding * 2 + columns * Width + Math.Max(0, columns - 1) * horizontalGap;
        int height = titleHeight + padding + rowHeights.Sum() + Math.Max(0, rows - 1) * verticalGap + padding;
        var positions = new Dictionary<int, (int X, int Y)>();
        int y = titleHeight + padding;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                if (index >= nodes.Count) break;
                positions[NodeId(nodes[index])] = (padding + column * (Width + horizontalGap), y);
            }
            y += rowHeights[row] + verticalGap;
        }
        string color = nodes.OfType<UmlClass>().Select(x => x.FillColor).FirstOrDefault() ?? "#E2E8F0";
        return new LocalCluster(name, color, width, height, positions);
    }

    private static int NodeId(object node) => node switch
    {
        UmlClass cls => cls.Id,
        UmlEnum item => item.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(node))
    };

    private static int NodeHeight(object node) => node switch
    {
        UmlClass cls => Height(cls),
        UmlEnum item => EnumHeight(item),
        _ => 80
    };

    private static string DisplayDomain(string value) => string.Join(" ", value.Replace('-', '_')
        .Split('_', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));

    private static bool IsCrossDomainRelation(ModelSnapshot model, UmlRelation relation)
    {
        var source = model.Classes.FirstOrDefault(x => x.Id == relation.SourceId);
        var target = model.Classes.FirstOrDefault(x => x.Id == relation.TargetId);
        return source is not null && target is not null && IsCrossDomainRelation(source, target);
    }

    private static bool IsCrossDomainRelation(UmlClass source, UmlClass target) =>
        !(source.Domains.FirstOrDefault() ?? "Other").Equals(target.Domains.FirstOrDefault() ?? "Other",
            StringComparison.OrdinalIgnoreCase);

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
    private static void AddLine(StringBuilder b, (int X, int Y) a, (int X, int Y) z, bool inheritance, string color,
        int routeIndex, IReadOnlyList<DiagramBox> boxes)
    {
        // SVG has no diagram router. Use separate outside lanes so a connector
        // starts/ends at a box edge and cannot run through class content.
        var source = boxes.First(box => box.Position == a);
        var target = boxes.First(box => box.Position == z);
        int sourceX = source.CenterX, sourceY = source.CenterY;
        int targetX = target.CenterX, targetY = target.CenterY;
        string route;
        if (Math.Abs(targetX - sourceX) >= Math.Abs(targetY - sourceY))
        {
            int laneY = ChooseHorizontalLane(source, target, boxes, routeIndex);
            int sourceEdgeY = laneY < source.Top ? source.Top : source.Bottom;
            int targetEdgeY = laneY < target.Top ? target.Top : target.Bottom;
            route = $"M {sourceX} {sourceEdgeY} V {laneY} H {targetX} V {targetEdgeY}";
        }
        else
        {
            int laneX = ChooseVerticalLane(source, target, boxes, routeIndex);
            int sourceEdgeX = laneX < source.Left ? source.Left : source.Right;
            int targetEdgeX = laneX < target.Left ? target.Left : target.Right;
            route = $"M {sourceEdgeX} {sourceY} H {laneX} V {targetY} H {targetEdgeX}";
        }
        b.AppendLine($"<path d=\"{route}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linejoin=\"miter\" {(inheritance ? "marker-end=\"url(#triangle)\"" : "")}/>");
    }

    private static int ChooseHorizontalLane(DiagramBox source, DiagramBox target,
        IReadOnlyList<DiagramBox> boxes, int routeIndex)
    {
        const int clearance = 18;
        int ideal = (source.CenterY + target.CenterY) / 2;
        var candidates = boxes.SelectMany(box => new[] { box.Top - clearance, box.Bottom + clearance })
            .Append(ideal).Distinct()
            .Where(y => (y < source.Top || y > source.Bottom) && (y < target.Top || y > target.Bottom))
            .Where(y => IsHorizontalRouteClear(source, target, y, boxes))
            .OrderBy(y => Math.Abs(source.CenterY - y) + Math.Abs(target.CenterY - y)).ThenBy(y => y)
            .ToList();
        return SelectLane(candidates, ideal, routeIndex,
            boxes.Min(box => box.Top) - clearance, boxes.Max(box => box.Bottom) + clearance);
    }

    private static int ChooseVerticalLane(DiagramBox source, DiagramBox target,
        IReadOnlyList<DiagramBox> boxes, int routeIndex)
    {
        const int clearance = 18;
        int ideal = (source.CenterX + target.CenterX) / 2;
        var candidates = boxes.SelectMany(box => new[] { box.Left - clearance, box.Right + clearance })
            .Append(ideal).Distinct()
            .Where(x => (x < source.Left || x > source.Right) && (x < target.Left || x > target.Right))
            .Where(x => IsVerticalRouteClear(source, target, x, boxes))
            .OrderBy(x => Math.Abs(source.CenterX - x) + Math.Abs(target.CenterX - x)).ThenBy(x => x)
            .ToList();
        return SelectLane(candidates, ideal, routeIndex,
            boxes.Min(box => box.Left) - clearance, boxes.Max(box => box.Right) + clearance);
    }

    private static int SelectLane(IReadOnlyList<int> candidates, int ideal, int routeIndex, int lowFallback,
        int highFallback)
    {
        if (candidates.Count == 0)
            return Math.Abs(ideal - lowFallback) <= Math.Abs(ideal - highFallback) ? lowFallback : highFallback;
        // Spread neighbouring connectors over the closest few valid lanes without
        // sending later relationships progressively outside the SVG view box.
        return candidates[routeIndex % Math.Min(3, candidates.Count)];
    }

    private static bool IsHorizontalRouteClear(DiagramBox source, DiagramBox target, int laneY,
        IReadOnlyList<DiagramBox> boxes)
    {
        int sourceEdgeY = laneY < source.Top ? source.Top : source.Bottom;
        int targetEdgeY = laneY < target.Top ? target.Top : target.Bottom;
        return boxes.Where(box => box.Id != source.Id && box.Id != target.Id).All(box =>
            !IntersectsVertical(source.CenterX, sourceEdgeY, laneY, box) &&
            !IntersectsHorizontal(laneY, source.CenterX, target.CenterX, box) &&
            !IntersectsVertical(target.CenterX, laneY, targetEdgeY, box));
    }

    private static bool IsVerticalRouteClear(DiagramBox source, DiagramBox target, int laneX,
        IReadOnlyList<DiagramBox> boxes)
    {
        int sourceEdgeX = laneX < source.Left ? source.Left : source.Right;
        int targetEdgeX = laneX < target.Left ? target.Left : target.Right;
        return boxes.Where(box => box.Id != source.Id && box.Id != target.Id).All(box =>
            !IntersectsHorizontal(source.CenterY, sourceEdgeX, laneX, box) &&
            !IntersectsVertical(laneX, source.CenterY, target.CenterY, box) &&
            !IntersectsHorizontal(target.CenterY, laneX, targetEdgeX, box));
    }

    private static bool IntersectsHorizontal(int y, int x1, int x2, DiagramBox box) =>
        y > box.Top - 8 && y < box.Bottom + 8 && Math.Max(x1, x2) > box.Left - 8 && Math.Min(x1, x2) < box.Right + 8;

    private static bool IntersectsVertical(int x, int y1, int y2, DiagramBox box) =>
        x > box.Left - 8 && x < box.Right + 8 && Math.Max(y1, y2) > box.Top - 8 && Math.Min(y1, y2) < box.Bottom + 8;

    private static void AddBox(StringBuilder b, string title, IEnumerable<string> rows, (int X, int Y) p, int height,
        string fillColor, string borderColor, string fontColor)
    {
        b.AppendLine($"<rect x=\"{p.X}\" y=\"{p.Y}\" width=\"{Width}\" height=\"{height}\" rx=\"3\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"2\"/>");
        b.AppendLine($"<line x1=\"{p.X}\" y1=\"{p.Y + Header}\" x2=\"{p.X + Width}\" y2=\"{p.Y + Header}\" stroke=\"{borderColor}\"/>");
        b.AppendLine($"<text x=\"{p.X + Width / 2}\" y=\"{p.Y + 22}\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" font-weight=\"600\" fill=\"{fontColor}\">{Esc(title)}</text>");
        int y = p.Y + Header + 18;
        foreach (var row in rows) { b.AppendLine($"<text x=\"{p.X + 10}\" y=\"{y}\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\" fill=\"{fontColor}\">{Esc(row)}</text>"); y += Row; }
    }

    private sealed record LocalCluster(string Name, string Color, int Width, int Height,
        IReadOnlyDictionary<int, (int X, int Y)> Positions);
    private sealed record DiagramBox(int Id, (int X, int Y) Position, int Height)
    {
        public int Left => Position.X;
        public int Top => Position.Y;
        public int Right => Position.X + Width;
        public int Bottom => Position.Y + Height;
        public int CenterX => Position.X + Width / 2;
        public int CenterY => Position.Y + Height / 2;
    }
    private sealed record SvgCluster(string Name, string Color, int X, int Y, int Width, int Height);
    private sealed record SvgLayout(IReadOnlyDictionary<int, (int X, int Y)> Positions,
        IReadOnlyList<SvgCluster> Clusters, int Width, int Height);
}
