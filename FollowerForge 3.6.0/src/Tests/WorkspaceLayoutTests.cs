using System.Xml.Linq;

namespace FollowerForge.Tests;

public sealed class WorkspaceLayoutTests
{
    private static readonly XDocument Document = XDocument.Load(SourcePath());
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Shell_supports_the_minimum_window_and_uses_semantic_resources()
    {
        var root = Assert.IsType<XElement>(Document.Root);
        Assert.Equal("1040", root.Attribute("MinWidth")?.Value);
        Assert.Equal("700", root.Attribute("MinHeight")?.Value);
        Assert.Contains("DynamicResource WindowBrush", root.Attribute("Background")?.Value);
        Assert.Contains(Document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Value.Contains("DynamicResource SurfaceBrush", StringComparison.Ordinal)));
        Assert.Contains(Document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Value.Contains("DynamicResource AccentBrush", StringComparison.Ordinal)));
    }

    [Fact]
    public void Shell_has_studio_seven_categories_dossier_palette_and_expert_deck()
    {
        var names = Document.Descendants()
            .Select(element => element.Attribute(Xaml + "Name")?.Value)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
                 {
                     "StudioPage", "Page0", "Page1", "Page2", "Page3", "Page4", "Page5", "Page6",
                     "DossierPanel", "DossierDrawer", "CommandPaletteOverlay", "DeckOverlay", "DeckGrid",
                     "TopFollowerName", "AutosaveState", "EnvironmentState", "ExperienceButton",
                 })
            Assert.Contains(required, names);

        for (var index = 0; index < 7; index++)
            Assert.Contains($"NavStatus{index}", names);
    }

    [Fact]
    public void Command_palette_wires_manager_paths_and_build()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var code = File.ReadAllText(Path.Combine(src, "Ui", "WizardWindow.axaml.cs"));
        foreach (var title in new[] { "Build follower", "Paths…", "MO2 setup…", "Switch manager" })
            Assert.Contains($"new(\"{title}\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Categories_do_not_contain_nested_tab_navigation()
    {
        Assert.DoesNotContain(Document.Descendants(), element => element.Name.LocalName == "TabControl");
        Assert.DoesNotContain(Document.Descendants(), element => element.Name.LocalName == "TabItem");
    }

    [Fact]
    public void Controls_and_deck_rows_have_accessible_target_heights()
    {
        var setters = Document.Descendants().Where(element => element.Name.LocalName == "Setter").ToList();
        Assert.Contains(setters, setter =>
            setter.Attribute("Property")?.Value == "MinHeight"
            && double.TryParse(setter.Attribute("Value")?.Value, out var value)
            && value >= 36);
        Assert.Contains(setters, setter =>
            setter.Attribute("Property")?.Value == "MinHeight"
            && setter.Parent?.Attribute("Selector")?.Value?.Contains("DataGridRow", StringComparison.Ordinal) == true
            && double.TryParse(setter.Attribute("Value")?.Value, out var value)
            && value >= 32);

        Assert.DoesNotContain(Document.Descendants().Attributes("MinWidth"), attribute =>
            double.TryParse(attribute.Value, out var value) && value > 1040);
    }

    private static string SourcePath()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(src, "Ui", "WizardWindow.axaml");
    }
}
