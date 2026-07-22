using System.Text.Json.Nodes;
using EAJsonModelImporter;

if (args.Length == 1)
{
    string path = Path.GetFullPath(args[0]);
    var inspected = new SchemaConverter().Convert(SimpleYaml.Parse(File.ReadAllText(path)), Path.GetFileNameWithoutExtension(path));
    var inspectedLayout = SmartDiagramLayout.Arrange(inspected);
    bool hasOverlap = inspectedLayout.SelectMany((left, index) => inspectedLayout.Skip(index + 1)
        .Select(right => Overlaps(left.Value, right.Value))).Any(x => x);
    if (hasOverlap) throw new InvalidOperationException("Smart layout contains overlapping boxes.");
    string inspectedXmlOntology = OwlSerializer.Serialize(inspected, OwlSerialization.RdfXml);
    var inspectedXml = new System.Xml.XmlDocument();
    inspectedXml.LoadXml(inspectedXmlOntology);
    string inspectedTurtle = OwlSerializer.Serialize(inspected, OwlSerialization.Turtle);
    if (!inspectedTurtle.Contains("a owl:Ontology")) throw new InvalidOperationException("Turtle ontology declaration is missing.");
    Console.WriteLine($"Parsed and laid out {inspected.Name}: {inspected.Classes.Count} classes, {inspected.Enums.Count} enumerations, {inspected.Classes.Sum(x => x.Properties.Count)} properties/relationships, no overlaps.");
    Console.WriteLine($"OWL exports validated: {inspectedXmlOntology.Length} RDF/XML characters, {inspectedTurtle.Length} Turtle characters.");
    var annotated = inspected.Classes.Where(x => x.DiagramDomains.Count > 0).ToList();
    Console.WriteLine($"Layout annotations: {annotated.Count}/{inspected.Classes.Count} classes annotated.");
    foreach (var domain in annotated.SelectMany(x => x.DiagramDomains).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {domain}: {annotated.Count(x => x.DiagramDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))} classes");
    }
    return;
}

var schema = JsonNode.Parse("""
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Library",
  "version": "1.2",
  "$defs": {
    "Person": { "type": "object", "properties": { "name": { "type": "string" } }, "required": ["name"] },
    "Author": { "allOf": [ { "$ref": "#/$defs/Person" } ], "type": "object" }
  },
  "type": "object",
  "properties": {
    "authors": { "type": "array", "items": { "$ref": "#/$defs/Author" } },
    "status": { "type": "string", "enum": ["open", "closed"] }
  },
  "required": ["authors"]
}
""")!;
var model = new SchemaConverter().Convert(schema, "fallback");
Assert(model.Name == "Library" && model.Version == "1.2", "schema metadata");
Assert(model.Classes.Any(x => x.Name == "Person" && x.Properties.Any(p => p.Name == "name" && p.Required)), "required attribute");
Assert(model.Classes.Any(x => x.Name == "Author" && x.Parents.Contains("Person")), "allOf inheritance");
Assert(model.Classes.Single(x => x.Name == "Library").Properties.Any(x => x.Name == "authors" && x.Many && x.IsReference), "array association");
Assert(model.Enums.Any(x => x.Values.SequenceEqual(["open", "closed"])), "enumeration");

var plain = JsonNode.Parse("""{"id":7,"customer":{"name":"Ada"},"lines":[{"sku":"A1","quantity":2}]}""")!;
var inferred = new SchemaConverter().Convert(plain, "order");
Assert(inferred.Classes.Any(x => x.Name == "Order"), "plain JSON root");
Assert(inferred.Classes.Any(x => x.Name == "Customer"), "nested object without owner prefix");
Assert(inferred.Classes.Any(x => x.Name == "Line"), "array item class without owner prefix");

var prefixed = JsonNode.Parse("""{"linkml":{"classes":{"Person":{"name":"Person"}}}}""")!;
var cleanNames = new SchemaConverter().Convert(prefixed, "LDMLoadModel");
Assert(cleanNames.Classes.Any(x => x.Name == "Linkml"), "first nested name");
Assert(cleanNames.Classes.Any(x => x.Name == "Person"), "leaf nested name");
Assert(cleanNames.Classes.All(x => x.Name != "Classes"), "classes container is flattened");
Assert(cleanNames.Classes.Where(x => x.Name != "LDMLoadModel").All(x => !x.Name.StartsWith("LDMLoadModel")), "no repeated root prefix");

