namespace EA17LinkMLExporter;

internal sealed record ExportedDiagram(string Name, string RelativePath);

internal static class EaDiagramSvgExporter
{
    public static IReadOnlyList<ExportedDiagram> Export(EA.Repository repository, EA.Package rootPackage,
        string outputDirectory)
    {
        var diagrams = new List<(EA.Diagram Diagram, string QualifiedName)>();
        Collect(rootPackage, rootPackage.Name, diagrams);
        if (diagrams.Count == 0) return [];

        string diagramDirectory = Path.Combine(outputDirectory, "diagrams");
        Directory.CreateDirectory(diagramDirectory);
        var project = repository.GetProjectInterface();
        EA.Diagram? originalDiagram = null;
        try { originalDiagram = repository.GetCurrentDiagram(); } catch { }

        var exported = new List<ExportedDiagram>();
        try
        {
            for (int index = 0; index < diagrams.Count; index++)
            {
                var item = diagrams[index];
                string fileName = $"{index + 1:D3}-{SafeFileName(item.Diagram.Name)}.svg";
                string fullPath = Path.Combine(diagramDirectory, fileName);
                repository.OpenDiagram(item.Diagram.DiagramID);
                project.SaveDiagramImageToFile(fullPath);
                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                    throw new IOException($"EA did not create an SVG for diagram '{item.QualifiedName}'.");
                exported.Add(new ExportedDiagram(item.QualifiedName, "diagrams/" + fileName));
            }
        }
        finally
        {
            if (originalDiagram is not null)
            {
                try { repository.OpenDiagram(originalDiagram.DiagramID); } catch { }
            }
        }
        return exported;
    }

    private static void Collect(EA.Package package, string packagePath,
        List<(EA.Diagram Diagram, string QualifiedName)> diagrams)
    {
        foreach (EA.Diagram diagram in package.Diagrams)
            diagrams.Add((diagram, packagePath + " / " + diagram.Name));
        foreach (EA.Package child in package.Packages)
            Collect(child, packagePath + " / " + child.Name, diagrams);
    }

    internal static string SafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(name) ? "diagram" : name;
    }
}
