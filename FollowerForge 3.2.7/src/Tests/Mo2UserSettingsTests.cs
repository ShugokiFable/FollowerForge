using System.Text.Json;
using FollowerForge.ModManagers;

namespace FollowerForge.Tests;

public sealed class Mo2UserSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_mo2_settings_" + Guid.NewGuid().ToString("N"));

    public Mo2UserSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSchemaOneSelection()
    {
        var selection = new Mo2UserSelection(Path.Combine(_root, "MO2"), "Main");

        Mo2UserSettings.Save(selection, _root);
        var loaded = Mo2UserSettings.Load(_root);

        Assert.Equal(selection, loaded);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "mo2-settings.json")));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(selection.InstanceRoot, json.RootElement.GetProperty("instanceRoot").GetString());
        Assert.Equal(selection.ProfileName, json.RootElement.GetProperty("profileName").GetString());
    }

    [Fact]
    public void Save_ReplacesExistingFileWithoutLeavingTemporaryFiles()
    {
        Mo2UserSettings.Save(new Mo2UserSelection("C:\\First", "Old"), _root);

        var expected = new Mo2UserSelection("D:\\Second", "New");
        Mo2UserSettings.Save(expected, _root);

        Assert.Equal(expected, Mo2UserSettings.Load(_root));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Load_CorruptJsonReturnsNullAndWarns()
    {
        File.WriteAllText(Path.Combine(_root, "mo2-settings.json"), "{ definitely not json");
        var warnings = new List<string>();

        var loaded = Mo2UserSettings.Load(_root, warnings.Add);

        Assert.Null(loaded);
        Assert.Single(warnings);
        Assert.Contains("could not be read", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnknownSchemaReturnsNullAndWarns()
    {
        File.WriteAllText(Path.Combine(_root, "mo2-settings.json"), """
            { "schemaVersion": 99, "instanceRoot": "C:\\MO2", "profileName": "Main" }
            """);
        var warnings = new List<string>();

        var loaded = Mo2UserSettings.Load(_root, warnings.Add);

        Assert.Null(loaded);
        Assert.Single(warnings);
        Assert.Contains("schema", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clear_RemovesOnlyFollowerForgeMo2SettingsFile()
    {
        var unrelated = Path.Combine(_root, "keep-me.txt");
        File.WriteAllText(unrelated, "keep");
        Mo2UserSettings.Save(new Mo2UserSelection("C:\\MO2", "Main"), _root);

        Mo2UserSettings.Clear(_root);

        Assert.False(File.Exists(Path.Combine(_root, "mo2-settings.json")));
        Assert.True(File.Exists(unrelated));
    }
}