var classContainer = JsonNode.Parse("""{"classes":{"TransformerLoadForecast":{"name":"Load forecast"},"TransformerActual":{"name":"Actual"}}}""")!;
var flattened = new SchemaConverter().Convert(classContainer, "Model");
Assert(flattened.Classes.Any(x => x.Name == "TransformerLoadForecast"), "classes container child name");
Assert(flattened.Classes.Any(x => x.Name == "TransformerActual"), "second classes container child name");
Assert(flattened.Classes.All(x => !x.Name.StartsWith("Classes")), "no Classes prefix");

var linkmlAttributes = JsonNode.Parse("""
{
  "classes": {
    "Terminal": {
      "description": "A terminal",
      "attributes": {
        "description": { "range": "string" },
        "conductingEquipment": { "range": "ConductingEquipment", "required": true }
      }
    },
    "ConductingEquipment": {
      "attributes": { "name": { "range": "string", "multivalued": false } }
    }
  }
}
""")!;
var attributeModel = new SchemaConverter().Convert(linkmlAttributes, "Grid");
var terminal = attributeModel.Classes.Single(x => x.Name == "Terminal");
Assert(attributeModel.Classes.All(x => !x.Name.EndsWith("Attributes")), "attributes container is not a class");
Assert(terminal.Properties.Any(x => x.Name == "description" && x.Type == "String" && !x.IsReference), "primitive attribute definition");
Assert(terminal.Properties.Any(x => x.Name == "conductingEquipment" && x.Type == "ConductingEquipment" && x.IsReference && x.Required), "class range association definition");

var namedEnums = JsonNode.Parse("""
{
  "$defs": {
    "Priority": { "type": "string", "enum": ["low", "high"] }
  },
  "type": "object",
  "properties": { "priority": { "$ref": "#/$defs/Priority" } }
}
""")!;
var namedEnumModel = new SchemaConverter().Convert(namedEnums, "Task");
Assert(namedEnumModel.Enums.Any(x => x.Name == "Priority" && x.Values.SequenceEqual(["low", "high"])), "named JSON Schema enum");
Assert(namedEnumModel.Classes.All(x => x.Name != "Priority"), "named JSON Schema enum is not a class");
Assert(namedEnumModel.Classes.Single(x => x.Name == "Task").Properties.Any(x => x.Name == "priority" && x.Type == "Priority" && !x.IsReference), "named JSON Schema enum attribute");

var linkmlEnums = JsonNode.Parse("""
{
  "enums": {
    "OperatingStatus": {
      "description": "Current status",
      "permissible_values": { "ON": { "description": "Active" }, "OFF": { "description": "Inactive" } }
    }
  },
  "classes": {
    "Device": {
      "attributes": { "status": { "range": "OperatingStatus" } }
    }
  }
}
""")!;
var linkmlEnumModel = new SchemaConverter().Convert(linkmlEnums, "Equipment");
Assert(linkmlEnumModel.Enums.Any(x => x.Name == "OperatingStatus" && x.Values.SequenceEqual(["ON", "OFF"])), "LinkML permissible values enum");
Assert(linkmlEnumModel.Enums.Single(x => x.Name == "OperatingStatus").ValueDescriptions["ON"] == "Active", "LinkML enum literal description");
Assert(linkmlEnumModel.Classes.All(x => x.Name != "OperatingStatus"), "LinkML enum is not a class");
Assert(linkmlEnumModel.Classes.Single(x => x.Name == "Device").Properties.Any(x => x.Name == "status" && x.Type == "OperatingStatus" && !x.IsReference), "LinkML enum attribute");

var yamlEnum = SimpleYaml.Parse("""
enums:
  SmartMeterYNEnum:
    description: SIMS smart meter indicator
    permissible_values:
      "Y":
        description: Smart meter installed and active
      "N":
        description: Not a smart meter
classes:
  Meter:
    attributes:
      smart:
        range: SmartMeterYNEnum
""");
var yamlEnumModel = new SchemaConverter().Convert(yamlEnum, "MeterModel");
var smartMeterEnum = yamlEnumModel.Enums.Single(x => x.Name == "SmartMeterYNEnum");
Assert(smartMeterEnum.Values.SequenceEqual(["Y", "N"]), "quoted YAML enum values");
Assert(smartMeterEnum.ValueDescriptions["Y"] == "Smart meter installed and active", "YAML enum literal notes");
Assert(yamlEnumModel.Classes.Single(x => x.Name == "Meter").Properties.Any(x => x.Name == "smart" && !x.IsReference), "YAML enum typed attribute");

