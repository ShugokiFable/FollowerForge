using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// AI packages and the player relationship. The ordering assertions matter most: Skyrim runs the
/// first package whose conditions hold, so a broad sandbox placed above a sleep package silently
/// prevents her from ever going to bed.
/// </summary>
public sealed class BehaviorTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile Profile(BehaviorSpec behavior) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        Behavior = behavior,
    };

    private static Npc Build(BehaviorSpec behavior) =>
        new FollowerCompiler(Log).Compile(Profile(behavior), location: null).Mod.Npcs.First();

    [Fact]
    public void SleepIsListedAboveSandbox_OrItWouldNeverFire()
    {
        var npc = Build(new BehaviorSpec { Idle = IdleBehavior.WandersNearby, SleepsAtNight = true });

        Assert.Equal(
            [VanillaForms.SleepEditorLoc24x8, VanillaForms.SandboxEditorLocation512],
            npc.Packages.Select(p => p.FormKey));
    }

    [Fact]
    public void ChosenPackagesGoAboveEverythingGenerated()
    {
        var mine = new RecordRef("0ABCDE:Skyrim.esm");
        var npc = Build(new BehaviorSpec
        {
            ExtraPackages = [mine],
            SleepsAtNight = true,
            Idle = IdleBehavior.StaysPut,
        });

        Assert.Equal(3, npc.Packages.Count);
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory(mine.FormKey), npc.Packages[0].FormKey);
        Assert.Equal(VanillaForms.SandboxEditorLocation256, npc.Packages[^1].FormKey);
    }

    [Theory]
    [InlineData(IdleBehavior.StaysPut)]
    [InlineData(IdleBehavior.WandersNearby)]
    [InlineData(IdleBehavior.SettlesWhereverSheIs)]
    public void EveryIdleChoiceUsesAVanillaPackage_SoSheNeedsNoExtraMod(IdleBehavior idle)
    {
        var npc = Build(new BehaviorSpec { Idle = idle, SleepsAtNight = false });

        var package = Assert.Single(npc.Packages);
        Assert.Equal("Skyrim.esm", package.FormKey.ModKey.FileName);
    }

    [Fact]
    public void EngineDefaultAddsNothing()
    {
        // Not the same as "she stands still": most vanilla NPCs carry no package and still
        // sandbox. This choice simply declines to override that.
        var npc = Build(new BehaviorSpec { Idle = IdleBehavior.EngineDefault, SleepsAtNight = false });
        Assert.Empty(npc.Packages);
    }

    [Theory]
    [InlineData(RelationshipRank.Lover, Relationship.RankType.Lover)]
    [InlineData(RelationshipRank.Ally, Relationship.RankType.Ally)]
    [InlineData(RelationshipRank.Confidant, Relationship.RankType.Confidant)]
    [InlineData(RelationshipRank.Rival, Relationship.RankType.Rival)]
    [InlineData(RelationshipRank.Archnemesis, Relationship.RankType.Archnemesis)]
    public void RelationshipRankReachesTheRecord(RelationshipRank chosen, Relationship.RankType expected)
    {
        var result = new FollowerCompiler(Log).Compile(
            Profile(new BehaviorSpec { Relationship = chosen }), location: null);

        var rela = Assert.Single(result.Mod.Relationships);
        Assert.Equal(expected, rela.Rank);
        Assert.Equal(result.NpcFormKey, rela.Parent.FormKey);
        Assert.Equal(VanillaForms.PlayerNpc, rela.Child.FormKey);
    }

    [Fact]
    public void SheCanStartCowardly()
    {
        // The wizard used to map a 3-way "temperament" onto 1/3/4, so Cowardly was unreachable —
        // the exact roleplay start the user asked for could not be built.
        var result = new FollowerCompiler(Log).Compile(
            new FollowerProfile
            {
                Name = "Timid",
                PluginName = "FF_Timid.esp",
                Race = new RecordRef(VanillaForms.NordRace.ToString()),
                VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
                Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
                Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
                Ai = new AiValues { Confidence = 0 },
            },
            location: null);

        Assert.Equal(Confidence.Cowardly, result.Mod.Npcs.First().AIData!.Confidence);
    }

    [Fact]
    public void RelationshipsWithOtherPeopleBecomeTheirOwnRecords()
    {
        // Lydia (HousecarlWhiterun), read from the live load order rather than recalled.
        // FormKeys carry the 6-hex LOCAL id, not the 8-hex full FormID.
        var lydia = new RecordRef("0A2C8E:Skyrim.esm");
        var result = new FollowerCompiler(Log).Compile(
            Profile(new BehaviorSpec
            {
                Relationship = RelationshipRank.Ally,
                OtherRelationships =
                [
                    new NpcRelationship { Npc = lydia, Rank = RelationshipRank.Rival },
                ],
            }),
            location: null);

        // One toward the player, one toward Lydia.
        Assert.Equal(2, result.Mod.Relationships.Count);
        var toLydia = result.Mod.Relationships.First(r => r.Child.FormKey != VanillaForms.PlayerNpc);
        Assert.Equal(Relationship.RankType.Rival, toLydia.Rank);
        Assert.Equal(result.NpcFormKey, toLydia.Parent.FormKey);
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory(lydia.FormKey), toLydia.Child.FormKey);
    }

    [Fact]
    public void DefaultsAreTheOnesAFollowerActuallyWants()
    {
        var npc = Build(new BehaviorSpec());
        Assert.Equal(
            [VanillaForms.SleepEditorLoc24x8, VanillaForms.SandboxEditorLocation512],
            npc.Packages.Select(p => p.FormKey));
    }
}
