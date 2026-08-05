using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// The Enemy-to-Ally loop: find her, beat her, then summon her.
///
/// Chain read out of AsianFollowers.esp rather than invented — a hostile non-essential copy in
/// bandit/creature factions carrying a spell tome, a spell whose script archetype enables an
/// initially-disabled ally reference, and no death script anywhere.
/// </summary>
public sealed class EnemyToAllyTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static SpawnLocation Place(string id) => new()
    {
        Id = id,
        Name = id,
        Kind = LocationKind.Interior,
        CellFormKey = "01A270:Skyrim.esm",
        RequiredPlugin = "Skyrim.esm",
    };

    private static FollowerProfile Profile(EnemyToAllySpec spec) => new()
    {
        Name = "Nadia",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        Essential = true,
        EnemyToAlly = spec,
    };

    private static FollowerCompiler.CompileResult Compile(EnemyToAllySpec spec, params string[] spots) =>
        new FollowerCompiler(Log).Compile(
            Profile(spec), location: null,
            alternateSpawns: spots.Select(id => (Place(id), (ICellGetter?)null)).ToList());

    private static EnemyToAllySpec Spec(params string[] ids) =>
        new() { Enabled = true, LocationIds = ids };

    [Fact]
    public void OffByDefault_JustAnOrdinaryFollower()
    {
        var result = new FollowerCompiler(Log).Compile(Profile(new EnemyToAllySpec()), location: null);

        Assert.Null(result.EnemyToAlly);
        Assert.Single(result.Mod.Npcs);
        Assert.Empty(result.Mod.Books);
        Assert.Empty(result.Mod.Spells);
    }

    [Fact]
    public void EnabledWithNowhereToBeFound_EmitsNothing()
    {
        // She could never be beaten, so she could never be recruited.
        Assert.False(Spec().IsUsable);
        Assert.Null(Compile(Spec()).EnemyToAlly);
    }

    [Fact]
    public void HerHostileFormIsASecondNpc_ThatCanActuallyBeKilled()
    {
        var result = Compile(Spec("a"), "a");

        var enemy = result.Mod.Npcs.First(n => n.FormKey == result.EnemyToAlly!.EnemyNpc);
        Assert.False(enemy.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Essential));
        Assert.False(enemy.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Protected));
    }

    [Fact]
    public void HerHostileFormIsNotAlreadyAFollower()
    {
        // Keeping the follower factions would make her recruitable before she is ever beaten.
        var result = Compile(Spec("a"), "a");

        var enemy = result.Mod.Npcs.First(n => n.FormKey == result.EnemyToAlly!.EnemyNpc);
        Assert.DoesNotContain(enemy.Factions, f => f.Faction.FormKey == VanillaForms.PotentialFollowerFaction);
        Assert.DoesNotContain(enemy.Factions, f => f.Faction.FormKey == VanillaForms.CurrentFollowerFaction);
        Assert.Contains(enemy.Factions, f => f.Faction.FormKey == VanillaForms.BanditFaction);
    }

    [Fact]
    public void SheCarriesTheTome_SoBeatingHerIsHowYouGetIt()
    {
        var result = Compile(Spec("a"), "a");
        var e2a = result.EnemyToAlly!;

        var enemy = result.Mod.Npcs.First(n => n.FormKey == e2a.EnemyNpc);
        Assert.Contains(enemy.Items!, i => i.Item.Item.FormKey == e2a.Tome);

        var tome = result.Mod.Books.First(b => b.FormKey == e2a.Tome);
        Assert.Equal(e2a.Spell, ((BookSpell)tome.Teaches!).Spell.FormKey);
    }

    [Fact]
    public void TheSpellSummonsHerOwnDisabledReference()
    {
        var result = Compile(Spec("a"), "a");

        var ally = result.Mod.EnumerateMajorRecords<IPlacedNpcGetter>()
            .First(r => r.FormKey == result.PlacedFormKey);
        Assert.True(ally.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled));

        var effect = result.Mod.MagicEffects.First(x => x.FormKey == result.EnemyToAlly!.MagicEffect);
        var script = Assert.Single(effect.VirtualMachineAdapter!.Scripts);
        Assert.Equal(EnemyToAllyCompiler.ScriptName, script.Name);
        var target = script.Properties.OfType<ScriptObjectProperty>().First(p => p.Name == "AllyToSummon");
        Assert.Equal(result.PlacedFormKey, target.Object.FormKey);
    }

    [Fact]
    public void TheQuestPlacesTheENEMY_NotTheFollower()
    {
        // With E2A the follower must stay disabled; spawning her would hand the player a follower
        // without any of the finding or fighting.
        var result = Compile(Spec("a", "b"), "a", "b");

        var script = Assert.Single(result.Mod.Quests).VirtualMachineAdapter!.Scripts.Single();
        var objects = script.Properties.OfType<ScriptObjectProperty>().ToDictionary(p => p.Name, p => p.Object.FormKey);
        Assert.Equal(result.EnemyToAlly!.EnemyNpc, objects["SpawnBase"]);
        Assert.DoesNotContain("Follower", objects.Keys);
    }

    [Fact]
    public void NoDeathScriptIsInvolvedAnywhere()
    {
        // The tome is loot, exactly as in the mods this was read from.
        var result = Compile(Spec("a"), "a");

        Assert.All(result.Mod.Npcs, n => Assert.Null(n.VirtualMachineAdapter));
    }

    [Fact]
    public void TheCompiledScriptIsBundledWithTheApp()
    {
        var names = typeof(FollowerForge.BuildPipeline.FollowerBuilder).Assembly.GetManifestResourceNames();
        Assert.Contains(names, n => n.EndsWith("FF_Summon.pex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.EndsWith("FF_Summon.psc", StringComparison.OrdinalIgnoreCase));
    }
}
