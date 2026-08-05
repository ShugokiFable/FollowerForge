using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using FollowerForge.Validation;
using Serilog;

namespace FollowerForge.Tests;

public sealed class FollowerBuilderTests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ff_bld_" + Guid.NewGuid().ToString("N"));

    // Environment whose protected roots are elsewhere, so the temp workspace stays writable.
    private EnvironmentSnapshot FakeEnv() => new()
    {
        Manager = ModManagerKind.Vortex,
        ManagerLabel = "Vortex",
        GameRootPath = Path.Combine(Path.GetTempPath(), "ff_fake_game"),
        GameDataPath = Path.Combine(Path.GetTempPath(), "ff_fake_game", "Data"),
        PluginDataPath = Path.Combine(Path.GetTempPath(), "ff_fake_game", "Data"),
        InstancePath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex"),
        StagingPath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex", "mods"),
        ProfilesPath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex", "profiles"),
        RuntimePluginsTxtPath = Path.Combine(Path.GetTempPath(), "ff_fake_runtime.txt"),
    };

    private static FollowerProfile Profile() => new()
    {
        Name = "Builder Test",
        PluginName = "FF_BuilderTest.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Outfit = new RecordRef(VanillaForms.FarmClothesOutfit.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
    };

    [Fact]
    public void Build_PublishesAllRequiredOutputs()
    {
        var result = new FollowerBuilder(Log).Build(Profile(), FakeEnv(), _workspace, location: null);

        Assert.True(result.Success);
        Assert.False(result.Validation.HasErrors);
        foreach (var name in new[]
        {
            "FF_BuilderTest.esp", "manifest.json", "source-assets.json", "dependency-report.json",
            "rebuild-profile.json", "build-report.html", "credits.md", "SHARE-CHECKLIST.txt",
        })
            Assert.True(File.Exists(Path.Combine(result.OutputDirectory, name)), $"missing {name}");
    }

    [Fact]
    public void Build_PluginPassesShipGateHeaderRules()
    {
        var result = new FollowerBuilder(Log).Build(Profile(), FakeEnv(), _workspace, location: null);
        var report = new ValidationReport();
        EspHeaderValidator.Validate(result.PluginPath, report, requireEsl: true);
        Assert.False(report.HasErrors, string.Join("; ", report.Findings.Select(f => f.Code + ":" + f.Message)));
        Assert.Contains(report.Findings, f => f.Code == "SHIP_GATE_PASS");
    }

    [Fact]
    public void Build_RefusesToWriteInsideProtectedRoots()
    {
        var env = FakeEnv();
        // Point the workspace inside the protected staging root.
        var badWorkspace = Path.Combine(env.StagingPath, "sneaky");
        Assert.Throws<UnauthorizedAccessException>(() =>
            new FollowerBuilder(Log).Build(Profile(), env, badWorkspace, location: null));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch (IOException) { }
    }
}
