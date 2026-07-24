using System.Drawing;
using System.Windows.Forms;

namespace EAJsonModelImporter;

internal sealed class ImportOptionsDialog : Form
{
    private readonly RadioButton? _updateExisting;

    public bool UpdateExisting => _updateExisting?.Checked == true;

    public ImportOptionsDialog(string modelName, string targetName, int classCount, int enumCount,
        string diagramSummary, string? existingPackageName = null)
    {
        Text = "Confirm UML import";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, existingPackageName is null ? 145 : 205);

        var summary = new Label
        {
            AutoSize = false,
            Location = new Point(18, 16),
            Size = new Size(484, 65),
            Text = $"Import '{modelName}' under '{targetName}'?\r\n\r\n" +
                   $"{classCount} classes, {enumCount} enumerations{diagramSummary}"
        };
        int buttonY = existingPackageName is null ? 101 : 161;
        var ok = new Button { Text = "Import", DialogResult = DialogResult.OK, Location = new Point(336, buttonY), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(422, buttonY), Size = new Size(80, 28) };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(summary);
        if (existingPackageName is not null)
        {
            _updateExisting = new RadioButton
            {
                AutoSize = true,
                Checked = true,
                Location = new Point(24, 91),
                Text = $"Update '{existingPackageName}' and preserve diagram positions"
            };
            var createCopy = new RadioButton
            {
                AutoSize = true,
                Location = new Point(24, 120),
                Text = "Create a new package"
            };
            Controls.AddRange([_updateExisting, createCopy]);
        }
        Controls.AddRange([ok, cancel]);
    }
}