var linkmlRelationships = SimpleYaml.Parse("""
id: https://example.org/network
name: network_model
title: Network Model
prefixes:
  ex: https://example.org/
imports:
  - linkml:types
classes:
  SecondarySubstation:
    attributes:
      location_tk:
        range: string
    relationships:
      transformers:
        description: Transformers installed at this substation
        range: Transformer
        multivalued: true
        join_key: location_tk
    annotations:
      source_table: substations
  Transformer:
    attributes:
      asset_tk:
        range: string
""");
var relationshipModel = new SchemaConverter().Convert(linkmlRelationships, "fallback");
var substation = relationshipModel.Classes.Single(x => x.Name == "SecondarySubstation");
Assert(relationshipModel.Classes.Select(x => x.Name).Order().SequenceEqual(["SecondarySubstation", "Transformer"]), "LinkML metadata does not create classes");
Assert(substation.Properties.Any(x => x.Name == "transformers" && x.Type == "Transformer" && x.IsReference && x.Many), "LinkML relationship becomes association");
Assert(relationshipModel.Classes.Count(x => x.Name.Contains("Transformer", StringComparison.OrdinalIgnoreCase)) == 1, "no plural relationship class duplicate");

var diagramAnnotations = SimpleYaml.Parse("""
annotations:
  ea_domain_colors:
    network_spine: "#DDEBF7"
    asset_health: "#FFF2CC"
classes:
  Transformer:
    annotations:
      ea_domains: "network_spine,load_planning,asset_health"
      ea_order: 20
    attributes:
      asset_tk:
        range: string
  Asset:
    annotations:
      ea_domains: [asset_health]
      ea_order: 10
""");
var diagramModel = new SchemaConverter().Convert(diagramAnnotations, "diagram-layout");
var diagramTransformer = diagramModel.Classes.Single(x => x.Name == "Transformer");
var diagramAsset = diagramModel.Classes.Single(x => x.Name == "Asset");
Assert(diagramTransformer.DiagramDomains.SequenceEqual(["network_spine", "load_planning", "asset_health"]), "comma-separated EA diagram domains");
Assert(diagramTransformer.DiagramOrder == 20, "EA diagram order");
Assert(diagramAsset.DiagramDomains.SequenceEqual(["asset_health"]), "array EA diagram domains");
Assert(diagramAsset.DiagramOrder == 10, "second EA diagram order");
Assert(diagramModel.DiagramDomainColors["network_spine"] == "#DDEBF7", "EA domain colour annotation");
Assert(diagramModel.DiagramDomainColors["asset_health"] == "#FFF2CC", "second EA domain colour annotation");
Assert(EaModelWriter.ParseEaColor("#FF0000") == 255, "EA red colour encoding");
Assert(EaModelWriter.ParseEaColor("#00FF00") == 65280, "EA green colour encoding");
Assert(EaModelWriter.ParseEaColor("#0000FF") == 16711680, "EA blue colour encoding");
Assert(EaModelWriter.ParseEaColor("not-a-colour") is null, "invalid EA colour");

var linkmlKeys = SimpleYaml.Parse("""
classes:
  Reading:
    unique_keys:
      reading_key:
        unique_key_slots: [meter_id, effective_from]
    attributes:
      meter_id:
        range: string
      effective_from:
        range: datetime
      sequence:
        identifier: true
        range: integer
""");
var keyModel = new SchemaConverter().Convert(linkmlKeys, "keys");
var reading = keyModel.Classes.Single(x => x.Name == "Reading");
Assert(reading.Properties.Single(x => x.Name == "meter_id").Identifier, "first composite key member");
Assert(reading.Properties.Single(x => x.Name == "effective_from").Identifier, "second composite key member");
Assert(reading.Properties.Single(x => x.Name == "sequence").Identifier, "LinkML identifier");

