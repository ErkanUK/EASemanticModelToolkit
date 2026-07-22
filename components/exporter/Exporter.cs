using System.Text;

namespace EA17LinkMLExporter;

internal static class Exporter
{
    public static string Export(EA.Repository repository, EA.Package package, string parentDirectory,
        ExportOptions? options = null)
    {
        options ??= ExportOptions.All;
        var model = EaModelReader.Read(repository, package);
        var stem = SafeFileName(package.Name);
        var directory = Path.Combine(parentDirectory, stem);
        Directory.CreateDirectory(directory);
        var yamlName = stem + ".linkml.yaml";
        var jsonSchemaName = stem + ".schema.json";
        var mdName = stem + ".md";
        var drawIoName = stem + ".drawio";
        var owlName = stem + ".owl";
        var turtleName = stem + ".ttl";
        if (options.LinkMl)
            File.WriteAllText(Path.Combine(directory, yamlName), LinkMlWriter.Write(model), new UTF8Encoding(false));
        if (options.JsonSchema)
            File.WriteAllText(Path.Combine(directory, jsonSchemaName), JsonSchemaWriter.Write(model), new UTF8Encoding(false));
        if (options.DrawIo)
            File.WriteAllText(Path.Combine(directory, drawIoName), DiagramWriter.WriteDrawIo(model), new UTF8Encoding(false));
        IReadOnlyList<ExportedDiagram> eaDiagrams = [];
        string? fallbackSvgName = null;
        if (options.Svg)
        {
            eaDiagrams = EaDiagramSvgExporter.Export(repository, package, directory);
            if (eaDiagrams.Count == 0)
            {
                fallbackSvgName = stem + ".svg";
                File.WriteAllText(Path.Combine(directory, fallbackSvgName), DiagramWriter.WriteSvg(model), new UTF8Encoding(false));
            }
        }
        if (options.Owl)
            File.WriteAllText(Path.Combine(directory, owlName), OwlWriter.WriteRdfXml(model), new UTF8Encoding(false));
        if (options.Turtle)
            File.WriteAllText(Path.Combine(directory, turtleName), OwlWriter.WriteTurtle(model), new UTF8Encoding(false));
        if (options.Markdown)
            File.WriteAllText(Path.Combine(directory, mdName), MarkdownWriter.Write(model,
                options.DrawIo ? drawIoName : null, fallbackSvgName, options.LinkMl ? yamlName : null,
                options.JsonSchema ? jsonSchemaName : null, options.Owl ? owlName : null,
                options.Turtle ? turtleName : null, eaDiagrams), new UTF8Encoding(false));
        return directory;
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "uml-model" : name.Trim();
    }
}
