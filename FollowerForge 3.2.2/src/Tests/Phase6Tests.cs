using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Phase6Tests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private readonly string _ws = Path.Combine(Path.GetTempPath(), "ff_p6_" + Guid.NewGuid().ToString("N"));

    private static EnvironmentSnapshot FakeEnv() => new()
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

    private static FollowerProfile Profile(string name, string plugin) => new()
    {
        Name = name,
        PluginName = plugin,
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
    };

    [Fact]
    public void Batch_BuildsAllProfiles()
    {
        var profiles = new[]
        {
            Profile("Batch One", "FF_BatchOne.esp"),
            Profile("Batch Two", "FF_BatchTwo.esp"),
        };
        var result = new BatchBuilder(Log).Build(profiles, FakeEnv(), _ws);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void LoadProfiles_FromDirectory_ReadsAllJson()
    {
        var dir = Path.Combine(_ws, "profiles");
        Directory.CreateDirectory(dir);
        ProfileIo.Save(Path.Combine(dir, "a.json"), Profile("A", "FF_A.esp"));
        ProfileIo.Save(Path.Combine(dir, "b.json"), Profile("B", "FF_B.esp"));
        var loaded = BatchBuilder.LoadProfiles(dir);
        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public void DeterminismVerifier_ReportsByteIdenticalRebuild()
    {
        var det = new DeterminismVerifier(Log).Verify(Profile("Det", "FF_Det.esp"), FakeEnv(), location: null);
        Assert.True(det.Identical);
        Assert.Equal(det.HashA, det.HashB);
    }

    [Fact]
    public void NormalFollower_HasNoDuplicateEditorIds()
    {
        var result = new FollowerBuilder(Log).Build(Profile("Clean", "FF_Clean.esp"), FakeEnv(), _ws, null);
        Assert.DoesNotContain(result.Validation.Findings, f => f.Code == "DUP_EDITORID");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_ws)) Directory.Delete(_ws, recursive: true); }
        catch (IOException) { }
    }
}
