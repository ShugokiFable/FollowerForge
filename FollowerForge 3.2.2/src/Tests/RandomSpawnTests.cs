using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Starting somewhere different each game, modelled on the Enemy-to-Ally mods.
///
/// Their KWYK_Quest picks one of three spots and calls PlaceActorAtMe. We move the follower who
/// was already placed instead, so she keeps one persistent reference — dialogue conditions,
/// relationships and follower frameworks all track that reference.
/// </summary>
public sealed class RandomSpawnTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static SpawnLocation Place(string id, float x) => new()
    {
        Id = id,
        Name = id,
        Kind = LocationKind.Interior,
        CellFormKey = "01A270:Skyrim.esm",
        RequiredPlugin = "Skyrim.esm",
        X = x,
        Y = 0,
        Z = 0,
    };

    private static FollowerProfile Profile(params string[] alternates) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec
        {
            Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()),
            AlternateLocationIds = alternates,
        },
    };

    /// <summary>Placed objects live inside CELL records, so they have to be enumerated.</summary>
    private static List<IPlacedObjectGetter> Markers(FollowerCompiler.CompileResult result) =>
        result.Mod.EnumerateMajorRecords<IPlacedObjectGetter>().ToList();

    private static FollowerCompiler.CompileResult Compile(params SpawnLocation[] spots) =>
        new FollowerCompiler(Log).Compile(
            Profile(spots.Select(s => s.Id).ToArray()), location: null,
            alternateSpawns: spots.Select(s => (s, (ICellGetter?)null)).ToList());

    [Fact]
    public void NoAlternates_NoQuestAndNoMarkers()
    {
        var result = new FollowerCompiler(Log).Compile(Profile(), location: null);

        Assert.Null(result.RandomSpawn);
        Assert.Empty(result.Mod.Quests);
        Assert.Empty(Markers(result));
    }

    [Fact]
    public void OneMarkerPerPlace_AndAQuestThatPointsAtThem()
    {
        var result = Compile(Place("a", 10), Place("b", 20), Place("c", 30));

        Assert.Equal(3, result.RandomSpawn!.Markers.Count);
        var quest = Assert.Single(result.Mod.Quests);
        var script = Assert.Single(quest.VirtualMachineAdapter!.Scripts);
        Assert.Equal(RandomSpawnCompiler.ScriptName, script.Name);

        var objects = script.Properties.OfType<ScriptObjectProperty>()
            .ToDictionary(p => p.Name, p => p.Object.FormKey);
        Assert.Equal(result.PlacedFormKey, objects["Follower"]);
        foreach (var (marker, i) in result.RandomSpawn.Markers.Select((m, i) => (m, i)))
            Assert.Equal(marker, objects[$"Spot{i + 1}"]);
    }

    [Fact]
    public void SheIsMovedRatherThanRespawned()
    {
        // The script targets her own placed reference. Spawning a copy would give her a second
        // identity that nothing else in the plugin knows about.
        var result = Compile(Place("a", 10));

        var follower = Assert.Single(result.Mod.Quests)
            .VirtualMachineAdapter!.Scripts.Single()
            .Properties.OfType<ScriptObjectProperty>().Single(p => p.Name == "Follower");
        Assert.Equal(result.PlacedFormKey, follower.Object.FormKey);
    }

    [Fact]
    public void MarkersUseTheVanillaXMarker()
    {
        var result = Compile(Place("a", 10), Place("b", 20));

        Assert.All(Markers(result),
            o => Assert.Equal(VanillaForms.XMarker, o.Base.FormKey));
    }

    [Fact]
    public void MarkersArePersistent_SoThePropertyResolvesBeforeTheCellLoads()
    {
        var result = Compile(Place("a", 10));

        const int persistent = 0x00000400;
        Assert.All(Markers(result),
            o => Assert.True(((int)o.SkyrimMajorRecordFlags & persistent) != 0));
    }

    [Fact]
    public void MoreThanFourPlaces_AreCappedAtWhatTheScriptHolds()
    {
        var result = Compile(Place("a", 1), Place("b", 2), Place("c", 3), Place("d", 4), Place("e", 5));

        Assert.Equal(RandomSpawnCompiler.MaxSpots, result.RandomSpawn!.Markers.Count);
    }

    [Fact]
    public void TheCompiledScriptIsBundledWithTheApp()
    {
        var names = typeof(FollowerForge.BuildPipeline.FollowerBuilder).Assembly.GetManifestResourceNames();
        Assert.Contains(names, n => n.EndsWith("FF_RandomSpawn.pex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.EndsWith("FF_RandomSpawn.psc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WeShipOurOwnScript_NotTheOneEveryE2AModShares()
    {
        // Every Enemy-to-Ally mod ships an identical KWYK_Quest.pex, which is why they all
        // report conflicts with each other. Adding another copy would join that pile and would
        // mean redistributing someone else's script.
        Assert.StartsWith("FF_", RandomSpawnCompiler.ScriptName);
        Assert.DoesNotContain("KWYK", RandomSpawnCompiler.ScriptName, StringComparison.OrdinalIgnoreCase);
    }
}
