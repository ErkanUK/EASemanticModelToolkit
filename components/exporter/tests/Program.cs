using System.Xml.Linq;
using EA17LinkMLExporter;

Assert(EaDiagramSvgExporter.SafeFileName("Network: Overview") == "Network_ Overview", "safe diagram filename");
Assert(EaDiagramSvgExporter.SafeFileName("...") == "diagram", "empty diagram filename fallback");

var model = new ModelSnapshot
{
    Name = "People Model",
    Version = "3.1",
    Notes = "People & organisations",
    OntologyIri = "https://example.org/people"
};
var person = new UmlClass
{
    Id = 1, Name = "Person", QualifiedName = "Model::Person", Notes = "A person", Abstract = false
};
person.Properties.Add(new UmlProperty
{
    Name = "identifier", Type = "String", Notes = "Stable identifier", Lower = "1", Upper = "1",
    Identifier = true
});
person.Properties.Add(new UmlProperty
{
    Name = "status", Type = "Status", Notes = "Current status", Lower = "0", Upper = "1"
});
var employee = new UmlClass
{
    Id = 2, Name = "Employee", QualifiedName = "Model::Employee", Notes = "", Abstract = false
};
employee.Parents.Add("Person");
model.Classes.AddRange([person, employee]);
var status = new UmlEnum { Id = 3, Name = "Status", Notes = "Employment status" };
status.Values.AddRange(["Active", "Inactive"]);
model.Enums.Add(status);
model.Relations.Add(new UmlRelation
{
    Kind = "Association", SourceId = 2, TargetId = 1, SourceName = "Employee", TargetName = "Person",
    SourceRole = "reports", TargetRole = "manager", SourceMultiplicity = "0..*", TargetMultiplicity = "0..1",
    Notes = "Line management", Composition = false
});

string turtle = OwlWriter.WriteTurtle(model);
Assert(turtle.Contains("a owl:Ontology"), "Turtle ontology declaration");
Assert(turtle.Contains("a owl:Class"), "Turtle classes");
Assert(turtle.Contains("a owl:ObjectProperty"), "Turtle object properties");
Assert(turtle.Contains("a owl:DatatypeProperty"), "Turtle datatype properties");
Assert(turtle.Contains("owl:cardinality \"1\"^^xsd:nonNegativeInteger"), "Turtle cardinality");
Assert(turtle.Contains("owl:oneOf"), "Turtle enumeration");
Assert(turtle.Contains("https://example.org/people#isIdentifier"), "identifier annotation");

string rdfXml = OwlWriter.WriteRdfXml(model);
var xml = XDocument.Parse(rdfXml);
Assert(xml.Root?.Name.LocalName == "RDF", "RDF/XML is well formed");
Assert(rdfXml.Contains("owl:ObjectProperty"), "RDF/XML object properties");
Assert(rdfXml.Contains("owl:NamedIndividual"), "RDF/XML enum individuals");
Assert(rdfXml.Contains("owl:versionInfo"), "RDF/XML version");

string jsonSchema = JsonSchemaWriter.Write(model);
var schema = System.Text.Json.Nodes.JsonNode.Parse(jsonSchema)!.AsObject();
Assert(schema["$schema"]?.ToString() == "https://json-schema.org/draft/2020-12/schema", "JSON Schema draft");
Assert(schema["$defs"]?["Person"]?["properties"]?["identifier"]?["x-ea-identifier"]?.GetValue<bool>() == true,
    "JSON Schema identifier");
Assert(schema["$defs"]?["Employee"]?["properties"]?["manager"]?["$ref"]?.ToString() == "#/$defs/Person",
    "JSON Schema association");
Assert(schema["$defs"]?["Status"]?["enum"]?.AsArray().Count == 2, "JSON Schema enumeration");

var diagrams = new[]
{
    new ExportedDiagram("Network / Asset Health", "diagrams/001-Asset Health.svg"),
    new ExportedDiagram("Network / Load [Planning]", "diagrams/002-Load Planning.svg")
};
string nativeMarkdown = MarkdownWriter.Write(model, "model.drawio", null, "model.yaml", "model.schema.json",
    "model.owl", "model.ttl", diagrams);
Assert(nativeMarkdown.Contains("[JSON Schema](model.schema.json)"), "Markdown JSON Schema link");
Assert(nativeMarkdown.Contains("[OWL/RDF-XML ontology](model.owl)"), "Markdown OWL link");
Assert(nativeMarkdown.Contains("[OWL Turtle ontology](model.ttl)"), "Markdown Turtle link");
Assert(nativeMarkdown.Contains("## EA diagrams"), "EA diagram section");
Assert(nativeMarkdown.Contains("diagrams/001-Asset%20Health.svg"), "SVG path encoding");
Assert(nativeMarkdown.Contains("![Network / Load (Planning)]"), "Markdown alt text escaping");

string fallbackMarkdown = MarkdownWriter.Write(model, "model.drawio", "model.svg", "model.yaml",
    "model.schema.json", "model.owl", "model.ttl", []);
Assert(fallbackMarkdown.Contains("![Generated UML class diagram](model.svg)"), "generated SVG fallback");
Assert(ExportOptions.All.LinkMl && ExportOptions.All.JsonSchema && ExportOptions.All.Markdown &&
       ExportOptions.All.DrawIo && ExportOptions.All.Svg && ExportOptions.All.Owl && ExportOptions.All.Turtle,
    "all export formats checked by default");

Console.WriteLine("All exporter tests passed.");

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("Failed: " + name);
}
