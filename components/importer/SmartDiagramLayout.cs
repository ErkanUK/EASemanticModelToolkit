namespace EAJsonModelImporter;

internal readonly record struct DiagramBox(int Left, int Top, int Right, int Bottom);

internal static class SmartDiagramLayout
{
    private const int Margin = 40;
    private const int LayerGap = 110;
    private const int NodeGap = 55;
    private const int ComponentGap = 160;
    private const int EnumGap = 180;
    private const int ShelfWidth = 2400;

    public static IReadOnlyDictionary<string, DiagramBox> Arrange(ImportModel model,
        IEnumerable<string>? includedClasses = null, bool includeEnums = true)
    {
        var boxes = new Dictionary<string, DiagramBox>(StringComparer.OrdinalIgnoreCase);
        var availableClasses = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classNames = includedClasses is null
            ? availableClasses
            : includedClasses.Where(availableClasses.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var neighbours = classNames.ToDictionary(x => x, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var outgoing = classNames.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var cls in model.Classes)
        {
            foreach (var property in cls.Properties.Where(x => x.IsReference && classNames.Contains(x.Type)))
            {
                Connect(neighbours, cls.Name, property.Type);
                outgoing[cls.Name]++;
            }
            foreach (var parent in cls.Parents.Where(classNames.Contains))
            {
                Connect(neighbours, cls.Name, parent);
                outgoing[parent]++;
            }
        }

        var sizes = model.Classes.ToDictionary(x => x.Name,
            x => Size(x.Name, x.Properties.Where(p => !p.IsReference).Select(p => p.Name + ": " + p.Type),
                x.Properties.Count(p => !p.IsReference)),
            StringComparer.OrdinalIgnoreCase);
        var components = Components(classNames, neighbours)
            .Select(nodes => LayoutComponent(nodes, neighbours, outgoing, sizes))
            .OrderByDescending(x => x.Nodes.Count).ThenBy(x => x.Nodes[0], StringComparer.OrdinalIgnoreCase).ToList();

        int shelfX = Margin, shelfY = Margin, shelfHeight = 0, classRight = Margin, classBottom = Margin;
        foreach (var component in components)
        {
            if (shelfX > Margin && shelfX + component.Width > ShelfWidth)
            {
                shelfX = Margin;
                shelfY += shelfHeight + ComponentGap;
                shelfHeight = 0;
            }
            foreach (var (name, box) in component.Boxes)
                boxes[name] = new DiagramBox(box.Left + shelfX, box.Top + shelfY, box.Right + shelfX, box.Bottom + shelfY);
            shelfX += component.Width + ComponentGap;
            shelfHeight = Math.Max(shelfHeight, component.Height);
            classRight = Math.Max(classRight, shelfX - ComponentGap);
            classBottom = Math.Max(classBottom, shelfY + component.Height);
        }

        int enumX = classNames.Count == 0 ? Margin : classRight + EnumGap, enumY = Margin, enumColumnWidth = 0;
        int enumColumnBottom = Math.Max(1200, classBottom);
        foreach (var definition in model.Enums.Where(_ => includeEnums).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var size = Size(definition.Name, definition.Values, definition.Values.Count);
            if (enumY > Margin && enumY + size.Height > enumColumnBottom)
            {
                enumX += enumColumnWidth + LayerGap;
                enumY = Margin;
                enumColumnWidth = 0;
            }
            boxes[definition.Name] = new DiagramBox(enumX, enumY, enumX + size.Width, enumY + size.Height);
            enumY += size.Height + NodeGap;
            enumColumnWidth = Math.Max(enumColumnWidth, size.Width);
        }
        return boxes;
    }

    private static ComponentLayout LayoutComponent(IReadOnlyList<string> nodes,
        IReadOnlyDictionary<string, HashSet<string>> neighbours, IReadOnlyDictionary<string, int> outgoing,
        IReadOnlyDictionary<string, NodeSize> sizes)
    {
        string root = nodes.OrderByDescending(x => neighbours[x].Count)
            .ThenByDescending(x => outgoing[x]).ThenBy(x => x, StringComparer.OrdinalIgnoreCase).First();
        var layers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [root] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var next in neighbours[current].OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (layers.ContainsKey(next)) continue;
                layers[next] = layers[current] + 1;
                queue.Enqueue(next);
            }
        }

        var orderedLayers = new List<List<string>>();
        var priorOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in nodes.GroupBy(x => layers[x]).OrderBy(x => x.Key))
        {
            var ordered = group.Select(name => new
                {
                    Name = name,
                    Barycentre = neighbours[name].Where(priorOrder.ContainsKey).Select(x => priorOrder[x]).DefaultIfEmpty(int.MaxValue).Average()
                })
                .OrderBy(x => x.Barycentre).ThenByDescending(x => neighbours[x.Name].Count)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Name).ToList();
            orderedLayers.Add(ordered);
            for (int i = 0; i < ordered.Count; i++) priorOrder[ordered[i]] = i;
        }

        var layerWidths = orderedLayers.Select(layer => layer.Max(x => sizes[x].Width)).ToList();
        var layerHeights = orderedLayers.Select(layer => layer.Sum(x => sizes[x].Height) + NodeGap * Math.Max(0, layer.Count - 1)).ToList();
        int height = layerHeights.DefaultIfEmpty(0).Max();
        int x = 0;
        var result = new Dictionary<string, DiagramBox>(StringComparer.OrdinalIgnoreCase);
        for (int layerIndex = 0; layerIndex < orderedLayers.Count; layerIndex++)
        {
            int y = (height - layerHeights[layerIndex]) / 2;
            foreach (var name in orderedLayers[layerIndex])
            {
                var size = sizes[name];
                int left = x + (layerWidths[layerIndex] - size.Width) / 2;
                result[name] = new DiagramBox(left, y, left + size.Width, y + size.Height);
                y += size.Height + NodeGap;
            }
            x += layerWidths[layerIndex] + LayerGap;
        }
        int width = orderedLayers.Count == 0 ? 0 : x - LayerGap;
        return new ComponentLayout(nodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), result, width, height);
    }

    private static List<IReadOnlyList<string>> Components(IReadOnlyCollection<string> names,
        IReadOnlyDictionary<string, HashSet<string>> neighbours)
    {
        var remaining = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<IReadOnlyList<string>>();
        while (remaining.Count > 0)
        {
            string start = remaining.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();
            var component = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start);
            remaining.Remove(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                component.Add(current);
                foreach (var next in neighbours[current].OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    if (remaining.Remove(next)) queue.Enqueue(next);
            }
            result.Add(component);
        }
        return result;
    }

    private static void Connect(IDictionary<string, HashSet<string>> neighbours, string first, string second)
    {
        if (first.Equals(second, StringComparison.OrdinalIgnoreCase)) return;
        neighbours[first].Add(second);
        neighbours[second].Add(first);
    }

    private static NodeSize Size(string name, IEnumerable<string> rows, int rowCount)
    {
        int longest = rows.Prepend(name).DefaultIfEmpty(name).Max(x => x.Length);
        int width = Math.Clamp(190 + longest * 4, 250, 430);
        int height = Math.Clamp(85 + rowCount * 19, 125, 500);
        return new NodeSize(width, height);
    }

    private readonly record struct NodeSize(int Width, int Height);
    private sealed record ComponentLayout(IReadOnlyList<string> Nodes,
        IReadOnlyDictionary<string, DiagramBox> Boxes, int Width, int Height);
}
