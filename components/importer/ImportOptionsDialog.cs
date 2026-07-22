using System.Drawing;
using System.Windows.Forms;

namespace EAJsonModelImporter;

internal sealed class ImportOptionsDialog : Form
{
    public ImportOptionsDialog(string modelName, string targetName, int classCount, int enumCount, string diagramSummary)
    {
        Text = "Confirm UML import";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 145);

        var summary = new Label
        {
            AutoSize = false,
            Location = new Point(18, 16),
            Size = new Size(484, 65),
            Text = $"Create '{modelName}' under '{targetName}'?\r\n\r\n" +
                   $"{classCount} classes, {enumCount} enumerations{diagramSummary}"
        };
        var ok = new Button { Text = "Import", DialogResult = DialogResult.OK, Location = new Point(336, 101), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(422, 101), Size = new Size(80, 28) };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([summary, ok, cancel]);
    }
}
