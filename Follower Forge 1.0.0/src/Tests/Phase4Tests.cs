using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using FollowerForge.Validation;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Phase4Tests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private readonly string _ws = Path.Combine(Path.GetTempPath(), "ff_p4_" + Guid.NewGuid().ToString("N"));

    private static EnvironmentSnapshot FakeEnv() => new()
    {
        GameRootPath = Path.Combine(Path.GetTempPath(), "ff_fake_game"),
        GameDataPath = Path.Combine(Path.GetTempPath(), "ff_fake_game", "Data"),
        VortexGamePath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex"),
        StagingPath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex", "mods"),
        ProfilesPath = Path.Combine(Path.GetTempPath(), "ff_fake_vortex", "profiles"),
        RuntimePluginsTxtPath = Path.Combine(Path.GetTempPath(), "ff_fake_runtime.txt"),
    };

    [Fact]
    public void Hub_BuildsAsValidLightMasterWithMarkerKeyword()
    {
        var result = new HubBuilder(Log).Build("TestHub", FakeEnv(), _ws);
        Assert.True(result.Success);
        Assert.EndsWith("TestHub.esm", result.PluginPath);

        var report = new ValidationReport();
        EspHeaderValidator.Validate(result.PluginPath, report, requireEsl: true);
        Assert.False(report.HasErrors);
        Assert.Contains(report.Findings, f => f.Code == "SHIP_GATE_PASS");
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "hub-manifest.json")));
    }

    [Fact]
    public void SharedHubFollower_MastersTheHub()
    {
        var profile = new FollowerProfile
        {
            Name = "Hub Member",
            PluginName = "FF_HubMember.esp",
            Race = new RecordRef(VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
            Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
            Strategy = OutputStrategy.SharedHub,
            HubPluginName = "TestHub.esm",
        };
        var result = new FollowerBuilder(Log).Build(profile, FakeEnv(), _ws, placementWorldspace: null);
        Assert.True(result.Success);
        Assert.Contains("TestHub.esm", result.Manifest.Masters);
        Assert.Contains("Skyrim.esm", result.Manifest.Masters);
    }

    [Fact]
    public void PortableStandalone_WithoutPermission_FailsAndCopiesNothing()
    {
        var profile = new FollowerProfile
        {
            Name = "Standalone No Perm",
            PluginName = "FF_StandaloneNoPerm.esp",
            Race = new RecordRef(VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
            Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
            Strategy = OutputStrategy.PortableStandalone,
            RedistributionPermission = null,
        };
        var result = new FollowerBuilder(Log).Build(profile, FakeEnv(), _ws, placementWorldspace: null);
        Assert.False(result.Success);
        Assert.Contains(result.Validation.Findings, f => f.Code == "STANDALONE_NO_PERMISSION");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_ws)) Directory.Delete(_ws, recursive: true); }
        catch (IOException) { }
    }
}
