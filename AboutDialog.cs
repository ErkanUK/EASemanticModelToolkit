using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace EASemanticModelToolkit;

internal sealed class AboutDialog : Form
{
    private const string RepositoryUrl = "https://github.com/ErkanUK/EASemanticModelToolkit";

    public AboutDialog()
    {
        Text = "About Semantic Model Toolkit";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 220);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "Semantic Model Toolkit",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 20)
        };
        var description = new Label
        {
            Text = "Imports JSON and YAML models into Enterprise Architect and exports selected packages to LinkML, JSON Schema, Markdown, draw.io, PlantUML, SVG, and OWL.",
            AutoSize = false,
            Location = new Point(22, 53),
            Size = new Size(454, 55)
        };
        var version = new Label
        {
            Text = "Build version: " + BuildVersion(),
            AutoSize = true,
            Location = new Point(22, 117)
        };
        var repositoryLabel = new Label
        {
            Text = "Source repository:",
            AutoSize = true,
            Location = new Point(22, 146)
        };
        var repositoryLink = new LinkLabel
        {
            Text = RepositoryUrl,
            AutoSize = true,
            Location = new Point(126, 146)
        };
        repositoryLink.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });

        var close = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 28),
            Location = new Point(396, 180)
        };
        AcceptButton = close;
        CancelButton = close;
        Controls.AddRange([title, description, version, repositoryLabel, repositoryLink, close]);
    }

    internal static string BuildVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";
        return informational.Length > 0
            ? informational
            : assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
