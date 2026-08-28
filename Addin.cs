using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EASemanticModelToolkit;

[ComVisible(true)]
[Guid("6BDE33B3-C200-4DEC-B692-CBC4293E07F0")]
[ProgId("EASemanticModelToolkit.Addin")]
[ClassInterface(ClassInterfaceType.AutoDual)]
public sealed class Addin
{
    private const string ProductName = "Semantic Model Toolkit";
    private const string Menu = "-&Semantic Model Toolkit";
    private const string ImportItem = "Import JSON/YAML into selected package...";
    private const string AttachMetadataItem = "Attach LinkML comments and annotations...";
    private const string ExportItem = "Export selected package...";
    private const string AboutItem = "About Semantic Model Toolkit";

    public string EA_Connect(EA.Repository repository) => "EASemanticModelToolkit";
    public void EA_Disconnect() { }

    public object EA_GetMenuItems(EA.Repository repository, string location, string menuName) => menuName switch
    {
        "" => Menu,
        Menu => new[] { ImportItem, AttachMetadataItem, ExportItem, AboutItem },
        _ => ""
    };

    public void EA_GetMenuState(EA.Repository repository, string location, string menuName, string itemName,
        ref bool isEnabled, ref bool isChecked)
    {
        isChecked = false;
        isEnabled = itemName == AboutItem || SelectedPackage(repository) is not null;
    }

    public void EA_MenuClick(EA.Repository repository, string location, string menuName, string itemName)
    {
        switch (itemName)
        {
            case ImportItem:
                Import(repository);
                break;
            case AttachMetadataItem:
                AttachMetadata(repository);
                break;
            case ExportItem:
                Export(repository);
                break;
            case AboutItem:
                using (var about = new AboutDialog())
                    about.ShowDialog();
                break;
        }
    }

    private static void Import(EA.Repository repository)
    {
        var target = SelectedPackage(repository);
        if (target is null)
        {
            MessageBox.Show("Select the target package in the Browser first.", ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Choose JSON, JSON Schema, or YAML",
            Filter = "Supported files|*.json;*.schema.json;*.yaml;*.yml|JSON|*.json|YAML|*.yaml;*.yml|All files|*.*"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var root = EAJsonModelImporter.InputLoader.Load(dialog.FileName);
            var model = new EAJsonModelImporter.SchemaConverter().Convert(root,
                Path.GetFileNameWithoutExtension(dialog.FileName));
            model.SourceComments = EAJsonModelImporter.InputLoader.ExtractYamlComments(dialog.FileName);
            if (model.UnsupportedLinkMlFeatures.Count > 0)
            {
                string features = string.Join("\r\n", model.UnsupportedLinkMlFeatures.Select(x => "• " + x));
                if (MessageBox.Show("This LinkML file uses features that are not fully represented in EA:\r\n\r\n" +
                        features + "\r\n\r\nSupported model content can still be imported. Continue?",
                        "LinkML import limitations", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            int domainCount = model.Classes.SelectMany(x => x.DiagramDomains)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            string diagramSummary = domainCount > 0
                ? $", {domainCount} structured domain diagrams"
                : ", one smart diagram";
            var existingPackage = EAJsonModelImporter.EaModelWriter.FindExistingPackage(target, model.Name);
            using var options = new EAJsonModelImporter.ImportOptionsDialog(model.Name, target.Name,
                model.Classes.Count, model.Enums.Count, diagramSummary, existingPackage?.Name);
            if (options.ShowDialog() != DialogResult.OK) return;

            bool updating = options.UpdateExisting && existingPackage is not null;
            var writeTarget = !updating && existingPackage?.PackageID == target.PackageID && target.ParentID > 0
                ? repository.GetPackageByID(target.ParentID)
                : target;
            var package = EAJsonModelImporter.EaModelWriter.Write(repository, writeTarget, model,
                updating ? existingPackage : null);
            MessageBox.Show(updating
                    ? $"Update complete.\nUpdated package: {package.Name}\nExisting diagram positions were preserved."
                    : $"Import complete.\nCreated package: {package.Name}",
                ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import failed:\n" + ex.Message, ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void AttachMetadata(EA.Repository repository)
    {
        var package = SelectedPackage(repository);
        if (package is null)
        {
            MessageBox.Show("Select the existing model package in the Browser first.", ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Choose the source LinkML YAML whose comments and annotations should be retained",
            Filter = "LinkML YAML|*.yaml;*.yml|All files|*.*"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var model = new EAJsonModelImporter.SchemaConverter().Convert(
                EAJsonModelImporter.InputLoader.Load(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName));
            model.SourceComments = EAJsonModelImporter.InputLoader.ExtractYamlComments(dialog.FileName);
            EAJsonModelImporter.EaModelWriter.WriteLinkMlMetadata(package, model);
            int commentLines = model.SourceComments.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            MessageBox.Show($"Metadata attached to '{package.Name}'.\n\n" +
                    $"{commentLines} comment lines and {model.LinkMlAnnotations.Count} annotation blocks " +
                    "will be restored during LinkML export.\n\n" +
                    "Classes, attributes, connectors and diagrams were not changed.",
                ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Metadata attachment failed:\n" + ex.Message, ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Export(EA.Repository repository)
    {
        var package = SelectedPackage(repository);
        if (package is null)
        {
            MessageBox.Show("Select a package in the Browser first.", ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var optionsDialog = new EA17LinkMLExporter.ExportOptionsDialog(package.Name);
        if (optionsDialog.ShowDialog() != DialogResult.OK) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder that will contain the exported model",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            string output = EA17LinkMLExporter.Exporter.Export(repository, package, dialog.SelectedPath,
                optionsDialog.Options);
            MessageBox.Show("Export complete:\n" + output, ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed:\n" + ex.Message, ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static EA.Package? SelectedPackage(EA.Repository repository)
    {
        try { return repository.GetTreeSelectedPackage(); }
        catch { return null; }
    }
}
