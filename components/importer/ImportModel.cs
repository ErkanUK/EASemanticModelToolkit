namespace EAJsonModelImporter;

internal sealed class ImportModel
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string Version { get; init; } = "";
    public string OntologyIri { get; init; } = "";
    public List<ImportClass> Classes { get; } = [];
    public List<ImportEnum> Enums { get; } = [];
    public Dictionary<string, string> DiagramDomainColors { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ImportClass
{
    public required string Name { get; init; }
    public string Description { get; set; } = "";
    public List<string> Parents { get; } = [];
    public List<ImportProperty> Properties { get; } = [];
    public List<string> DiagramDomains { get; } = [];
    public int DiagramOrder { get; set; } = int.MaxValue;
}

internal sealed class ImportEnum
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public List<string> Values { get; } = [];
    public Dictionary<string, string> ValueDescriptions { get; } = new(StringComparer.Ordinal);
}

internal sealed class ImportProperty
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string Description { get; init; } = "";
    public bool Required { get; init; }
    public bool Many { get; init; }
    public bool IsReference { get; init; }
    public bool Identifier { get; set; }
}
