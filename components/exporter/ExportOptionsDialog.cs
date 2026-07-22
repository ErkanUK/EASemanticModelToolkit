using System.Drawing;
using System.Windows.Forms;

namespace EA17LinkMLExporter;

internal sealed record ExportOptions(bool LinkMl, bool JsonSchema, bool Markdown, bool DrawIo, bool Svg,
    bool Owl, bool Turtle)
{
    public static ExportOptions All { get; } = new(true, true, true, true, true, true, true);
}

internal sealed class ExportOptionsDialog : Form
{
    private readonly CheckBox _linkMl = Box("LinkML YAML (.linkml.yaml)");
    private readonly CheckBox _jsonSchema = Box("JSON Schema (.schema.json)");
    private readonly CheckBox _markdown = Box("Markdown documentation (.md)");
    private readonly CheckBox _drawIo = Box("Editable draw.io diagram (.drawio)");
    private readonly CheckBox _svg = Box("SVG diagram (.svg)");
    private readonly CheckBox _owl = Box("OWL ontology (.owl, RDF/XML)");
    private readonly CheckBox _turtle = Box("OWL ontology (.ttl, Turtle)");

    public ExportOptions Options => new(_linkMl.Checked, _jsonSchema.Checked, _markdown.Checked, _drawIo.Checked,
        _svg.Checked, _owl.Checked, _turtle.Checked);

    public ExportOptionsDialog(string packageName)
    {
        Text = "Export model documentation";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 280);

        var summary = new Label
        {
            AutoSize = false,
            Location = new Point(18, 16),
            Size = new Size(564, 50),
            Text = $"Export '{packageName}'?\r\n\r\nSelect the formats to generate:"
        };
        _linkMl.Location = new Point(34, 82);
        _jsonSchema.Location = new Point(34, 113);
        _markdown.Location = new Point(34, 144);
        _drawIo.Location = new Point(315, 82);
        _svg.Location = new Point(315, 113);
        _owl.Location = new Point(315, 144);
        _turtle.Location = new Point(315, 175);
        var ok = new Button { Text = "Continue", DialogResult = DialogResult.OK, Location = new Point(416, 236), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(502, 236), Size = new Size(80, 28) };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([summary, _linkMl, _jsonSchema, _markdown, _drawIo, _svg, _owl, _turtle, ok, cancel]);
    }

    private static CheckBox Box(string text) => new() { AutoSize = true, Text = text, Checked = true };
}
