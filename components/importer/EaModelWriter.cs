namespace EAJsonModelImporter;

internal static class EaModelWriter
{
    public static EA.Package Write(EA.Repository repository, EA.Package target, ImportModel model)
    {
        string packageName = UniquePackageName(target, model.Name);
        var package = (EA.Package)target.Packages.AddNew(packageName, "");
        package.Notes = model.Description;
        if (model.Version.Length > 0) package.Version = model.Version;
        package.Update();
        target.Packages.Refresh();

        var elements = new Dictionary<string, EA.Element>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in model.Enums)
        {
            var element = (EA.Element)package.Elements.AddNew(definition.Name, "Enumeration");
            element.Notes = definition.Description;
            element.Update();
            foreach (var value in definition.Values)
            {
                var literal = (EA.Attribute)element.Attributes.AddNew(value, "");
                if (definition.ValueDescriptions.TryGetValue(value, out var description)) literal.Notes = description;
                literal.Update();
            }
            element.Attributes.Refresh();
            elements[definition.Name] = element;
        }
        foreach (var definition in model.Classes)
        {
            var element = (EA.Element)package.Elements.AddNew(definition.Name, "Class");
            element.Notes = definition.Description;
            element.Update();
            elements[definition.Name] = element;
        }
        package.Elements.Refresh();

