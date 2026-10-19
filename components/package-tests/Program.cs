using System.Text.Json.Nodes;
using EAJsonModelImporter;
using EA17LinkMLExporter;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (InvalidDataException) { assertions++; return; }
    throw new Exception(message);
}
var model = new ModelSnapshot { Name = "Pathways", Version = "0.26", Notes = "", OntologyIri = "https://example.org/pathways" };
model.Classes.Add(new UmlClass { Id = 1, Name = "CRIProperty", QualifiedName = "Pathways::CRI::CRIProperty", PackagePath = "LCT CRI Readiness", Notes = "A property" });
model.Classes.Add(new UmlClass { Id = 2, Name = "StreetAddress", QualifiedName = "Pathways::CRI::Address::StreetAddress", PackagePath = "LCT CRI Readiness/Address", Notes = "An address" });
model.Classes.Add(new UmlClass { Id = 3, Name = "UsagePoint", QualifiedName = "Pathways::Network::UsagePoint", PackagePath = "Network", Notes = "A supply point" });
model.Classes.Add(new UmlClass { Id = 4, Name = "Shared", QualifiedName = "Pathways::Shared", PackagePath = "", Notes = "Root class" });
model.Enums.Add(new UmlEnum { Id = 5, Name = "LicenceArea", PackagePath = "Shared/Enumerations", Notes = "Licence" });
model.Enums.Single().Values.Add("SEPD");
model.LinkMlAnnotations["class:CRIProperty"] = """{"domain":"LCT CRI Readiness","ea_package_path":"stale"}""";
model.Relations.Add(new UmlRelation { Kind = "Association", SourceId = 1, TargetId = 3,
    SourceName = "CRIProperty", TargetName = "UsagePoint", SourceRole = "", TargetRole = "usage_point",
    SourceMultiplicity = "0..*", TargetMultiplicity = "0..1", Notes = "Cross-domain", TargetNavigable = true });
var yaml = LinkMlWriter.Write(model);
var imported = new SchemaConverter().Convert(SimpleYaml.Parse(yaml), "fallback");
Check(imported.Classes.Single(c => c.Name == "CRIProperty").PackagePath == "LCT CRI Readiness", "Actual package replaces stale metadata");
Check(imported.Classes.Single(c => c.Name == "StreetAddress").PackagePath == "LCT CRI Readiness/Address", "Nested class placement round-trips");
Check(imported.Classes.Single(c => c.Name == "Shared").PackagePath == "", "Explicit root survives round-trip");
Check(imported.Enums.Single().PackagePath == "Shared/Enumerations", "Enum placement round-trips");
Check(imported.Classes.Single(c => c.Name == "CRIProperty").DiagramDomains.SequenceEqual(["LCT CRI Readiness"]), "Domain annotation preserved separately");
Check(imported.Classes.Single(c => c.Name == "CRIProperty").Properties.Any(p => p.Type == "UsagePoint" && p.IsReference), "Cross-package reference survives");
var old = new SchemaConverter().Convert(SimpleYaml.Parse("""
classes:
  OldClass:
    annotations: {"domain":"Network"}
  NewClass:
    annotations:
      ea_package_path:
        tag: ea_package_path
        value: Network/New
"""), "older");
Check(old.Classes.Single(c => c.Name == "OldClass").PackagePath is null, "Absent package path means preserve placement, not move to root");
Check(old.Classes.Single(c => c.Name == "NewClass").PackagePath == "Network/New", "Structured LinkML annotation supported");
foreach (string invalid in new[] { "/Network", "Network/", "Network//LV", "../Network", "Network/..", "./Network", "Network\\LV", " Network", "Network/ LV" })
    Reject(() => PackagePlacement.Normalize(invalid), "Invalid package path accepted: " + invalid);
Reject(() => new SchemaConverter().Convert(SimpleYaml.Parse("""
classes:
  Bad:
    annotations: {"ea_package_path":["Network"]}
"""), "bad"), "Non-string path accepted");

var root = new TestPackage("Pathways");
root.Children.Add(new TestPackage("Network"));
var network = root.Children.Single();
network.Elements.Add(new TestElement("UsagePoint"));
var originalElement = network.Elements.Single();
Dictionary<string, TestElement> Index() => PackagePlacement.Index(root, p => p.Children, p => p.Elements, p => p.Name, e => e.Name);
TestPackage Resolve(string path) => PackagePlacement.Resolve(root, path, p => p.Children, p => p.Name, (parent, name) =>
{
    var child = new TestPackage(name); parent.Children.Add(child); return child;
});
Check(ReferenceEquals(Index()["usagepoint"], originalElement), "Recursive index finds existing class identity in child package");
Check(ReferenceEquals(Resolve("network"), network), "Case-insensitive path reuses package");
Check(ReferenceEquals(Resolve(""), root), "Empty path resolves model root");
var nested = Resolve("LCT CRI Readiness/Address");
Check(ReferenceEquals(Resolve("LCT CRI Readiness/Address"), nested), "Repeated import does not duplicate packages");
Check(root.Children.Count == 2, "Only missing package hierarchy created");
network.Elements.Add(new TestElement("usagepoint"));
Reject(() => Index(), "Ambiguous class names silently accepted");
network.Elements.RemoveAt(1);
root.Children.Add(new TestPackage("NETWORK"));
Reject(() => Index(), "Ambiguous sibling package names accepted");
Reject(() => Resolve("Network"), "Ambiguous package selected arbitrarily");
var before = root.Children.Count;
Reject(() => Resolve("Escape/../Elsewhere"), "Traversal accepted");
Check(root.Children.Count == before, "Invalid paths fail before package creation");
model.Enums.Add(new UmlEnum { Id = 6, Name = "UsagePoint", Notes = "Conflicting key" });
Reject(() => LinkMlWriter.Write(model), "Duplicate class/enum LinkML keys accepted");
if (args.Length > 0)
{
    var pathways = new SchemaConverter().Convert(SimpleYaml.Parse(File.ReadAllText(args[0])), "Pathways");
    Check(pathways.Classes.Count == 39, "Pathways address model class count changed");
    Check(pathways.Classes.Single(c => c.Name == "CRIProperty").Properties.Any(p => p.Name == "address" && p.Type == "StreetAddress"), "Pathways address relationship changed");
    Check(pathways.Classes.Single(c => c.Name == "NetworkIdentity").Properties.Any(p => p.Type == "UsagePoint" && p.IsReference), "Pathways CRI domain connection changed");
    Check(pathways.Classes.SelectMany(c => c.Properties).Where(p => p.IsReference).All(p => pathways.Classes.Any(c => c.Name == p.Type)), "Unresolved Pathways reference");
}
Console.WriteLine($"Package round-trip tests passed ({assertions} checks). EA COM interactions require live EA verification.");

sealed class TestPackage(string name)
{
    public string Name { get; } = name;
    public List<TestPackage> Children { get; } = [];
    public List<TestElement> Elements { get; } = [];
}
sealed record TestElement(string Name);
