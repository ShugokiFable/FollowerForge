using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Attaches the optional transformation script.
///
/// Werewolf fills BeastRace from the game's own WerewolfBeastRace. It does NOT attach
/// WerewolfChangeFX: that spell's script waits 10 seconds and SetRace(Werewolf)s the target
/// on its own, which re-wolfs a follower after Revert() has already run.
/// </summary>
public sealed class TransformCompiler(ILogger log)
{
    /// <summary>Must match the compiled FF_Transform.pex shipped alongside the app.</summary>
    public const string ScriptName = "FF_Transform";

    public sealed record TransformResult(TransformKind Kind, FormKey? BeastRace);

    /// <summary>Returns null when no transformation was asked for, which is the default.</summary>
    public TransformResult? Compile(Npc npc, TransformSpec spec)
    {
        if (!spec.IsUsable) return null;

        // Werewolf resolves the beast race from Skyrim.esm. TransformFX is only what the
        // user picked — never the vanilla beast-form visual, which independently SetRace()s
        // the target after a 10s wait and undoes revert.
        var beastRace = spec.Kind == TransformKind.Werewolf
            ? VanillaForms.WerewolfBeastRace
            : Key(spec.BeastRace);
        var fx = Key(spec.TransformFx);

        var entry = new ScriptEntry { Name = ScriptName, Flags = ScriptEntry.Flag.Local };
        if (beastRace is { } race) entry.Properties.Add(Obj("BeastRace", race));
        if (fx is { } effect) entry.Properties.Add(Obj("TransformFX", effect));
        if (Key(spec.OnTransformSpell) is { } onTransform)
            entry.Properties.Add(Obj("OnTransform", onTransform));
        entry.Properties.Add(new ScriptBoolProperty { Name = "RevertOutOfCombat", Data = spec.RevertOutOfCombat });
        entry.Properties.Add(new ScriptFloatProperty { Name = "DelaySeconds", Data = Math.Max(0f, spec.DelaySeconds) });

        npc.VirtualMachineAdapter ??= new VirtualMachineAdapter();
        npc.VirtualMachineAdapter.Scripts.Add(entry);

        log.Information("Transformation: {Kind}, beast race {Race}, reverts {Revert}",
            spec.Kind, beastRace?.ToString() ?? "(none)", spec.RevertOutOfCombat);
        return new TransformResult(spec.Kind, beastRace);
    }

    private static FormKey? Key(RecordRef? reference) =>
        reference is null ? null : FormKey.Factory(reference.FormKey);

    private static ScriptObjectProperty Obj(string name, FormKey key) =>
        new() { Name = name, Object = new FormLink<ISkyrimMajorRecordGetter>(key) };
}
