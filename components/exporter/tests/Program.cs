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
model.LinkMlPrefixes["people"] = "https://example.org/people/";
model.LinkMlPrefixes["linkml"] = "https://w3id.org/linkml/";
model.LinkMlImports.AddRange(["linkml:types", "https://example.org/common"]);
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
person.Properties.Add(new UmlProperty
{
    Name = "untypedValue", Type = "", Notes = "EA datatype is not set", Lower = "0", Upper = "1"
});
person.Properties.Add(new UmlProperty
{
    Name = "interval", Type = "Duration", Notes = "EA custom datatype is not declared", Lower = "0", Upper = "1"
});
var employee = new UmlClass
{
    Id = 2, Name = "Employee", QualifiedName = "Model::Employee", Notes = "", Abstract = false
};
employee.Parents.Add("Person");
employee.Parents.Add("PartyMixin");
model.Classes.AddRange([person, employee]);
model.Classes.Add(new UmlClass
{
    Id = 4, Name = "PartyMixin", QualifiedName = "Model::PartyMixin", Notes = "", Abstract = true
});
var status = new UmlEnum { Id = 3, Name = "Status", Notes = "Employment status" };
status.Values.AddRange(["Active", "Inactive"]);
status.ValueDescriptions["Active"] = "Currently employed";
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

string linkMl = LinkMlWriter.Write(model);
Assert(linkMl.Contains("range: string"), "blank EA datatype has a valid LinkML fallback");
Assert(!linkMl.Contains("range: unnamed"), "blank EA datatype does not create an unresolved LinkML range");
Assert(!linkMl.Contains("range: Duration"), "undeclared EA custom datatype has a valid LinkML fallback");
Assert(linkMl.Replace("\r", "").Contains("  PartyMixin:\n    abstract: true\n    mixin: true"),
    "secondary parent is declared as a LinkML mixin");
Assert(linkMl.Contains("id: \"https://example.org/people\""), "LinkML ontology id is preserved");
Assert(linkMl.Contains("people: \"https://example.org/people/\""), "LinkML prefixes are preserved");
Assert(linkMl.Contains("- \"https://example.org/common\""), "LinkML imports are preserved");
Assert(linkMl.Contains("identifier: true"), "LinkML identifier is exported");
Assert(linkMl.Contains("description: \"Currently employed\""), "LinkML enum value description is exported");

string plantUml = PlantUmlWriter.Write(model);
Assert(plantUml.StartsWith("@startuml"), "PlantUML document start");
Assert(plantUml.TrimEnd().EndsWith("@enduml"), "PlantUML document end");
Assert(plantUml.Contains("class \"Person\""), "PlantUML class");
Assert(plantUml.Contains("enum \"Status\""), "PlantUML enumeration");
Assert(plantUml.Contains("untypedValue: unnamed [0..1]"), "unknown datatype remains a harmless diagram label");
Assert(plantUml.Contains("<|--"), "PlantUML inheritance");
Assert(plantUml.Contains("-->"), "PlantUML association");

var diagrams = new[]
{
    new ExportedDiagram("Network / Asset Health", "diagrams/001-Asset Health.svg"),
    new ExportedDiagram("Network / Load [Planning]", "diagrams/002-Load Planning.svg")
};
string nativeMarkdown = MarkdownWriter.Write(model, "model.drawio", null, "model.yaml", "model.schema.json",
    "model.owl", "model.ttl", "model.puml", diagrams);
Assert(nativeMarkdown.Contains("[JSON Schema](model.schema.json)"), "Markdown JSON Schema link");
Assert(nativeMarkdown.Contains("[OWL/RDF-XML ontology](model.owl)"), "Markdown OWL link");
Assert(nativeMarkdown.Contains("[OWL Turtle ontology](model.ttl)"), "Markdown Turtle link");
Assert(nativeMarkdown.Contains("[PlantUML class diagram](model.puml)"), "Markdown PlantUML link");
Assert(nativeMarkdown.Contains("## EA diagrams"), "EA diagram section");
Assert(nativeMarkdown.Contains("diagrams/001-Asset%20Health.svg"), "SVG path encoding");
Assert(nativeMarkdown.Contains("![Network / Load (Planning)]"), "Markdown alt text escaping");

string fallbackMarkdown = MarkdownWriter.Write(model, "model.drawio", "model.svg", "model.yaml",
    "model.schema.json", "model.owl", "model.ttl", "model.puml", []);
Assert(fallbackMarkdown.Contains("![Generated UML class diagram](model.svg)"), "generated SVG fallback");
Assert(ExportOptions.All.LinkMl && ExportOptions.All.JsonSchema && ExportOptions.All.Markdown &&
       ExportOptions.All.DrawIo && ExportOptions.All.PlantUml && ExportOptions.All.Svg &&
       ExportOptions.All.Owl && ExportOptions.All.Turtle,
    "all export formats checked by default");

Console.WriteLine("All exporter tests passed.");

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("Failed: " + name);
}
