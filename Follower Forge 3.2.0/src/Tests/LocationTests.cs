using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

public sealed class LocationTests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private readonly string _ws = Path.Combine(Path.GetTempPath(), "ff_loc_" + Guid.NewGuid().ToString("N"));

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

    private static SpawnLocation BanneredMare() => new()
    {
        Id = "the-bannered-mare-01605e",
        Name = "The Bannered Mare",
        Area = "Whiterun",
        Kind = LocationKind.Interior,
        CellFormKey = "01605E:Skyrim.esm",
        RequiredPlugin = "Skyrim.esm",
        X = 170, Y = -535, Z = 69,
        Popularity = 7,
    };

    /// <summary>
    /// Interior cells sit in blocks derived from their own FormID. These four expectations were
    /// read out of real Skyrim.esm cells, so a regression here means placements land in the
    /// wrong group and the game would not find the follower.
    /// </summary>
    [Theory]
    [InlineData(0x013870u, 4, 8)]   // RoriksteadFrostfruitInn
    [InlineData(0x015239u, 5, 8)]   // Morvunskar01
    [InlineData(0x0152AAu, 8, 9)]   // Ustengrav02
    [InlineData(0x01605Eu, 6, 0)]   // WhiterunBanneredMare
    public void InteriorBlocks_MatchRealSkyrimCells(uint formId, int block, int subBlock)
    {
        var (b, s) = CellPlacer.InteriorBlocks(formId);
        Assert.Equal(block, b);
        Assert.Equal(subBlock, s);
    }

    [Fact]
    public void PlaceInInterior_CreatesBlockStructureWithSinglePersistentRef()
    {
        var mod = new SkyrimMod(ModKey.FromFileName("FF_Loc.esp"), SkyrimRelease.SkyrimSE) { IsSmallMaster = true };
        var npc = mod.Npcs.AddNew("FF_Loc_NPC");
        var placed = new PlacedNpc(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE)
        {
            Base = new FormLinkNullable<INpcGetter>(npc.FormKey),
        };

        CellPlacer.Place(mod, BanneredMare(), placed);

        var block = Assert.Single(mod.Cells.Records);
        Assert.Equal(6, block.BlockNumber);
        var sub = Assert.Single(block.SubBlocks);
        Assert.Equal(0, sub.BlockNumber);
        var cell = Assert.Single(sub.Cells);
        Assert.Equal(FormKey.Factory("01605E:Skyrim.esm"), cell.FormKey);
        // Exactly our reference — the room's own contents must never be copied in.
        Assert.Single(cell.Persistent);
        Assert.Empty(cell.Temporary);
    }

    [Fact]
    public void Compile_UsesLocationCoordinates()
    {
        var profile = new FollowerProfile
        {
            Name = "Loc Test",
            PluginName = "FF_LocTest.esp",
            Race = new RecordRef(VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
            Placement = new PlacementSpec { LocationId = "the-bannered-mare-01605e" },
        };

        var result = new FollowerCompiler(Log).Compile(profile, BanneredMare());

        var placed = result.Mod.Cells.Records
            .SelectMany(b => b.SubBlocks)
            .SelectMany(s => s.Cells)
            .SelectMany(c => c.Persistent)
            .OfType<IPlacedNpcGetter>()
            .Single();
        Assert.Equal(170, placed.Placement!.Position.X);
        Assert.Equal(-535, placed.Placement.Position.Y);
        Assert.Equal(result.Mod.Npcs.First().FormKey, placed.Base.FormKey);
    }

    [Fact]
    public void Build_UnknownLocationId_FailsLoudlyInsteadOfSilentlyMovingTheFollower()
    {
        var profile = new FollowerProfile
        {
            Name = "Bad Location",
            PluginName = "FF_BadLocation.esp",
            Race = new RecordRef(VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
            Placement = new PlacementSpec { LocationId = "no-such-place-999999" },
        };

        var result = new FollowerBuilder(Log).Build(profile, FakeEnv(), _ws);

        Assert.False(result.Success);
        Assert.Contains(result.Validation.Findings,
            f => f.Code is "LOCATION_UNKNOWN" or "LOCATION_NO_LIBRARY");
    }

    [Fact]
    public void Library_RoundTripsAndSearches()
    {
        var path = Path.Combine(_ws, "locations.json");
        Directory.CreateDirectory(_ws);
        var library = new LocationLibrary
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            ScannedPlugins = 2,
            Locations = [BanneredMare(), BanneredMare() with { Id = "sleeping-giant-0133c6", Name = "Sleeping Giant Inn", Area = "Riverwood" }],
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(library, ProfileIo.Options));

        var loaded = LocationLibraryBuilder.Load(path);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Locations.Count);

        Assert.Single(LocationLibraryBuilder.Search(loaded, "sleeping"));
        Assert.Single(LocationLibraryBuilder.Search(loaded, "whiterun"));   // matches by Area
        Assert.Equal(2, LocationLibraryBuilder.Search(loaded, null).Count);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_ws)) Directory.Delete(_ws, recursive: true); }
        catch (IOException) { }
    }
}
