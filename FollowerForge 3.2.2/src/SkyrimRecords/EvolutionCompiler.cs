using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Attaches the optional evolution script to a generated follower.
///
/// The script itself is written and compiled once by us and shipped with the app, so nobody
/// needs a Papyrus compiler to build a follower — the plugin just references it by name and
/// fills in its properties.
///
/// State lives in two globals rather than inside the script. That is deliberate: globals are
/// visible to dialogue conditions and to the console, they can be retuned by a one-line patch
/// plugin the way Melana's "evolve faster/slower" add-ons do, and a script that goes wrong
/// loses its counters without taking anything else with it.
/// </summary>
public sealed class EvolutionCompiler(ILogger log)
{
    /// <summary>Must match the compiled FF_Evolution.pex shipped alongside the app.</summary>
    public const string ScriptName = "FF_Evolution";

    public sealed record EvolutionResult(FormKey PhaseGlobal, FormKey ProgressGlobal);

    /// <summary>Returns null when evolution is off, which is the default.</summary>
    public EvolutionResult? Compile(SkyrimMod mod, Npc npc, string prefix, EvolutionSpec spec)
    {
        if (!spec.IsUsable) return null;

        // Allocated in a fixed order so the same profile keeps producing the same FormIDs.
        var phase = new GlobalShort(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}Phase",
            Data = 1,
        };
        var progress = new GlobalShort(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}Progress",
            Data = 0,
        };
        mod.Globals.Add(phase);
        mod.Globals.Add(progress);

        var entry = new ScriptEntry { Name = ScriptName, Flags = ScriptEntry.Flag.Local };
        entry.Properties.Add(Obj("FF_Phase", phase.FormKey));
        entry.Properties.Add(Obj("FF_Progress", progress.FormKey));
        entry.Properties.Add(Int("CombatsPerPhase", spec.CombatsPerPhase));
        entry.Properties.Add(Int("MaxPhase", spec.Phases));
        entry.Properties.Add(Int("StartConfidence", spec.StartConfidence));
        entry.Properties.Add(Int("EndConfidence", spec.EndConfidence));
        entry.Properties.Add(Int("SkillPerPhase", spec.SkillPerPhase));
        entry.Properties.Add(Flt("HealthPerPhase", spec.HealthPerPhase));
        entry.Properties.Add(Flt("StaminaPerPhase", spec.StaminaPerPhase));
        entry.Properties.Add(Flt("MagickaPerPhase", spec.MagickaPerPhase));

        npc.VirtualMachineAdapter ??= new VirtualMachineAdapter();
        npc.VirtualMachineAdapter.Scripts.Add(entry);

        // She must START at the phase-one confidence, or her first fight happens at whatever the
        // record says before the script has had a chance to run.
        npc.AIData ??= new AIData();
        npc.AIData.Confidence = (Confidence)Math.Clamp(spec.StartConfidence, (byte)0, (byte)4);

        log.Information("Evolution: {Phases} phases, {Combats} fights each, confidence {From}->{To}",
            spec.Phases, spec.CombatsPerPhase, spec.StartConfidence, spec.EndConfidence);
        return new EvolutionResult(phase.FormKey, progress.FormKey);
    }

    private static ScriptObjectProperty Obj(string name, FormKey key) =>
        new() { Name = name, Object = new FormLink<ISkyrimMajorRecordGetter>(key) };

    private static ScriptIntProperty Int(string name, int value) =>
        new() { Name = name, Data = value };

    private static ScriptFloatProperty Flt(string name, float value) =>
        new() { Name = name, Data = value };
}
