using System.Net;
using System.Text.RegularExpressions;

namespace EA17LinkMLExporter;

internal static class EaModelReader
{
    public static ModelSnapshot Read(EA.Repository repository, EA.Package root)
    {
        var appearances = ReadAppearances(root);
        var model = new ModelSnapshot
        {
            Name = root.Name,
            Version = root.Version ?? "",
            Notes = CleanNotes(root.Notes),
            OntologyIri = "urn:ea:model:" + (string.IsNullOrWhiteSpace(root.PackageGUID)
                ? Uri.EscapeDataString(root.Name) : root.PackageGUID.Trim('{', '}'))
        };
        ReadPackage(repository, root, root.Name, model, appearances.Elements);
        ReadRelations(repository, model, appearances.Connectors);
        return model;
    }

    private static void ReadPackage(EA.Repository repository, EA.Package package, string path, ModelSnapshot model,
        IReadOnlyDictionary<int, ElementAppearance> appearances)
    {
        foreach (EA.Element element in package.Elements)
        {
            appearances.TryGetValue(Convert.ToInt32(element.ElementID), out var appearance);
            if (IsEnumeration(element))
            {
                var item = new UmlEnum
                {
                    Id = (short)element.ElementID, Name = element.Name, Notes = CleanNotes(element.Notes),
                    FillColor = appearance?.Fill ?? "#FFF7D6",
                    BorderColor = appearance?.Border ?? "#D6B656",
                    FontColor = appearance?.Font ?? "#0F172A"
                };
                foreach (EA.Attribute attribute in element.Attributes)
                    item.Values.Add(attribute.Name);
                model.Enums.Add(item);
                continue;
            }

            if (!IsClass(element)) continue;
            var cls = new UmlClass
            {
                Id = (short)element.ElementID,
                Name = element.Name,
                QualifiedName = path + "::" + element.Name,
                Notes = CleanNotes(element.Notes),
                Version = element.Version,
                Abstract = element.Abstract == "1",
                FillColor = appearance?.Fill ?? "#FFFFFF",
                BorderColor = appearance?.Border ?? "#334155",
                FontColor = appearance?.Font ?? "#0F172A"
            };
            foreach (EA.Attribute attribute in element.Attributes)
            {
                var type = attribute.Type;
                if (attribute.ClassifierID > 0)
                {
                    try { type = repository.GetElementByID(attribute.ClassifierID).Name; } catch { /* retain EA type */ }
                }
                cls.Properties.Add(new UmlProperty
                {
                    Name = attribute.Name,
                    Type = type,
                    Notes = CleanNotes(attribute.Notes),
                    Lower = string.IsNullOrWhiteSpace(attribute.LowerBound) ? "0" : attribute.LowerBound,
                    Upper = string.IsNullOrWhiteSpace(attribute.UpperBound) ? "1" : attribute.UpperBound,
                    Derived = attribute.IsDerived,
                    ReadOnly = attribute.IsConst,
                    Identifier = attribute.IsID
                });
            }
            model.Classes.Add(cls);
        }

        foreach (EA.Package child in package.Packages)
            ReadPackage(repository, child, path + "::" + child.Name, model, appearances);
    }

