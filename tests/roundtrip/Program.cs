using System.Text.Json;
using EAJsonModelImporter;
using EA17LinkMLExporter;

var source = """
name: VersionTest
version: '1.0'
classes:
  NoClassVersion:
    attributes:
      record_id:
        range: string
  ClassVersionOnly:
    annotations: {"ea_package_path":"Asset/Network"}
    version: '1.0'
    attributes:
      record_id:
        range: string
  ExplicitVersionAttribute:
    attributes:
      record_id:
        range: string
      version:
        range: string
""";
var model = new SchemaConverter().Convert(SimpleYaml.Parse(source), "Test");
Assert(model.Version == "1.0", "schema version");
Assert(model.Classes.Single(c => c.Name == "ClassVersionOnly").PackagePath == "Asset/Network", "initial package path");
Assert(model.Classes.Single(c => c.Name == "ClassVersionOnly").Version == "1.0", "class version retained");
Assert(model.Classes.Single(c => c.Name == "NoClassVersion").Version == "", "absent version remains absent");
// Include actual newlines, tabs, quotes, apostrophes, Unicode, JSON, a Windows path,
// literal backslash-n and two backslashes. None may grow or disappear.
string notes = "First\nSecond\t\"quoted\" O'Brien café {\"source\":\"test\"} C:\\models\\file literal \\n double \\\\";
foreach (var cls in model.Classes) cls.Description = notes;
for (int round = 0; round < 5; round++)
{
    foreach (var cls in model.Classes)
    {
        int expected = cls.Name == "ExplicitVersionAttribute" ? 2 : 1;
        Assert(cls.Properties.Count == expected, $"round {round}: no extra properties on {cls.Name}");
        Assert(cls.Properties.Count(p => p.Name == "version") == expected - 1, "explicit version only");
        Assert(cls.Description == notes, "description preserved exactly");
    }
    var snapshot = new ModelSnapshot { Name = model.Name, Version = model.Version, Notes = notes, OntologyIri = "https://example.org/test" };
    short id = 0;
    foreach (var cls in model.Classes)
    {
        // EA supplies 0.1 for a new element when the source omits its version.
        var item = new UmlClass { Id = ++id, Name = cls.Name, QualifiedName = cls.Name,
            Notes = cls.Description, Abstract = false, PackagePath = cls.PackagePath, Version = cls.Version.Length > 0 ? cls.Version : "0.1" };
        foreach (var p in cls.Properties)
            item.Properties.Add(new UmlProperty { Name = p.Name, Type = p.Type, Notes = notes, Lower = "0", Upper = "1" });
        snapshot.Classes.Add(item);
    }
    model = new SchemaConverter().Convert(SimpleYaml.Parse(LinkMlWriter.Write(snapshot)), "Test");
    Assert(model.Description == notes, "schema description preserved");
    Assert(model.Classes.Single(c => c.Name == "ClassVersionOnly").PackagePath == "Asset/Network", "package path survives round trip");
    Assert(model.Classes.Single(c => c.Name == "ClassVersionOnly").Version == "1.0", "class version stable");
    Assert(model.Classes.All(c => c.Properties.All(p => p.Description == notes)), "attribute descriptions preserved");
}
var inline = SimpleYaml.Parse("annotations: {\"source\":\"O'Brien\"}");
Assert(inline["annotations"]?["source"]?.ToString() == "O'Brien", "flow mapping apostrophe");
Assert(SimpleYaml.Parse("description: 'It''s literal \\n'")["description"]?.ToString() == "It's literal \\n", "single-quoted scalar");
Assert(SimpleYaml.Parse("description: \"C:\\\\\" # comment")["description"]?.ToString() == "C:\\", "even backslashes before closing quote");
Console.WriteLine("All version and escaping round-trip tests passed.");
static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
