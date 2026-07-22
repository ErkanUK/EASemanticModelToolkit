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
            MessageBox.Show("Imports JSON, JSON Schema, and YAML as an editable UML class model, with optional OWL/RDF and Turtle ontology exports. LinkML ea_domains annotations generate structured overview and domain diagrams.",
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
            int domainCount = model.Classes.SelectMany(x => x.DiagramDomains).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            string diagramSummary = domainCount > 0 ? $", {domainCount} structured domain diagrams" : ", one smart diagram";
            using var options = new ImportOptionsDialog(model.Name, target.Name, model.Classes.Count,
                model.Enums.Count, diagramSummary);
            if (options.ShowDialog() != DialogResult.OK) return;

            var exported = new List<string>();
            string directory = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
            string baseName = Path.GetFileNameWithoutExtension(dialog.FileName);
            if (options.ExportOwl)
            {
                string path = Path.Combine(directory, baseName + ".owl");
                File.WriteAllText(path, OwlSerializer.Serialize(model, OwlSerialization.RdfXml),
                    new System.Text.UTF8Encoding(false));
                exported.Add(path);
            }
            if (options.ExportTurtle)
            {
                string path = Path.Combine(directory, baseName + ".ttl");
                File.WriteAllText(path, OwlSerializer.Serialize(model, OwlSerialization.Turtle),
                    new System.Text.UTF8Encoding(false));
                exported.Add(path);
            }
            var package = EaModelWriter.Write(repository, target, model);
            string exportSummary = exported.Count == 0 ? "" : "\n\nOntology files:\n" + string.Join("\n", exported);
            MessageBox.Show($"Import complete.\nCreated package: {package.Name}{exportSummary}", "EA JSON/YAML Model Importer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import or ontology export failed:\n" + ex.Message, "EA JSON/YAML Model Importer",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static EA.Package? SelectedPackage(EA.Repository repository)
    {
        try { return repository.GetTreeSelectedPackage(); } catch { return null; }
    }
}
