using System.Text.Json;
using FollowerForge.ModManagers;

namespace FollowerForge.Tests;

public sealed class AppUserSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_app_settings_" + Guid.NewGuid().ToString("N"));

    public AppUserSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsManualPaths()
    {
        var xva = Path.Combine(_root, "xVASynth");
        var output = Path.Combine(_root, "out");
        Directory.CreateDirectory(xva);
        Directory.CreateDirectory(output);

        AppUserSettings.Save(new AppUserSelection(xva, output), _root);
        var loaded = AppUserSettings.Load(_root);

        Assert.Equal(Path.GetFullPath(xva), loaded.XvaSynthRoot);
        Assert.Equal(Path.GetFullPath(output), loaded.WorkspaceRoot);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "app-settings.json")));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void Load_MissingFileMeansAutomatic()
    {
        var loaded = AppUserSettings.Load(_root);
        Assert.Null(loaded.XvaSynthRoot);
        Assert.Null(loaded.WorkspaceRoot);
    }

    [Fact]
    public void Clear_RemovesOnlyTheAppSettingsFile()
    {
        File.WriteAllText(Path.Combine(_root, "keep-me.txt"), "keep");
        AppUserSettings.Save(new AppUserSelection(@"C:\xVASynth", @"D:\mods"), _root);

        AppUserSettings.Clear(_root);

        Assert.False(File.Exists(Path.Combine(_root, "app-settings.json")));
        Assert.True(File.Exists(Path.Combine(_root, "keep-me.txt")));
    }
}