var layoutModel = new ImportModel { Name = "GenericModel" };
var hub = new ImportClass { Name = "Hub" };
hub.Properties.Add(new ImportProperty { Name = "first", Type = "First", IsReference = true });
hub.Properties.Add(new ImportProperty { Name = "second", Type = "Second", IsReference = true });
hub.Properties.Add(new ImportProperty { Name = "third", Type = "Third", IsReference = true });
hub.Properties.Add(new ImportProperty { Name = "status", Type = "Status" });
layoutModel.Classes.Add(hub);
layoutModel.Classes.Add(new ImportClass { Name = "First" });
layoutModel.Classes.Add(new ImportClass { Name = "Second" });
layoutModel.Classes.Add(new ImportClass { Name = "Third" });
layoutModel.Classes.Add(new ImportClass { Name = "Disconnected" });
var statusEnum = new ImportEnum { Name = "Status" };
statusEnum.Values.AddRange(["New", "Active", "Closed"]);
layoutModel.Enums.Add(statusEnum);
var smartLayout = SmartDiagramLayout.Arrange(layoutModel);
Assert(smartLayout.Count == 6, "layout contains every class and enumeration");
Assert(smartLayout["Hub"].Left < smartLayout["First"].Left, "most-connected hub starts the component layers");
Assert(smartLayout["Status"].Left > layoutModel.Classes.Max(x => smartLayout[x.Name].Right), "enumerations are separated from classes");
Assert(!smartLayout.SelectMany((left, index) => smartLayout.Skip(index + 1).Select(right => Overlaps(left.Value, right.Value))).Any(x => x), "layout boxes do not overlap");
var repeatedLayout = SmartDiagramLayout.Arrange(layoutModel);
Assert(smartLayout.All(x => repeatedLayout[x.Key] == x.Value), "layout is deterministic");

var ontologyModel = new ImportModel
{
    Name = "People Model",
    Description = "People & organisations",
    Version = "2.0",
    OntologyIri = "https://example.org/people"
};
var personClass = new ImportClass { Name = "Person", Description = "A person" };
personClass.Properties.Add(new ImportProperty
{
    Name = "identifier", Type = "String", Required = true, Identifier = true, Description = "Stable identifier"
});
personClass.Properties.Add(new ImportProperty { Name = "status", Type = "Status" });
var employeeClass = new ImportClass { Name = "Employee" };
employeeClass.Parents.Add("Person");
employeeClass.Properties.Add(new ImportProperty
{
    Name = "manager", Type = "Person", IsReference = true, Many = false, Description = "Line manager"
});
ontologyModel.Classes.AddRange([personClass, employeeClass]);
var ontologyEnum = new ImportEnum { Name = "Status", Description = "Employment status" };
ontologyEnum.Values.AddRange(["Active", "Inactive"]);
ontologyModel.Enums.Add(ontologyEnum);
string turtleOntology = OwlSerializer.Serialize(ontologyModel, OwlSerialization.Turtle);
Assert(turtleOntology.Contains("a owl:Ontology"), "Turtle ontology declaration");
Assert(turtleOntology.Contains("a owl:ObjectProperty"), "Turtle object property");
Assert(turtleOntology.Contains("a owl:DatatypeProperty"), "Turtle datatype property");
Assert(turtleOntology.Contains("owl:cardinality \"1\"^^xsd:nonNegativeInteger"), "Turtle cardinality restriction");
Assert(turtleOntology.Contains("owl:oneOf"), "Turtle enumeration");
Assert(turtleOntology.Contains("https://example.org/people#isIdentifier"), "Turtle identifier annotation");
string xmlOntology = OwlSerializer.Serialize(ontologyModel, OwlSerialization.RdfXml);
var ontologyXml = new System.Xml.XmlDocument();
ontologyXml.LoadXml(xmlOntology);
Assert(ontologyXml.DocumentElement?.LocalName == "RDF", "RDF/XML is well formed");
Assert(xmlOntology.Contains("owl:ObjectProperty"), "RDF/XML object property");
Assert(xmlOntology.Contains("owl:NamedIndividual"), "RDF/XML enumeration individuals");
Assert(xmlOntology.Contains("owl:versionInfo"), "RDF/XML version metadata");

var yaml = """
$schema: https://json-schema.org/draft/2020-12/schema
title: Product Catalogue
type: object
required:
  - products
properties:
  products:
    type: array
    items:
      type: object
      title: Product
      properties:
        code:
          type: string
""";
var yamlModel = new SchemaConverter().Convert(SimpleYaml.Parse(yaml), "catalogue");
Assert(yamlModel.Name == "ProductCatalogue", "YAML title");
Assert(yamlModel.Classes.Any(x => x.Name == "Product"), "YAML nested class");
Console.WriteLine("All importer tests passed.");

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("Failed: " + name);
}

static bool Overlaps(DiagramBox first, DiagramBox second) =>
    first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;
