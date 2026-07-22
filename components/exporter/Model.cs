namespace EA17LinkMLExporter;

internal sealed class ModelSnapshot
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Notes { get; init; }
    public required string OntologyIri { get; init; }
    public List<UmlClass> Classes { get; } = [];
    public List<UmlEnum> Enums { get; } = [];
    public List<UmlRelation> Relations { get; } = [];
}

internal sealed class UmlClass
{
    public required short Id { get; init; }
    public required string Name { get; init; }
    public required string QualifiedName { get; init; }
    public required string Notes { get; init; }
    public string? Version { get; init; }
    public bool Abstract { get; init; }
    public string FillColor { get; init; } = "#FFFFFF";
    public string BorderColor { get; init; } = "#334155";
    public string FontColor { get; init; } = "#0F172A";
    public List<UmlProperty> Properties { get; } = [];
    public List<string> Parents { get; } = [];
}

internal sealed class UmlEnum
{
    public required short Id { get; init; }
    public required string Name { get; init; }
    public required string Notes { get; init; }
    public string FillColor { get; init; } = "#FFF7D6";
    public string BorderColor { get; init; } = "#D6B656";
    public string FontColor { get; init; } = "#0F172A";
    public List<string> Values { get; } = [];
}

internal sealed class UmlProperty
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Notes { get; init; }
    public required string Lower { get; init; }
    public required string Upper { get; init; }
    public bool Derived { get; init; }
    public bool ReadOnly { get; init; }
    public bool Identifier { get; init; }
}

internal sealed class UmlRelation
{
    public required string Kind { get; init; }
    public required short SourceId { get; init; }
    public required short TargetId { get; init; }
    public required string SourceName { get; init; }
    public required string TargetName { get; init; }
    public required string SourceRole { get; init; }
    public required string TargetRole { get; init; }
    public required string SourceMultiplicity { get; init; }
    public required string TargetMultiplicity { get; init; }
    public required string Notes { get; init; }
    public bool Composition { get; init; }
    public string LineColor { get; init; } = "#475569";
}
