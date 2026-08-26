using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// The experimental transformation system. As with evolution, the first test matters most: it
/// must emit nothing at all unless asked for.
/// </summary>
public sealed class TransformationTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile Profile(TransformSpec transformation) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        Transformation = transformation,
    };

    private static FollowerCompiler.CompileResult Compile(TransformSpec spec) =>
        new FollowerCompiler(Log).Compile(Profile(spec), location: null);

    private static IReadOnlyDictionary<string, Mutagen.Bethesda.Plugins.FormKey> Objects(
        FollowerCompiler.CompileResult result) =>
        result.Mod.Npcs.First().VirtualMachineAdapter!.Scripts
            .Single(s => s.Name == TransformCompiler.ScriptName)
            .Properties.OfType<ScriptObjectProperty>()
            .ToDictionary(p => p.Name, p => p.Object.FormKey);

    [Fact]
    public void OffByDefault_NoScript()
    {
        var result = Compile(new TransformSpec());
        Assert.Null(result.Transformation);
        Assert.Null(result.Mod.Npcs.First().VirtualMachineAdapter);
    }

    [Fact]
    public void CustomWithNothingChosen_EmitsNothing()
    {
        // A script with no race and no spell would attach and then do absolutely nothing.
        var result = Compile(new TransformSpec { Kind = TransformKind.Custom });
        Assert.Null(result.Transformation);
    }

    [Fact]
    public void Werewolf_UsesTheGamesOwnRace_WithoutWerewolfChangeFx()
    {
        // WerewolfChangeFX (0x0F8208) is not a VFX. Its magic effect runs
        // WerewolfTransformVisual, which Utility.Wait(10) then SetRace(Werewolf) on the
        // target. That delayed SetRace fires AFTER combat ends and after our Revert(),
        // which is why werewolf followers stayed wolves after the battle.
        var result = Compile(new TransformSpec { Kind = TransformKind.Werewolf });

        var objects = Objects(result);
        Assert.Equal(VanillaForms.WerewolfBeastRace, objects["BeastRace"]);
        Assert.False(objects.ContainsKey("TransformFX"));
    }

    [Fact]
    public void BundledTransformScript_DoesNotCastWerewolfChangeFx()
    {
        var source = ReadBundledTransformSource();
        Assert.DoesNotContain("WerewolfChangeFX", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GetCombatState()", source, StringComparison.Ordinal);
        Assert.Contains("originalRace", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BundledTransformScript_AbortsIfTheFightAlreadyEnded()
    {
        // A 2s Wait after combat-start, then a blind SetRace, turns them AFTER the
        // battle. Revert already ran (or never will). The wait must re-check combat.
        var source = ReadBundledTransformSource();
        Assert.Contains("GetCombatState()", source, StringComparison.Ordinal);
        Assert.Contains("transformed = False", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadBundledTransformSource()
    {
        var asm = typeof(FollowerForge.BuildPipeline.FollowerBuilder).Assembly;
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("FF_Transform.psc", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Werewolf_NeedsNothingBeyondSkyrim()
    {
        var result = Compile(new TransformSpec { Kind = TransformKind.Werewolf });

        // Every record the script points at must come from the base game, or a "vanilla"
        // werewolf follower would quietly acquire a dependency.
        Assert.All(Objects(result).Values,
            key => Assert.Equal("Skyrim.esm", key.ModKey.FileName));
    }

    [Fact]
    public void Custom_PointsAtTheUsersOwnRecords()
    {
        var race = new RecordRef("0ABCDE:SomeCreatures.esp");
        var spell = new RecordRef("0FEDCB:SomeMagic.esp");

        var result = Compile(new TransformSpec
        {
            Kind = TransformKind.Custom,
            BeastRace = race,
            OnTransformSpell = spell,
        });

        var objects = Objects(result);
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory(race.FormKey), objects["BeastRace"]);
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory(spell.FormKey), objects["OnTransform"]);
    }

    [Fact]
    public void SpellOnlyTransformation_IsAllowed()
    {
        // This is the shape the transforming followers actually use: no race swap, just a spell
        // cast a few seconds into combat.
        var result = Compile(new TransformSpec
        {
            Kind = TransformKind.Custom,
            OnTransformSpell = new RecordRef("0FEDCB:SomeMagic.esp"),
        });

        Assert.NotNull(result.Transformation);
        Assert.DoesNotContain("BeastRace", Objects(result).Keys);
    }

    [Fact]
    public void RevertPreferenceReachesTheScript()
    {
        var result = Compile(new TransformSpec { Kind = TransformKind.Werewolf, RevertOutOfCombat = false });

        var revert = result.Mod.Npcs.First().VirtualMachineAdapter!.Scripts
            .Single(s => s.Name == TransformCompiler.ScriptName)
            .Properties.OfType<ScriptBoolProperty>().Single(p => p.Name == "RevertOutOfCombat");
        Assert.False(revert.Data);
    }

    [Fact]
    public void EvolutionAndTransformationCoexistOnTheSameFollower()
    {
        var profile = Profile(new TransformSpec { Kind = TransformKind.Werewolf }) with
        {
            Evolution = new EvolutionSpec { Enabled = true },
        };
        var result = new FollowerCompiler(Log).Compile(profile, location: null);

        var scripts = result.Mod.Npcs.First().VirtualMachineAdapter!.Scripts.Select(s => s.Name).ToList();
        Assert.Contains(EvolutionCompiler.ScriptName, scripts);
        Assert.Contains(TransformCompiler.ScriptName, scripts);
    }

    [Fact]
    public void TheCompiledScriptIsBundledWithTheApp()
    {
        var names = typeof(FollowerForge.BuildPipeline.FollowerBuilder).Assembly.GetManifestResourceNames();
        Assert.Contains(names, n => n.EndsWith("FF_Transform.pex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.EndsWith("FF_Transform.psc", StringComparison.OrdinalIgnoreCase));
    }
}
