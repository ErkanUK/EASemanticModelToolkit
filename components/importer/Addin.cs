using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EAJsonModelImporter;

[ComVisible(true)]
[Guid("C9D3AA11-5139-4F2E-BA00-58AAE6B1DB06")]
[ProgId("EAJsonModelImporter.Addin")]
[ClassInterface(ClassInterfaceType.AutoDual)]
public sealed class Addin
{
    private const string Menu = "-&JSON/YAML Model Importer";
    private const string ImportItem = "Import into selected package...";
    private const string AboutItem = "About JSON/YAML Model Importer";

    public string EA_Connect(EA.Repository repository) => "EAJsonModelImporter";
    public void EA_Disconnect() { }
    public object EA_GetMenuItems(EA.Repository repository, string location, string menuName) => menuName switch
    {
        "" => Menu,
        Menu => new[] { ImportItem, AboutItem },
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
        if (itemName == AboutItem)
        {
            MessageBox.Show("Imports JSON, JSON Schema, and YAML as an editable UML class model. LinkML ea_domains annotations generate structured overview and domain diagrams.",
                "EA JSON/YAML Model Importer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (itemName != ImportItem) return;
        var target = SelectedPackage(repository);
        if (target is null)
        {
            MessageBox.Show("Select the target package in the Browser first.", "EA JSON/YAML Model Importer",
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
            var root = InputLoader.Load(dialog.FileName);
            var model = new SchemaConverter().Convert(root, Path.GetFileNameWithoutExtension(dialog.FileName));
            if (model.UnsupportedLinkMlFeatures.Count > 0)
            {
                string features = string.Join("\r\n", model.UnsupportedLinkMlFeatures.Select(x => "• " + x));
                if (MessageBox.Show("This LinkML file uses features that are not fully represented in EA:\r\n\r\n" +
                        features + "\r\n\r\nSupported model content can still be imported. Continue?",
                        "LinkML import limitations", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            int domainCount = model.Classes.SelectMany(x => x.DiagramDomains).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            string diagramSummary = domainCount > 0 ? $", {domainCount} structured domain diagrams" : ", one smart diagram";
            var existingPackage = EaModelWriter.FindExistingPackage(target, model.Name);
            using var options = new ImportOptionsDialog(model.Name, target.Name, model.Classes.Count,
                model.Enums.Count, diagramSummary, existingPackage?.Name);
            if (options.ShowDialog() != DialogResult.OK) return;

            bool updating = options.UpdateExisting && existingPackage is not null;
            var writeTarget = !updating && existingPackage?.PackageID == target.PackageID && target.ParentID > 0
                ? repository.GetPackageByID(target.ParentID)
                : target;
            var package = EaModelWriter.Write(repository, writeTarget, model, updating ? existingPackage : null);
            MessageBox.Show(updating
                    ? $"Update complete.\nUpdated package: {package.Name}\nExisting diagram positions were preserved."
                    : $"Import complete.\nCreated package: {package.Name}",
                "EA JSON/YAML Model Importer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import failed:\n" + ex.Message, "EA JSON/YAML Model Importer",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static EA.Package? SelectedPackage(EA.Repository repository)
    {
        try { return repository.GetTreeSelectedPackage(); } catch { return null; }
    }
}