        foreach (var definition in model.Classes)
        {
            var element = elements[definition.Name];
            foreach (var property in definition.Properties)
            {
                if (property.IsReference && elements.TryGetValue(property.Type, out var targetElement))
                {
                    var connector = (EA.Connector)element.Connectors.AddNew("", "Association");
                    connector.SupplierID = targetElement.ElementID;
                    connector.SupplierEnd.Role = property.Name;
                    connector.SupplierEnd.Cardinality = Cardinality(property);
                    connector.Notes = property.Description;
                    connector.Update();
                    continue;
                }

                var attribute = (EA.Attribute)element.Attributes.AddNew(property.Name, property.Type);
                attribute.Notes = property.Description;
                attribute.IsID = property.Identifier;
                attribute.LowerBound = property.Required ? "1" : "0";
                attribute.UpperBound = property.Many ? "*" : "1";
                if (elements.TryGetValue(property.Type, out var classifier)) attribute.ClassifierID = classifier.ElementID;
                attribute.Update();
            }
            element.Attributes.Refresh();
            element.Connectors.Refresh();

            foreach (var parentName in definition.Parents.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!elements.TryGetValue(parentName, out var parent)) continue;
                var generalization = (EA.Connector)element.Connectors.AddNew("", "Generalization");
                generalization.SupplierID = parent.ElementID;
                generalization.Update();
            }
            element.Connectors.Refresh();
        }

        CreateDiagrams(repository, package, model, elements);
        repository.RefreshModelView(package.PackageID);
        return package;
    }

    private static void CreateDiagrams(EA.Repository repository, EA.Package package, ImportModel model,
        IReadOnlyDictionary<string, EA.Element> elements)
    {
        if (!model.Classes.Any(x => x.DiagramDomains.Count > 0))
        {
            CreateSmartDiagram(repository, package, model.Name + " Class Model",
                "Generated from JSON, JSON Schema, or YAML using relationship-aware smart layout.",
                model, elements, null, true, _ => 0);
            return;
        }

        var domains = OrderedDomains(model);
        var domainIndexes = domains.Select((domain, index) => (domain, index))
            .ToDictionary(x => x.domain, x => x.index, StringComparer.OrdinalIgnoreCase);
        CreateSmartDiagram(repository, package, model.Name + " - Overview",
            "Model overview generated from ea_domains annotations using relationship-aware smart layout.",
            model, elements, model.Classes.Select(x => x.Name), false, name =>
            {
                var definition = model.Classes.First(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                string domain = PrimaryDomain(definition);
                return DomainColor(model, domain, domainIndexes[domain]);
            });
        for (int domainIndex = 0; domainIndex < domains.Count; domainIndex++)
        {
            string domain = domains[domainIndex];
            var classNames = model.Classes
                .Where(x => EffectiveDomains(x).Contains(domain, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x.DiagramOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList();
            int color = DomainColor(model, domain, domainIndex);
            CreateSmartDiagram(repository, package, model.Name + " - " + DisplayDomain(domain),
                "Generated from ea_domains LinkML annotations using relationship-aware smart layout.",
                model, elements, classNames, false, _ => color);
        }

        if (model.Enums.Count > 0)
        {
            CreateSmartDiagram(repository, package, model.Name + " - Enumerations",
                "Enumerations are separated from domain views to reduce diagram clutter.",
                model, elements, [], true, _ => 0);
        }
    }

    private static void CreateSmartDiagram(EA.Repository repository, EA.Package package, string name, string notes,
        ImportModel model, IReadOnlyDictionary<string, EA.Element> elements, IEnumerable<string>? classNames,
        bool includeEnums, Func<string, int> backgroundColor)
    {
        var diagram = (EA.Diagram)package.Diagrams.AddNew(name, "Logical");
        diagram.Notes = notes;
        diagram.Update();
        var placements = SmartDiagramLayout.Arrange(model, classNames, includeEnums);
        foreach (var (elementName, box) in placements)
        {
            if (!elements.TryGetValue(elementName, out var element)) continue;
            AddDiagramObject(diagram, element, box, backgroundColor(elementName));
        }
        diagram.DiagramObjects.Refresh();
        repository.SaveDiagram(diagram.DiagramID);
    }

    private static void AddDiagramObject(EA.Diagram diagram, EA.Element element, DiagramBox box,
        int backgroundColor)
    {
        var diagramObject = (EA.DiagramObject)diagram.DiagramObjects.AddNew(
            $"l={box.Left};r={box.Right};t={-box.Top};b={-box.Bottom};", "");
        diagramObject.ElementID = element.ElementID;
        if (backgroundColor != 0) diagramObject.BackgroundColor = backgroundColor;
        diagramObject.Update();
    }

    private static List<string> OrderedDomains(ImportModel model)
    {
        return model.Classes.SelectMany(EffectiveDomains).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => model.Classes.Where(x => EffectiveDomains(x).Contains(domain, StringComparer.OrdinalIgnoreCase))
                .Min(x => x.DiagramOrder))
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> EffectiveDomains(ImportClass definition) =>
        definition.DiagramDomains.Count > 0 ? definition.DiagramDomains : ["other"];

    private static string PrimaryDomain(ImportClass definition) => EffectiveDomains(definition).First();

    private static string DisplayDomain(string domain) => string.Join(" ", domain.Replace('-', '_').Split('_',
        StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));

    private static int DomainColor(ImportModel model, string domain, int index)
    {
        string fallback = (index % 4) switch
        {
            0 => "#DDEBF7",
            1 => "#E2EFDA",
            2 => "#FFF2CC",
            _ => "#EAD1DC"
        };
        string configured = model.DiagramDomainColors.TryGetValue(domain, out string? color) ? color : fallback;
        return ParseEaColor(configured) ?? ParseEaColor(fallback)!.Value;
    }

    // EA stores diagram colours as BGR decimal: red is the least-significant byte.
    internal static int? ParseEaColor(string value)
    {
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(x => new string(x, 2)));
        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int rgb)) return null;
        int red = (rgb >> 16) & 0xFF;
        int green = (rgb >> 8) & 0xFF;
        int blue = rgb & 0xFF;
        return red | (green << 8) | (blue << 16);
    }

    private static string Cardinality(ImportProperty property) => property.Many
        ? (property.Required ? "1..*" : "0..*")
        : (property.Required ? "1" : "0..1");

    private static string UniquePackageName(EA.Package target, string desired)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EA.Package child in target.Packages) names.Add(child.Name);
        if (!names.Contains(desired)) return desired;
        int suffix = 2;
        while (names.Contains(desired + " " + suffix)) suffix++;
        return desired + " " + suffix;
    }
}
