using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Record-level correctness checks on a compiled follower (complements the byte-level
/// ship-gate). Confirms the things that make an NPC an actual recruitable follower.
/// </summary>
public static class FollowerValidator
{
    public static void Validate(FollowerCompiler.CompileResult result, FollowerProfile profile, ValidationReport report)
    {
        var npc = result.Mod.Npcs.FirstOrDefault();
        if (npc is null)
        {
            report.Add(ValidationSeverity.Error, "NO_NPC", "No NPC record was generated");
            return;
        }

        // Player relationship.
        var rel = result.Mod.Relationships.FirstOrDefault(r =>
            r.Parent.FormKey == npc.FormKey && r.Child.FormKey == VanillaForms.PlayerNpc);
        if (rel is null)
            report.Add(ValidationSeverity.Error, "NO_RELATIONSHIP",
                "Missing player relationship (RELA parent=follower, child=Player)");

        // Follower factions with correct ranks.
        var hasPotential = npc.Factions.Any(f =>
            f.Faction.FormKey == VanillaForms.PotentialFollowerFaction && f.Rank == 0);
        var hasCurrent = npc.Factions.Any(f =>
            f.Faction.FormKey == VanillaForms.CurrentFollowerFaction && f.Rank == -1);
        if (!hasPotential)
            report.Add(ValidationSeverity.Error, "NO_POTENTIAL_FOLLOWER",
                "Missing PotentialFollowerFaction rank 0 (cannot be recruited)");
        if (!hasCurrent)
            report.Add(ValidationSeverity.Error, "NO_CURRENT_FOLLOWER",
                "Missing CurrentFollowerFaction rank -1 (engine follower state)");

        // Voice, class, race present.
        if (npc.Voice.IsNull)
            report.Add(ValidationSeverity.Error, "NO_VOICE", "NPC has no voice type (no follower dialogue)");
        if (npc.Class.IsNull)
            report.Add(ValidationSeverity.Error, "NO_CLASS", "NPC has no class");
        if (npc.Race.IsNull)
            report.Add(ValidationSeverity.Error, "NO_RACE", "NPC has no race");

        // Placement: any ACHR (interior or exterior) whose base is this follower.
        var placed = result.Mod.EnumerateMajorRecords<IPlacedNpcGetter>()
            .Any(p => p.Base.FormKey == npc.FormKey);
        if (!placed)
            report.Add(ValidationSeverity.Error, "NO_PLACEMENT",
                "No placed ACHR references this follower (cannot be found in the world)");

        // Protection sanity: a follower that is neither protected nor essential can die permanently.
        var flags = npc.Configuration.Flags;
        if (!flags.HasFlag(NpcConfiguration.Flag.Protected) && !flags.HasFlag(NpcConfiguration.Flag.Essential))
            report.Add(ValidationSeverity.Warning, "MORTAL",
                "Follower is neither Protected nor Essential and can be killed permanently");

        if (!flags.HasFlag(NpcConfiguration.Flag.Unique))
            report.Add(ValidationSeverity.Warning, "NOT_UNIQUE",
                "Follower is not Unique; respawn/renumber can break FaceGen");

        // Duplicate EditorIDs within the plugin corrupt lookups.
        var edids = result.Mod.EnumerateMajorRecords()
            .Select(r => r.EditorID)
            .Where(e => !string.IsNullOrEmpty(e))
            .GroupBy(e => e, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key!)
            .ToList();
        foreach (var dup in edids)
            report.Add(ValidationSeverity.Error, "DUP_EDITORID", $"Duplicate EditorID in plugin: {dup}");
    }
}
