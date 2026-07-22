using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EA17LinkMLExporter;

[ComVisible(true)]
[Guid("82F1E748-F71A-4AC9-B12A-53EF197C2EF8")]
[ProgId("EA17LinkMLExporter.Addin")]
[ClassInterface(ClassInterfaceType.AutoDual)]
public sealed class Addin
{
    private const string Menu = "-&LinkML Documentation";
    private const string ExportItem = "Export selected package…";
    private const string AboutItem = "About EA17 LinkML Exporter";

    public string EA_Connect(EA.Repository repository) => "EA17LinkMLExporter";
    public void EA_Disconnect() { }

    public object EA_GetMenuItems(EA.Repository repository, string location, string menuName) => menuName switch
    {
        "" => Menu,
        Menu => new[] { ExportItem, AboutItem },
        _ => ""
    };

    public void EA_GetMenuState(EA.Repository repository, string location, string menuName, string itemName,
        ref bool isEnabled, ref bool isChecked)
    {
        isChecked = false;
        isEnabled = itemName == AboutItem || GetSelectedPackage(repository) is not null;
    }

    public void EA_MenuClick(EA.Repository repository, string location, string menuName, string itemName)
    {
        if (itemName == AboutItem)
        {
            MessageBox.Show("Exports the selected UML package to selectable LinkML YAML, JSON Schema, Markdown, draw.io, native EA SVG diagrams, OWL/RDF-XML and Turtle formats.",
                "EA17 LinkML Exporter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (itemName != ExportItem) return;
        var package = GetSelectedPackage(repository);
        if (package is null)
        {
            MessageBox.Show("Select a package in the Browser first.", "EA17 LinkML Exporter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var optionsDialog = new ExportOptionsDialog(package.Name);
        if (optionsDialog.ShowDialog() != DialogResult.OK) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder that will contain the generated documentation",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            var output = Exporter.Export(repository, package, dialog.SelectedPath, optionsDialog.Options);
            MessageBox.Show("Export complete:\n" + output, "EA17 LinkML Exporter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed:\n" + ex.Message, "EA17 LinkML Exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static EA.Package? GetSelectedPackage(EA.Repository repository)
    {
        try { return repository.GetTreeSelectedPackage(); }
        catch { return null; }
    }
}
