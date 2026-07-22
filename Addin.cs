using System.Runtime.InteropServices;
using System.Text;
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
    private const string ExportItem = "Export selected package...";
    private const string AboutItem = "About Semantic Model Toolkit";

    public string EA_Connect(EA.Repository repository) => "EASemanticModelToolkit";
    public void EA_Disconnect() { }

    public object EA_GetMenuItems(EA.Repository repository, string location, string menuName) => menuName switch
    {
        "" => Menu,
        Menu => new[] { ImportItem, ExportItem, AboutItem },
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
            case ExportItem:
                Export(repository);
                break;
            case AboutItem:
                MessageBox.Show(
                    "Imports JSON and YAML models into Enterprise Architect and exports selected packages to LinkML, JSON Schema, Markdown, diagrams, and OWL.",
                    ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            int domainCount = model.Classes.SelectMany(x => x.DiagramDomains)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            string diagramSummary = domainCount > 0
                ? $", {domainCount} structured domain diagrams"
                : ", one smart diagram";
            using var options = new EAJsonModelImporter.ImportOptionsDialog(model.Name, target.Name,
                model.Classes.Count, model.Enums.Count, diagramSummary);
            if (options.ShowDialog() != DialogResult.OK) return;

            var exported = new List<string>();
            string directory = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
            string baseName = Path.GetFileNameWithoutExtension(dialog.FileName);
            if (options.ExportOwl)
            {
                string path = Path.Combine(directory, baseName + ".owl");
                File.WriteAllText(path, EAJsonModelImporter.OwlSerializer.Serialize(model,
                    EAJsonModelImporter.OwlSerialization.RdfXml), new UTF8Encoding(false));
                exported.Add(path);
            }
            if (options.ExportTurtle)
            {
                string path = Path.Combine(directory, baseName + ".ttl");
                File.WriteAllText(path, EAJsonModelImporter.OwlSerializer.Serialize(model,
                    EAJsonModelImporter.OwlSerialization.Turtle), new UTF8Encoding(false));
                exported.Add(path);
            }

            var package = EAJsonModelImporter.EaModelWriter.Write(repository, target, model);
            string exportSummary = exported.Count == 0 ? "" : "\n\nOntology files:\n" + string.Join("\n", exported);
            MessageBox.Show($"Import complete.\nCreated package: {package.Name}{exportSummary}", ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import failed:\n" + ex.Message, ProductName,
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

