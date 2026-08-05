using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// The experimental evolution system. The most important test here is the first one: this is
/// the only feature that puts a script in a generated follower, so it must emit absolutely
/// nothing unless explicitly asked for.
/// </summary>
public sealed class EvolutionTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile Profile(EvolutionSpec evolution) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        EditorIdPrefix = "FFTest",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        Evolution = evolution,
    };

    private static FollowerCompiler.CompileResult Compile(EvolutionSpec evolution) =>
        new FollowerCompiler(Log).Compile(Profile(evolution), location: null);

    [Fact]
    public void OffByDefault_NoScriptAndNoGlobals()
    {
        var result = Compile(new EvolutionSpec());

        Assert.Null(result.Evolution);
        Assert.Empty(result.Mod.Globals);
        Assert.Null(result.Mod.Npcs.First().VirtualMachineAdapter);
    }

    [Fact]
    public void EnabledButNonsensical_StillEmitsNothing()
    {
        // A single phase cannot evolve, and zero fights per phase would advance instantly.
        Assert.Null(Compile(new EvolutionSpec { Enabled = true, Phases = 1 }).Evolution);
        Assert.Null(Compile(new EvolutionSpec { Enabled = true, CombatsPerPhase = 0 }).Evolution);
    }

    [Fact]
    public void Enabled_AttachesTheScriptWithItsTuningValues()
    {
        var result = Compile(new EvolutionSpec
        {
            Enabled = true,
            Phases = 3,
            CombatsPerPhase = 40,
            StartConfidence = 0,
            EndConfidence = 4,
        });

        var script = Assert.Single(result.Mod.Npcs.First().VirtualMachineAdapter!.Scripts);
        Assert.Equal(EvolutionCompiler.ScriptName, script.Name);

        var ints = script.Properties.OfType<ScriptIntProperty>().ToDictionary(p => p.Name, p => p.Data);
        Assert.Equal(40, ints["CombatsPerPhase"]);
        Assert.Equal(3, ints["MaxPhase"]);
        Assert.Equal(0, ints["StartConfidence"]);
        Assert.Equal(4, ints["EndConfidence"]);
    }

    [Fact]
    public void PhaseAndProgressAreGlobals_SoDialogueAndTheConsoleCanSeeThem()
    {
        var result = Compile(new EvolutionSpec { Enabled = true });

        Assert.Equal(2, result.Mod.Globals.Count);
        // Resolve by the FormKey the compiler reported rather than by name, so the test does not
        // silently depend on how the EditorID prefix is sanitised.
        var phase = result.Mod.Globals.First(g => g.FormKey == result.Evolution!.PhaseGlobal);
        Assert.Equal((short)1, Assert.IsType<GlobalShort>(phase).Data);   // she begins in phase one

        // The script properties must point at those globals, or nothing is ever recorded.
        var objects = Assert.Single(result.Mod.Npcs.First().VirtualMachineAdapter!.Scripts)
            .Properties.OfType<ScriptObjectProperty>().ToDictionary(p => p.Name, p => p.Object.FormKey);
        Assert.Equal(phase.FormKey, objects["FF_Phase"]);
        Assert.Contains("FF_Progress", objects.Keys);
    }

    [Fact]
    public void SheStartsAtThePhaseOneConfidenceOnTheRecordItself()
    {
        // If the record said "Brave", her first fight would happen at Brave — before the script
        // has had any chance to run. The starting value has to be baked in.
        var result = Compile(new EvolutionSpec { Enabled = true, StartConfidence = 0 });

        Assert.Equal(Mutagen.Bethesda.Skyrim.Confidence.Cowardly,
            result.Mod.Npcs.First().AIData!.Confidence);
    }

    [Fact]
    public void SameProfileCompilesIdentically()
    {
        var spec = new EvolutionSpec { Enabled = true, Phases = 4, CombatsPerPhase = 12 };
        var a = Compile(spec);
        var b = Compile(spec);

        Assert.Equal(a.Evolution!.PhaseGlobal, b.Evolution!.PhaseGlobal);
        Assert.Equal(a.Evolution.ProgressGlobal, b.Evolution.ProgressGlobal);
        Assert.Equal(a.NpcFormKey, b.NpcFormKey);
    }

    [Fact]
    public void TheCompiledScriptIsBundledWithTheApp()
    {
        // The plugin references the script by name; without the .pex she loads fine and simply
        // never evolves, which is exactly the sort of silent failure worth a test.
        var assembly = typeof(FollowerForge.BuildPipeline.FollowerBuilder).Assembly;
        var names = assembly.GetManifestResourceNames();

        Assert.Contains(names, n => n.EndsWith("FF_Evolution.pex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.EndsWith("FF_Evolution.psc", StringComparison.OrdinalIgnoreCase));
    }
}