    private static void ReadRelations(EA.Repository repository, ModelSnapshot model,
        IReadOnlyDictionary<int, string> connectorColors)
    {
        var ids = model.Classes.Select(x => (int)x.Id).Concat(model.Enums.Select(x => (int)x.Id)).ToHashSet();
        var seen = new HashSet<int>();
        foreach (var source in model.Classes)
        {
            EA.Element element = repository.GetElementByID((int)source.Id);
            foreach (EA.Connector connector in element.Connectors)
            {
                if (!seen.Add(connector.ConnectorID) || !ids.Contains(connector.ClientID) || !ids.Contains(connector.SupplierID))
                    continue;
                var client = repository.GetElementByID(connector.ClientID);
                var supplier = repository.GetElementByID(connector.SupplierID);
                if (connector.Type.Equals("Generalization", StringComparison.OrdinalIgnoreCase))
                {
                    var child = model.Classes.FirstOrDefault(x => x.Id == (short)connector.ClientID);
                    if (child is not null) child.Parents.Add(supplier.Name);
                    continue;
                }

                if (!connector.Type.Equals("Association", StringComparison.OrdinalIgnoreCase) &&
                    !connector.Type.Equals("Aggregation", StringComparison.OrdinalIgnoreCase)) continue;

                model.Relations.Add(new UmlRelation
                {
                    Kind = connector.Type,
                    SourceId = (short)connector.ClientID,
                    TargetId = (short)connector.SupplierID,
                    SourceName = client.Name,
                    TargetName = supplier.Name,
                    SourceRole = connector.ClientEnd.Role,
                    TargetRole = connector.SupplierEnd.Role,
                    SourceMultiplicity = DefaultMultiplicity(connector.ClientEnd.Cardinality),
                    TargetMultiplicity = DefaultMultiplicity(connector.SupplierEnd.Cardinality),
                    Notes = CleanNotes(connector.Notes),
                    Composition = connector.ClientEnd.Aggregation == 2 || connector.SupplierEnd.Aggregation == 2,
                    LineColor = connectorColors.TryGetValue(Convert.ToInt32(connector.ConnectorID), out var lineColor)
                        ? lineColor : "#475569"
                });
            }
        }
    }

    private static bool IsClass(EA.Element element) =>
        element.Type.Equals("Class", StringComparison.OrdinalIgnoreCase) && !IsEnumeration(element);

    private static bool IsEnumeration(EA.Element element) =>
        element.Type.Equals("Enumeration", StringComparison.OrdinalIgnoreCase) ||
        element.Stereotype.Equals("enumeration", StringComparison.OrdinalIgnoreCase);

    private static string DefaultMultiplicity(string value) => string.IsNullOrWhiteSpace(value) ? "0..1" : value;

    private static AppearanceMaps ReadAppearances(EA.Package root)
    {
        var maps = new AppearanceMaps();
        ReadPackageAppearances(root, maps);
        return maps;
    }

    private static void ReadPackageAppearances(EA.Package package, AppearanceMaps maps)
    {
        foreach (EA.Diagram diagram in package.Diagrams)
        {
            foreach (EA.DiagramObject diagramObject in diagram.DiagramObjects)
            {
                int id = Convert.ToInt32(diagramObject.ElementID);
                if (!maps.Elements.TryGetValue(id, out var colors))
                    maps.Elements[id] = colors = new ElementAppearance();
                colors.Fill ??= ToCssColor(diagramObject.BackgroundColor);
                colors.Border ??= ToCssColor(diagramObject.BorderColor);
                colors.Font ??= ToCssColor(diagramObject.FontColor);
            }
            foreach (EA.DiagramLink link in diagram.DiagramLinks)
            {
                var color = ToCssColor(link.LineColor);
                if (color is not null)
                    maps.Connectors.TryAdd(Convert.ToInt32(link.ConnectorID), color);
            }
        }
        foreach (EA.Package child in package.Packages) ReadPackageAppearances(child, maps);
    }

    // EA stores colours as BGR integers: red is the least-significant byte.
    internal static string? ToCssColor(int eaColor)
    {
        if (eaColor < 0) return null;
        int red = eaColor & 0xFF;
        int green = (eaColor >> 8) & 0xFF;
        int blue = (eaColor >> 16) & 0xFF;
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private sealed class AppearanceMaps
    {
        public Dictionary<int, ElementAppearance> Elements { get; } = [];
        public Dictionary<int, string> Connectors { get; } = [];
    }

    private sealed class ElementAppearance
    {
        public string? Fill { get; set; }
        public string? Border { get; set; }
        public string? Font { get; set; }
    }

    internal static string CleanNotes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decoded = WebUtility.HtmlDecode(value.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase));
        return Regex.Replace(decoded, "<[^>]+>", "").Replace("\r", "").Trim();
    }
}
