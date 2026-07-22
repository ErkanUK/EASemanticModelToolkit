using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EAJsonModelImporter;

internal enum OwlSerialization { RdfXml, Turtle }

internal static class OwlSerializer
{
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private const string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    private const string Owl = "http://www.w3.org/2002/07/owl#";
    private const string Xsd = "http://www.w3.org/2001/XMLSchema#";

    public static string Serialize(ImportModel model, OwlSerialization format)
    {
        var iris = IriContext.Create(model);
        return format == OwlSerialization.Turtle ? Turtle(model, iris) : RdfXml(model, iris);
    }

    private static string Turtle(ImportModel model, IriContext iris)
    {
        var text = new StringBuilder();
        text.AppendLine("@prefix rdf: <" + Rdf + "> .");
        text.AppendLine("@prefix rdfs: <" + Rdfs + "> .");
        text.AppendLine("@prefix owl: <" + Owl + "> .");
        text.AppendLine("@prefix xsd: <" + Xsd + "> .");
        text.AppendLine();
        text.Append('<').Append(iris.OntologyIri).Append("> a owl:Ontology ;\n  rdfs:label ")
            .Append(Literal(model.Name));
        if (model.Description.Length > 0) text.Append(" ;\n  rdfs:comment ").Append(Literal(model.Description));
        if (model.Version.Length > 0) text.Append(" ;\n  owl:versionInfo ").Append(Literal(model.Version));
        text.AppendLine(" .").AppendLine();

        bool hasIdentifiers = model.Classes.SelectMany(x => x.Properties).Any(x => x.Identifier);
        if (hasIdentifiers)
            text.Append('<').Append(iris.IdentifierIri).AppendLine("> a owl:AnnotationProperty .").AppendLine();

        var classNames = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enumNames = model.Enums.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string classIri = iris.Entity(cls.Name);
            text.Append('<').Append(classIri).Append("> a owl:Class ;\n  rdfs:label ").Append(Literal(cls.Name));
            if (cls.Description.Length > 0) text.Append(" ;\n  rdfs:comment ").Append(Literal(cls.Description));
            text.AppendLine(" .");
            foreach (var parent in cls.Parents.Where(classNames.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
                text.Append('<').Append(classIri).Append("> rdfs:subClassOf <").Append(iris.Entity(parent)).AppendLine("> .");
            foreach (var property in cls.Properties)
                WriteTurtleRestrictions(text, classIri, iris.Property(cls.Name, property.Name), property);
            text.AppendLine();
        }

        foreach (var definition in model.Enums.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string enumIri = iris.Entity(definition.Name);
            var values = EnumValues(definition, iris);
            text.Append('<').Append(enumIri).Append("> a owl:Class ;\n  rdfs:label ").Append(Literal(definition.Name));
            if (definition.Description.Length > 0) text.Append(" ;\n  rdfs:comment ").Append(Literal(definition.Description));
            text.Append(" ;\n  owl:oneOf (");
            foreach (var value in values) text.Append(" <").Append(value.Iri).Append('>');
            text.AppendLine(" ) .");
            foreach (var value in values)
            {
                text.Append('<').Append(value.Iri).Append("> a owl:NamedIndividual, <").Append(enumIri)
                    .Append("> ;\n  rdfs:label ").Append(Literal(value.Value));
                if (value.Description.Length > 0) text.Append(" ;\n  rdfs:comment ").Append(Literal(value.Description));
                text.AppendLine(" .");
            }
            text.AppendLine();
        }

        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        foreach (var property in cls.Properties.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            bool objectProperty = property.IsReference || classNames.Contains(property.Type) || enumNames.Contains(property.Type);
            string propertyIri = iris.Property(cls.Name, property.Name);
            text.Append('<').Append(propertyIri).Append(objectProperty ? "> a owl:ObjectProperty" : "> a owl:DatatypeProperty")
                .Append(" ;\n  rdfs:label ").Append(Literal(property.Name))
                .Append(" ;\n  rdfs:domain <").Append(iris.Entity(cls.Name)).Append('>');
            if (objectProperty)
            {
                string range = classNames.Contains(property.Type) || enumNames.Contains(property.Type)
                    ? iris.Entity(property.Type) : Owl + "Thing";
                text.Append(" ;\n  rdfs:range <").Append(range).Append('>');
            }
            else text.Append(" ;\n  rdfs:range ").Append(DatatypeQName(property.Type));
            if (property.Description.Length > 0) text.Append(" ;\n  rdfs:comment ").Append(Literal(property.Description));
            if (property.Identifier) text.Append(" ;\n  <").Append(iris.IdentifierIri).Append("> true");
            text.AppendLine(" .").AppendLine();
        }
        return text.ToString();
    }

    private static void WriteTurtleRestrictions(StringBuilder text, string classIri, string propertyIri, ImportProperty property)
    {
        if (property.Required && !property.Many)
        {
            Restriction("owl:cardinality", 1);
            return;
        }
        if (property.Required) Restriction("owl:minCardinality", 1);
        if (!property.Many) Restriction("owl:maxCardinality", 1);
        return;

        void Restriction(string predicate, int value) => text.Append('<').Append(classIri)
            .Append("> rdfs:subClassOf [ a owl:Restriction ; owl:onProperty <").Append(propertyIri)
            .Append("> ; ").Append(predicate).Append(" \"").Append(value)
            .AppendLine("\"^^xsd:nonNegativeInteger ] .");
    }

    private static string RdfXml(ImportModel model, IriContext iris)
    {
        var output = new StringBuilder();
        using var writer = XmlWriter.Create(output, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true });
        writer.WriteStartElement("rdf", "RDF", Rdf);
        writer.WriteAttributeString("xmlns", "rdfs", null, Rdfs);
        writer.WriteAttributeString("xmlns", "owl", null, Owl);
        writer.WriteAttributeString("xmlns", "xsd", null, Xsd);
        writer.WriteAttributeString("xmlns", "ea", null, iris.EntityBase);

        writer.WriteStartElement("owl", "Ontology", Owl);
        ResourceAttribute(writer, "about", iris.OntologyIri);
        TextElement(writer, "rdfs", "label", Rdfs, model.Name);
        if (model.Description.Length > 0) TextElement(writer, "rdfs", "comment", Rdfs, model.Description);
        if (model.Version.Length > 0) TextElement(writer, "owl", "versionInfo", Owl, model.Version);
        writer.WriteEndElement();

        bool hasIdentifiers = model.Classes.SelectMany(x => x.Properties).Any(x => x.Identifier);
        if (hasIdentifiers)
        {
            writer.WriteStartElement("owl", "AnnotationProperty", Owl);
            ResourceAttribute(writer, "about", iris.IdentifierIri);
            writer.WriteEndElement();
        }

        var classNames = model.Classes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enumNames = model.Enums.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartElement("owl", "Class", Owl);
            ResourceAttribute(writer, "about", iris.Entity(cls.Name));
            TextElement(writer, "rdfs", "label", Rdfs, cls.Name);
            if (cls.Description.Length > 0) TextElement(writer, "rdfs", "comment", Rdfs, cls.Description);
            foreach (var parent in cls.Parents.Where(classNames.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
                ResourceElement(writer, "rdfs", "subClassOf", Rdfs, iris.Entity(parent));
            foreach (var property in cls.Properties) WriteXmlRestrictions(writer, iris.Property(cls.Name, property.Name), property);
            writer.WriteEndElement();
        }

        foreach (var definition in model.Enums.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            string enumIri = iris.Entity(definition.Name);
            var values = EnumValues(definition, iris);
            writer.WriteStartElement("owl", "Class", Owl);
            ResourceAttribute(writer, "about", enumIri);
            TextElement(writer, "rdfs", "label", Rdfs, definition.Name);
            if (definition.Description.Length > 0) TextElement(writer, "rdfs", "comment", Rdfs, definition.Description);
            writer.WriteStartElement("owl", "oneOf", Owl);
            writer.WriteAttributeString("rdf", "parseType", Rdf, "Collection");
            foreach (var value in values)
            {
                writer.WriteStartElement("rdf", "Description", Rdf);
                ResourceAttribute(writer, "about", value.Iri);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();

            foreach (var value in values)
            {
                writer.WriteStartElement("owl", "NamedIndividual", Owl);
                ResourceAttribute(writer, "about", value.Iri);
                ResourceElement(writer, "rdf", "type", Rdf, enumIri);
                TextElement(writer, "rdfs", "label", Rdfs, value.Value);
                if (value.Description.Length > 0) TextElement(writer, "rdfs", "comment", Rdfs, value.Description);
                writer.WriteEndElement();
            }
        }

        foreach (var cls in model.Classes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        foreach (var property in cls.Properties.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            bool objectProperty = property.IsReference || classNames.Contains(property.Type) || enumNames.Contains(property.Type);
            writer.WriteStartElement("owl", objectProperty ? "ObjectProperty" : "DatatypeProperty", Owl);
            ResourceAttribute(writer, "about", iris.Property(cls.Name, property.Name));
            TextElement(writer, "rdfs", "label", Rdfs, property.Name);
            ResourceElement(writer, "rdfs", "domain", Rdfs, iris.Entity(cls.Name));
            string range = objectProperty
                ? (classNames.Contains(property.Type) || enumNames.Contains(property.Type) ? iris.Entity(property.Type) : Owl + "Thing")
                : DatatypeIri(property.Type);
            ResourceElement(writer, "rdfs", "range", Rdfs, range);
            if (property.Description.Length > 0) TextElement(writer, "rdfs", "comment", Rdfs, property.Description);
            if (property.Identifier)
            {
                writer.WriteStartElement("ea", "isIdentifier", iris.EntityBase);
                writer.WriteAttributeString("rdf", "datatype", Rdf, Xsd + "boolean");
                writer.WriteString("true");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.Flush();
        return output.ToString();
    }

    private static void WriteXmlRestrictions(XmlWriter writer, string propertyIri, ImportProperty property)
    {
        if (property.Required && !property.Many)
        {
            Restriction("cardinality", 1);
            return;
        }
        if (property.Required) Restriction("minCardinality", 1);
        if (!property.Many) Restriction("maxCardinality", 1);
        return;

        void Restriction(string name, int value)
        {
            writer.WriteStartElement("rdfs", "subClassOf", Rdfs);
            writer.WriteStartElement("owl", "Restriction", Owl);
            ResourceElement(writer, "owl", "onProperty", Owl, propertyIri);
            writer.WriteStartElement("owl", name, Owl);
            writer.WriteAttributeString("rdf", "datatype", Rdf, Xsd + "nonNegativeInteger");
            writer.WriteString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }

    private static List<EnumValue> EnumValues(ImportEnum definition, IriContext iris)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EnumValue>();
        for (int index = 0; index < definition.Values.Count; index++)
        {
            string value = definition.Values[index];
            string local = Local(definition.Name) + "_" + Local(value);
            if (!used.Add(local)) local += "_" + (index + 1);
            string description = definition.ValueDescriptions.TryGetValue(value, out var notes) ? notes : "";
            result.Add(new EnumValue(value, iris.EntityBase + local, description));
        }
        return result;
    }

    private static string DatatypeQName(string type) => DatatypeIri(type) switch
    {
        var iri when iri.StartsWith(Xsd, StringComparison.Ordinal) => "xsd:" + iri[Xsd.Length..],
        _ => "rdfs:Literal"
    };

    private static string DatatypeIri(string type) => type.ToLowerInvariant() switch
    {
        "integer" or "int" or "long" => Xsd + "integer",
        "real" or "float" or "double" or "decimal" or "number" => Xsd + "decimal",
        "boolean" or "bool" => Xsd + "boolean",
        "date" => Xsd + "date",
        "datetime" or "date_time" or "date-time" => Xsd + "dateTime",
        "time" => Xsd + "time",
        "uri" or "uriorcurie" => Xsd + "anyURI",
        _ => Xsd + "string"
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

    private static void ResourceAttribute(XmlWriter writer, string localName, string value) =>
        writer.WriteAttributeString("rdf", localName, Rdf, value);

    private static void ResourceElement(XmlWriter writer, string prefix, string localName, string ns, string resource)
    {
        writer.WriteStartElement(prefix, localName, ns);
        ResourceAttribute(writer, "resource", resource);
        writer.WriteEndElement();
    }

    private static void TextElement(XmlWriter writer, string prefix, string localName, string ns, string value) =>
        writer.WriteElementString(prefix, localName, ns, value);

    private sealed record EnumValue(string Value, string Iri, string Description);

    private sealed record IriContext(string OntologyIri, string EntityBase)
    {
        public string IdentifierIri => EntityBase + "isIdentifier";
        public string Entity(string name) => EntityBase + Local(name);
        public string Property(string owner, string name) => EntityBase + Local(owner) + "_" + Local(name);

        public static IriContext Create(ImportModel model)
        {
            string ontology = Uri.TryCreate(model.OntologyIri, UriKind.Absolute, out var source)
                ? source.AbsoluteUri.TrimEnd('#')
                : "urn:ea:model:" + Local(model.Name);
            string entityBase = ontology.EndsWith("/", StringComparison.Ordinal) ? ontology : ontology + "#";
            return new IriContext(ontology, entityBase);
        }
    }
}
