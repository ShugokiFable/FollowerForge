using FollowerForge.AssetIndex;
using FollowerForge.ModManagers;

namespace FollowerForge.Tests;

public sealed class XvaSynthLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_xva_" + Guid.NewGuid().ToString("N"));

    public XvaSynthLocatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Resolve_HonoursAnExplicitOverrideEvenWhenIncomplete()
    {
        var folder = Path.Combine(_root, "steam", "xVASynth");
        Directory.CreateDirectory(folder);

        var resolved = XvaSynthLocator.Resolve(folder);

        Assert.Equal(Path.GetFullPath(folder), resolved);
        Assert.False(XvaSynthLocator.HasModels(resolved));
        Assert.False(XvaSynthLocator.LooksLikeRoot(resolved));
    }

    [Fact]
    public void HasModels_RequiresTheSkyrimModelFolder()
    {
        var folder = Path.Combine(_root, "xVASynth");
        var models = Path.Combine(folder, "resources", "app", "models", "skyrim");
        Directory.CreateDirectory(models);
        File.WriteAllText(Path.Combine(models, "sk_femaleeventoned.json"), "{}");

        Assert.True(XvaSynthLocator.HasModels(folder));
        Assert.True(XvaSynthLocator.LooksLikeRoot(folder));
        Assert.Equal(Path.GetFullPath(folder), XvaSynthLocator.Resolve(folder));
    }

    [Fact]
    public void Resolve_UsesSavedSettingsWhenNoOverrideIsPassed()
    {
        var folder = Path.Combine(_root, "saved-xva");
        Directory.CreateDirectory(Path.Combine(folder, "resources", "app", "models", "skyrim"));
        AppUserSettings.Save(new AppUserSelection(folder, null), _root);

        var resolved = XvaSynthLocator.Resolve(settingsDirectory: _root);

        Assert.Equal(Path.GetFullPath(folder), resolved);
    }

    [Fact]
    public void VoiceModelCatalog_UsesTheResolvedOverrideRoot()
    {
        var folder = Path.Combine(_root, "catalog-xva");
        Directory.CreateDirectory(folder);
        var catalog = new VoiceModelCatalog(folder);
        Assert.Equal(Path.GetFullPath(folder), catalog.Root);
        Assert.False(catalog.Installed);
    }
}
