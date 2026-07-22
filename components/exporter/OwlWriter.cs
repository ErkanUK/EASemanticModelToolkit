using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EA17LinkMLExporter;

internal static class OwlWriter
{
    private const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private const string RdfsUri = "http://www.w3.org/2000/01/rdf-schema#";
    private const string OwlUri = "http://www.w3.org/2002/07/owl#";
    private const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

    public static string WriteTurtle(ModelSnapshot model)
    {
        var iris = IriContext.Create(model);
        var properties = Properties(model);
        var classes = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enums = model.Enums.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = new StringBuilder();
        b.AppendLine("@prefix rdf: <" + RdfUri + "> .")
            .AppendLine("@prefix rdfs: <" + RdfsUri + "> .")
            .AppendLine("@prefix owl: <" + OwlUri + "> .")
            .AppendLine("@prefix xsd: <" + XsdUri + "> .").AppendLine();
        b.Append('<').Append(iris.Ontology).Append("> a owl:Ontology ;\n  rdfs:label ").Append(Literal(model.Name));
        if (model.Notes.Length > 0) b.Append(" ;\n  rdfs:comment ").Append(Literal(model.Notes));
        if (model.Version.Length > 0) b.Append(" ;\n  owl:versionInfo ").Append(Literal(model.Version));
        b.AppendLine(" .").AppendLine();
        if (properties.Any(x => x.Identifier))
            b.Append('<').Append(iris.Identifier).AppendLine("> a owl:AnnotationProperty .").AppendLine();

        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string classIri = iris.Entity(cls.Name);
            b.Append('<').Append(classIri).Append("> a owl:Class ;\n  rdfs:label ").Append(Literal(cls.Name));
            if (cls.Notes.Length > 0) b.Append(" ;\n  rdfs:comment ").Append(Literal(cls.Notes));
            b.AppendLine(" .");
            foreach (var parent in cls.Parents.Where(classes.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
                b.Append('<').Append(classIri).Append("> rdfs:subClassOf <").Append(iris.Entity(parent)).AppendLine("> .");
            foreach (var property in properties.Where(x => x.Owner.Equals(cls.Name, StringComparison.OrdinalIgnoreCase)))
                WriteTurtleRestrictions(b, classIri, iris.Property(property.Owner, property.IriName), property);
            b.AppendLine();
        }

        foreach (var item in model.Enums.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string enumIri = iris.Entity(item.Name);
            var values = EnumValues(item, iris);
            b.Append('<').Append(enumIri).Append("> a owl:Class ;\n  rdfs:label ").Append(Literal(item.Name));
            if (item.Notes.Length > 0) b.Append(" ;\n  rdfs:comment ").Append(Literal(item.Notes));
            b.Append(" ;\n  owl:oneOf (");
            foreach (var value in values) b.Append(" <").Append(value.Iri).Append('>');
            b.AppendLine(" ) .");
            foreach (var value in values)
                b.Append('<').Append(value.Iri).Append("> a owl:NamedIndividual, <").Append(enumIri)
                    .Append("> ; rdfs:label ").Append(Literal(value.Label)).AppendLine(" .");
            b.AppendLine();
        }

        foreach (var property in properties.OrderBy(x => x.Owner).ThenBy(x => x.IriName))
        {
            string propertyIri = iris.Property(property.Owner, property.IriName);
            bool objectProperty = property.Object || classes.Contains(property.Type) || enums.Contains(property.Type);
            b.Append('<').Append(propertyIri).Append(objectProperty ? "> a owl:ObjectProperty" : "> a owl:DatatypeProperty")
                .Append(" ;\n  rdfs:label ").Append(Literal(property.Name))
                .Append(" ;\n  rdfs:domain <").Append(iris.Entity(property.Owner)).Append('>');
            if (objectProperty)
            {
                string range = classes.Contains(property.Type) || enums.Contains(property.Type)
                    ? iris.Entity(property.Type) : OwlUri + "Thing";
                b.Append(" ;\n  rdfs:range <").Append(range).Append('>');
            }
            else b.Append(" ;\n  rdfs:range ").Append(DatatypeQName(property.Type));
            if (property.Notes.Length > 0) b.Append(" ;\n  rdfs:comment ").Append(Literal(property.Notes));
            if (property.Identifier) b.Append(" ;\n  <").Append(iris.Identifier).Append("> true");
            b.AppendLine(" .").AppendLine();
        }
        return b.ToString();
    }

    public static string WriteRdfXml(ModelSnapshot model)
    {
        XNamespace rdf = RdfUri, rdfs = RdfsUri, owl = OwlUri, xsd = XsdUri;
        var iris = IriContext.Create(model);
        XNamespace ea = iris.EntityBase;
        var properties = Properties(model);
        var classes = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enums = model.Enums.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var root = new XElement(rdf + "RDF",
            new XAttribute(XNamespace.Xmlns + "rdf", rdf), new XAttribute(XNamespace.Xmlns + "rdfs", rdfs),
            new XAttribute(XNamespace.Xmlns + "owl", owl), new XAttribute(XNamespace.Xmlns + "xsd", xsd),
            new XAttribute(XNamespace.Xmlns + "ea", ea));
        root.Add(new XElement(owl + "Ontology", new XAttribute(rdf + "about", iris.Ontology),
            new XElement(rdfs + "label", model.Name), Comment(rdfs, model.Notes),
            model.Version.Length == 0 ? null : new XElement(owl + "versionInfo", model.Version)));
        if (properties.Any(x => x.Identifier))
            root.Add(new XElement(owl + "AnnotationProperty", new XAttribute(rdf + "about", iris.Identifier)));

        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string classIri = iris.Entity(cls.Name);
            var element = new XElement(owl + "Class", new XAttribute(rdf + "about", classIri),
                new XElement(rdfs + "label", cls.Name), Comment(rdfs, cls.Notes));
            foreach (var parent in cls.Parents.Where(classes.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
                element.Add(new XElement(rdfs + "subClassOf", new XAttribute(rdf + "resource", iris.Entity(parent))));
            foreach (var property in properties.Where(x => x.Owner.Equals(cls.Name, StringComparison.OrdinalIgnoreCase)))
                foreach (var restriction in XmlRestrictions(rdf, rdfs, owl, xsd,
                             iris.Property(property.Owner, property.IriName), property)) element.Add(restriction);
            root.Add(element);
        }

        foreach (var item in model.Enums.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string enumIri = iris.Entity(item.Name);
            var values = EnumValues(item, iris);
            root.Add(new XElement(owl + "Class", new XAttribute(rdf + "about", enumIri),
                new XElement(rdfs + "label", item.Name), Comment(rdfs, item.Notes),
                new XElement(owl + "oneOf", new XAttribute(rdf + "parseType", "Collection"),
                    values.Select(x => new XElement(rdf + "Description", new XAttribute(rdf + "about", x.Iri))))));
            foreach (var value in values)
                root.Add(new XElement(owl + "NamedIndividual", new XAttribute(rdf + "about", value.Iri),
                    new XElement(rdf + "type", new XAttribute(rdf + "resource", enumIri)),
                    new XElement(rdfs + "label", value.Label)));
        }

        foreach (var property in properties.OrderBy(x => x.Owner).ThenBy(x => x.IriName))
        {
            bool objectProperty = property.Object || classes.Contains(property.Type) || enums.Contains(property.Type);
            string range = objectProperty
                ? (classes.Contains(property.Type) || enums.Contains(property.Type) ? iris.Entity(property.Type) : OwlUri + "Thing")
                : DatatypeIri(property.Type);
            root.Add(new XElement(owl + (objectProperty ? "ObjectProperty" : "DatatypeProperty"),
                new XAttribute(rdf + "about", iris.Property(property.Owner, property.IriName)),
                new XElement(rdfs + "label", property.Name),
                new XElement(rdfs + "domain", new XAttribute(rdf + "resource", iris.Entity(property.Owner))),
                new XElement(rdfs + "range", new XAttribute(rdf + "resource", range)),
                Comment(rdfs, property.Notes),
                property.Identifier ? new XElement(ea + "isIdentifier", new XAttribute(rdf + "datatype", XsdUri + "boolean"), "true") : null));
        }
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
    }

    private static List<OwlProperty> Properties(ModelSnapshot model)
    {
        var result = new List<OwlProperty>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in model.Classes)
        foreach (var property in cls.Properties)
            Add(cls.Name, property.Name, property.Type, false, property.Notes, property.Lower, property.Upper, property.Identifier);
        foreach (var relation in model.Relations)
        {
            Add(relation.SourceName, Role(relation.TargetRole, relation.TargetName), relation.TargetName, true,
                relation.Notes, Lower(relation.TargetMultiplicity), Upper(relation.TargetMultiplicity), false);
            if (!string.IsNullOrWhiteSpace(relation.SourceRole))
                Add(relation.TargetName, relation.SourceRole, relation.SourceName, true, relation.Notes,
                    Lower(relation.SourceMultiplicity), Upper(relation.SourceMultiplicity), false);
        }
        return result;

        void Add(string owner, string name, string type, bool isObject, string notes, string lower, string upper, bool identifier)
        {
            string iriName = name;
            int suffix = 2;
            while (!used.Add(owner + "|" + Local(iriName))) iriName = name + "_" + suffix++;
            result.Add(new OwlProperty(owner, name, iriName, type, isObject, notes, lower, upper, identifier));
        }
    }

    private static void WriteTurtleRestrictions(StringBuilder b, string classIri, string propertyIri, OwlProperty property)
    {
        var bounds = Bounds(property.Lower, property.Upper);
        if (bounds.Lower == 1 && bounds.Upper == 1) Restriction("owl:cardinality", 1);
        else
        {
            if (bounds.Lower > 0) Restriction("owl:minCardinality", bounds.Lower.Value);
            if (bounds.Upper is not null) Restriction("owl:maxCardinality", bounds.Upper.Value);
        }
        return;
        void Restriction(string predicate, int value) => b.Append('<').Append(classIri)
            .Append("> rdfs:subClassOf [ a owl:Restriction ; owl:onProperty <").Append(propertyIri)
            .Append("> ; ").Append(predicate).Append(" \"").Append(value)
            .AppendLine("\"^^xsd:nonNegativeInteger ] .");
    }

    private static IEnumerable<XElement> XmlRestrictions(XNamespace rdf, XNamespace rdfs, XNamespace owl,
        XNamespace xsd, string propertyIri, OwlProperty property)
    {
        var bounds = Bounds(property.Lower, property.Upper);
        if (bounds.Lower == 1 && bounds.Upper == 1) yield return Restriction("cardinality", 1);
        else
        {
            if (bounds.Lower > 0) yield return Restriction("minCardinality", bounds.Lower.Value);
            if (bounds.Upper is not null) yield return Restriction("maxCardinality", bounds.Upper.Value);
        }
        XElement Restriction(string name, int value) => new(rdfs + "subClassOf",
            new XElement(owl + "Restriction",
                new XElement(owl + "onProperty", new XAttribute(rdf + "resource", propertyIri)),
                new XElement(owl + name, new XAttribute(rdf + "datatype", XsdUri + "nonNegativeInteger"), value)));
    }

    private static (int? Lower, int? Upper) Bounds(string lower, string upper) => (Number(lower), Number(upper));
    private static int? Number(string value) => int.TryParse(value, out int number) ? number : null;
    private static string Lower(string value) => value.Contains("..") ? value.Split("..")[0] : value == "*" ? "0" : value;
    private static string Upper(string value) => value.Contains("..") ? value.Split("..")[1] : value;
    private static string Role(string role, string target) => string.IsNullOrWhiteSpace(role) ? "has" + Local(target) : role;
    private static XElement? Comment(XNamespace rdfs, string value) => value.Length == 0 ? null : new XElement(rdfs + "comment", value);

    private static List<EnumValue> EnumValues(UmlEnum item, IriContext iris) => item.Values.Select((value, index) =>
        new EnumValue(value, iris.EntityBase + Local(item.Name) + "_" + Local(value) + "_" + (index + 1))).ToList();

    private static string DatatypeQName(string type) => "xsd:" + DatatypeIri(type)[XsdUri.Length..];
    private static string DatatypeIri(string type) => type.ToLowerInvariant() switch
    {
        "integer" or "int" or "long" => XsdUri + "integer",
        "real" or "float" or "double" or "decimal" or "number" => XsdUri + "decimal",
        "boolean" or "bool" => XsdUri + "boolean",
        "date" => XsdUri + "date",
        "datetime" or "date_time" or "date-time" => XsdUri + "dateTime",
        "time" => XsdUri + "time",
        "uri" or "uriorcurie" => XsdUri + "anyURI",
        _ => XsdUri + "string"
    };

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static string Local(string value)
    {
        string local = Regex.Replace(value.Trim(), "[^A-Za-z0-9_.-]+", "_").Trim('_');
        if (local.Length == 0) local = "Entity";
        if (char.IsDigit(local[0])) local = "_" + local;
        return local;
    }

    private sealed record OwlProperty(string Owner, string Name, string IriName, string Type, bool Object,
        string Notes, string Lower, string Upper, bool Identifier);
    private sealed record EnumValue(string Label, string Iri);
    private sealed record IriContext(string Ontology, string EntityBase)
    {
        public string Identifier => EntityBase + "isIdentifier";
        public string Entity(string name) => EntityBase + Local(name);
        public string Property(string owner, string name) => EntityBase + Local(owner) + "_" + Local(name);
        public static IriContext Create(ModelSnapshot model)
        {
            string ontology = Uri.TryCreate(model.OntologyIri, UriKind.Absolute, out var uri)
                ? uri.AbsoluteUri.TrimEnd('#') : "urn:ea:model:" + Local(model.Name);
            return new IriContext(ontology, ontology.EndsWith("/", StringComparison.Ordinal) ? ontology : ontology + "#");
        }
    }
}
