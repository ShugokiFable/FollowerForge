using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Vampirism, which needs no script at all. Read off Sybille Stentor: race=BretonRaceVampire
/// plus the Vampire and ActorTypeUndead keywords, and nothing else.
/// </summary>
public sealed class VampireTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private static readonly FormKey NordVampire = new(ModKey.FromFileName("Skyrim.esm"), 0x088794);

    private static FollowerProfile Profile(bool vampire) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        IsVampire = vampire,
    };

    [Theory]
    // The convention every vanilla playable race follows, and the one custom-race mods copy.
    [InlineData("NordRace", "NordRaceVampire")]
    [InlineData("BretonRace", "BretonRaceVampire")]
    [InlineData("ElderRace", "ElderRaceVampire")]
    [InlineData("SomeCustomRace", "SomeCustomRaceVampire")]
    public void VariantNameFollowsTheGamesOwnConvention(string race, string expected) =>
        Assert.Equal(expected, VampireRaces.VariantEditorId(race));

    [Theory]
    [InlineData("NordRaceVampire", true)]
    [InlineData("NordRace", false)]
    [InlineData(null, false)]
    public void AlreadyVampiricRacesAreRecognised(string? race, bool expected) =>
        Assert.Equal(expected, VampireRaces.IsVampireRace(race));

    [Fact]
    public void NotAVampire_LeavesTheRecordCompletelyAlone()
    {
        var npc = new FollowerCompiler(Log).Compile(Profile(vampire: false), location: null)
            .Mod.Npcs.First();

        Assert.Equal(VanillaForms.NordRace, npc.Race.FormKey);
        Assert.DoesNotContain(npc.Keywords ?? [], k => k.FormKey == VanillaForms.VampireKeyword);
    }

    [Fact]
    public void Vampire_SwapsTheRaceAndAddsExactlyTheTwoKeywords()
    {
        var npc = new FollowerCompiler(Log)
            .Compile(Profile(vampire: true), location: null, vampireRace: NordVampire)
            .Mod.Npcs.First();

        Assert.Equal(NordVampire, npc.Race.FormKey);
        Assert.Contains(npc.Keywords!, k => k.FormKey == VanillaForms.VampireKeyword);
        Assert.Contains(npc.Keywords!, k => k.FormKey == VanillaForms.ActorTypeUndeadKeyword);
    }

    [Fact]
    public void Vampire_AddsNoSpellsOrFactionsOfItsOwn()
    {
        // Sybille Stentor has no vampire abilities and no vampire faction on her record, so
        // neither should ours — inventing them is how a follower ends up subtly wrong.
        var plain = new FollowerCompiler(Log).Compile(Profile(vampire: false), location: null)
            .Mod.Npcs.First();
        var vampire = new FollowerCompiler(Log)
            .Compile(Profile(vampire: true), location: null, vampireRace: NordVampire)
            .Mod.Npcs.First();

        Assert.Equal(plain.ActorEffect?.Count ?? 0, vampire.ActorEffect?.Count ?? 0);
        Assert.Equal(plain.Factions.Count, vampire.Factions.Count);
    }

    [Fact]
    public void VampireRequestedButUnresolved_LeavesHerMortalForTheBuilderToReject()
    {
        // The compiler cannot look races up; passing null must not silently half-apply vampirism.
        var npc = new FollowerCompiler(Log).Compile(Profile(vampire: true), location: null)
            .Mod.Npcs.First();

        Assert.Equal(VanillaForms.NordRace, npc.Race.FormKey);
        Assert.DoesNotContain(npc.Keywords ?? [], k => k.FormKey == VanillaForms.VampireKeyword);
    }
}
