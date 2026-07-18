using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Tests;

public sealed class FollowerCompilerTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile SampleProfile() => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        Female = true,
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Outfit = new RecordRef(VanillaForms.FarmClothesOutfit.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
    };

    [Fact]
    public void Compile_ProducesLightPluginWithFollowerRecords()
    {
        var result = new FollowerCompiler(Log).Compile(SampleProfile(), placementWorldspace: null);

        Assert.True(result.Mod.IsSmallMaster);
        Assert.Single(result.Mod.Npcs);
        Assert.Single(result.Mod.Relationships);
        Assert.Single(result.Mod.Worldspaces);

        var npc = result.Mod.Npcs.First();
        // FormID must be in the ESL light range 0x800–0xFFF.
        Assert.InRange(npc.FormKey.ID, 0x800u, 0xFFFu);
        Assert.InRange(result.PlacedFormKey.ID, 0x800u, 0xFFFu);

        // Follower factions with correct ranks.
        Assert.Contains(npc.Factions, f => f.Faction.FormKey == VanillaForms.PotentialFollowerFaction && f.Rank == 0);
        Assert.Contains(npc.Factions, f => f.Faction.FormKey == VanillaForms.CurrentFollowerFaction && f.Rank == -1);

        // Relationship: parent = follower, child = player, Ally.
        var rel = result.Mod.Relationships.First();
        Assert.Equal(npc.FormKey, rel.Parent.FormKey);
        Assert.Equal(VanillaForms.PlayerNpc, rel.Child.FormKey);

        // Placed ACHR lives in the persistent cell of the worldspace override.
        var ws = result.Mod.Worldspaces.First();
        Assert.NotNull(ws.TopCell);
        Assert.Single(ws.TopCell!.Persistent);
    }

    [Fact]
    public void Compile_WriteReopen_RoundTripsAsValidEspfe()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_rt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = new FollowerCompiler(Log).Compile(SampleProfile(), placementWorldspace: null);
            var writer = new PluginWriter(Log);
            var path = Path.Combine(dir, result.Mod.ModKey.FileName);
            writer.Write(result.Mod, path);

            Assert.True(File.Exists(path));
            var reopened = writer.Reopen(path);
            Assert.Equal(1, reopened.NpcCount);
            Assert.True(reopened.IsLight);
            // SSE header version 1.71 (skyrim-ship-gate requirement).
            Assert.Equal(1.71f, reopened.HeaderVersion, 3);
            Assert.Contains("Skyrim.esm", reopened.Masters);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Compile_IsDeterministic_StableFormIds()
    {
        var a = new FollowerCompiler(Log).Compile(SampleProfile(), null);
        var b = new FollowerCompiler(Log).Compile(SampleProfile(), null);
        Assert.Equal(a.NpcFormKey, b.NpcFormKey);
        Assert.Equal(a.PlacedFormKey, b.PlacedFormKey);
        Assert.Equal(a.NpcEditorId, b.NpcEditorId);
    }
}
