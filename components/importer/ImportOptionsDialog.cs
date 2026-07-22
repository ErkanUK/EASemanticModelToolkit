using System.Drawing;
using System.Windows.Forms;

namespace EAJsonModelImporter;

internal sealed class ImportOptionsDialog : Form
{
    private readonly CheckBox _owl = new() { AutoSize = true, Text = "Export OWL (.owl, RDF/XML)" };
    private readonly CheckBox _turtle = new() { AutoSize = true, Text = "Export OWL Turtle (.ttl)" };

    public bool ExportOwl => _owl.Checked;
    public bool ExportTurtle => _turtle.Checked;

    public ImportOptionsDialog(string modelName, string targetName, int classCount, int enumCount, string diagramSummary)
    {
        Text = "Confirm UML import";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 245);

        var summary = new Label
        {
            AutoSize = false,
            Location = new Point(18, 16),
            Size = new Size(484, 65),
            Text = $"Create '{modelName}' under '{targetName}'?\r\n\r\n" +
                   $"{classCount} classes, {enumCount} enumerations{diagramSummary}"
        };
        var exportLabel = new Label
        {
            AutoSize = true,
            Location = new Point(18, 92),
            Text = "Optional ontology files (saved beside the source file):"
        };
        _owl.Location = new Point(34, 122);
        _turtle.Location = new Point(34, 151);

        var ok = new Button { Text = "Import", DialogResult = DialogResult.OK, Location = new Point(336, 201), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(422, 201), Size = new Size(80, 28) };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([summary, exportLabel, _owl, _turtle, ok, cancel]);
    }
}
