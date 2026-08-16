using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class PathsSetupControllerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_paths_ui_" + Guid.NewGuid().ToString("N"));

    public PathsSetupControllerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void EmptyBoxes_AreValidAutomaticDetection()
    {
        var state = PathsSetupController.Validate("", "", env: null);
        Assert.True(state.IsValid);
        Assert.Empty(state.Errors);
        Assert.Contains("FollowerForge default", state.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingXvaFolder_IsAnError()
    {
        var state = PathsSetupController.Validate(Path.Combine(_root, "nope"), null);
        Assert.False(state.IsValid);
        Assert.Contains(state.Errors, e => e.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameDataOutput_IsRejected()
    {
        var game = Path.Combine(_root, "Skyrim Special Edition");
        var data = Path.Combine(game, "Data");
        Directory.CreateDirectory(data);
        var env = new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Vortex,
            ManagerLabel = "Vortex",
            GameRootPath = game,
            GameDataPath = data,
            PluginDataPath = data,
            InstancePath = Path.Combine(_root, "vortex"),
            StagingPath = Path.Combine(_root, "vortex", "mods"),
            ProfilesPath = Path.Combine(_root, "vortex", "profiles"),
            RuntimePluginsTxtPath = Path.Combine(_root, "plugins.txt"),
        };

        var state = PathsSetupController.Validate(null, data, env);

        Assert.False(state.IsValid);
        Assert.Contains(state.Errors, e => e.Contains("Data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VortexModsFolder_IsAllowedAsAnExplicitOutput()
    {
        var mods = Path.Combine(_root, "vortex", "mods");
        Directory.CreateDirectory(mods);

        var state = PathsSetupController.Validate(null, mods);

        Assert.True(state.IsValid, string.Join("; ", state.Errors));
        Assert.Contains(mods, state.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
    }
}
