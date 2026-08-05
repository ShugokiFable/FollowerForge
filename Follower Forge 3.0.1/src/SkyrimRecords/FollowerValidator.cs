using FollowerForge.Domain;
using Mutagen.Bethesda;
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
        => ValidateMod(result.Mod, profile, report);

    /// <summary>
    /// Validates the serialized bytes that will be published. This catches choices that existed
    /// in memory but were lost or mistranslated during master-list and FormID serialization.
    /// </summary>
    public static void ValidateFile(string pluginPath, FollowerProfile profile, ValidationReport report)
    {
        using var mod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, VanillaForms.Release);
        ValidateMod(mod, profile, report);
    }

    private static void ValidateMod(ISkyrimModGetter mod, FollowerProfile profile, ValidationReport report)
    {
        var npc = mod.Npcs.FirstOrDefault();
        if (npc is null)
        {
            report.Add(ValidationSeverity.Error, "NO_NPC", "No NPC record was generated");
            return;
        }

        // Player relationship.
        var rel = mod.Relationships.FirstOrDefault(r =>
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
        var placed = mod.EnumerateMajorRecords<IPlacedNpcGetter>()
            .FirstOrDefault(p => p.Base.FormKey == npc.FormKey);
        if (placed is null)
            report.Add(ValidationSeverity.Error, "NO_PLACEMENT",
                "No placed ACHR references this follower (cannot be found in the world)");
        else if (((uint)placed.MajorRecordFlagsRaw & 0x00000400u) == 0)
            report.Add(ValidationSeverity.Error, "PLACEMENT_NOT_PERSISTENT",
                "The follower ACHR is stored in a persistent child group without its Persistent flag.");

        // Protection sanity: a follower that is neither protected nor essential can die permanently.
        var flags = npc.Configuration.Flags;
        if (!flags.HasFlag(NpcConfiguration.Flag.Protected) && !flags.HasFlag(NpcConfiguration.Flag.Essential))
            report.Add(ValidationSeverity.Warning, "MORTAL",
                "Follower is neither Protected nor Essential and can be killed permanently");

        if (!flags.HasFlag(NpcConfiguration.Flag.Unique))
            report.Add(ValidationSeverity.Warning, "NOT_UNIQUE",
                "Follower is not Unique; respawn/renumber can break FaceGen");

        // Stats mode must survive serialization exactly. Automatic profiles rely on the class;
        // custom profiles require a complete DNAM block and must not retain AutoCalcStats.
        if (profile.Stats.Mode == FollowerStatsMode.AutoCalculate)
        {
            if (!flags.HasFlag(NpcConfiguration.Flag.AutoCalcStats))
                report.Add(ValidationSeverity.Error, "AUTOCALC_STATS_NOT_WRITTEN",
                    "Automatic stats were requested but the NPC AutoCalcStats flag is missing.");
        }
        else
        {
            if (flags.HasFlag(NpcConfiguration.Flag.AutoCalcStats))
                report.Add(ValidationSeverity.Error, "CUSTOM_STATS_IGNORED",
                    "Custom stats were requested but AutoCalcStats is still enabled.");

            if (npc.PlayerSkills is not { } playerSkills)
            {
                report.Add(ValidationSeverity.Error, "CUSTOM_STATS_NOT_WRITTEN",
                    "Custom stats were requested but the NPC has no DNAM player-skills block.");
            }
            else
            {
                if (playerSkills.Health != profile.Stats.Health ||
                    playerSkills.Magicka != profile.Stats.Magicka ||
                    playerSkills.Stamina != profile.Stats.Stamina)
                {
                    report.Add(ValidationSeverity.Error, "CUSTOM_ATTRIBUTES_MISMATCH",
                        "Custom Health, Magicka, or Stamina did not survive plugin serialization.");
                }

                foreach (var (profileSkill, skyrimSkill) in FollowerSkillMap.All)
                {
                    var expected = profile.Stats.GetSkill(profileSkill);
                    if (playerSkills.SkillValues[skyrimSkill] != expected)
                        report.Add(ValidationSeverity.Error, "CUSTOM_SKILL_MISMATCH",
                            $"Custom {profileSkill} value did not survive plugin serialization.");
                    if (playerSkills.SkillOffsets[skyrimSkill] != 0)
                        report.Add(ValidationSeverity.Error, "CUSTOM_SKILL_OFFSET",
                            $"Custom {profileSkill} has a non-zero hidden offset.");
                }
            }
        }

        // Every creator-facing loadout choice must survive into the actual NPC record.
        var actualItems = npc.Items?
            .Select(i => i.Item.Item.FormKey)
            .ToHashSet() ?? [];
        foreach (var expected in profile.InventoryItems.Select(i => Mutagen.Bethesda.Plugins.FormKey.Factory(i.FormKey)))
            if (!actualItems.Contains(expected))
                report.Add(ValidationSeverity.Error, "ITEM_NOT_WRITTEN",
                    $"Selected equipment/inventory item was not written to the NPC: {expected}");

        if (profile.EquippedArmor.Count > 0)
        {
            if (npc.DefaultOutfit.IsNull)
            {
                report.Add(ValidationSeverity.Error, "STARTING_EQUIPMENT_NOT_ASSIGNED",
                    "Selected armor is in inventory but no engine starting-equipment set is assigned.");
            }
            else
            {
                var outfit = mod.Outfits.FirstOrDefault(record =>
                    record.FormKey == npc.DefaultOutfit.FormKey);
                var outfitItems = outfit?.Items?.Select(item => item.FormKey).ToHashSet() ?? [];
                foreach (var expected in profile.EquippedArmor.Select(item =>
                             Mutagen.Bethesda.Plugins.FormKey.Factory(item.FormKey)))
                    if (!outfitItems.Contains(expected))
                        report.Add(ValidationSeverity.Error, "STARTING_ARMOR_NOT_WRITTEN",
                            $"Selected armor was not written to the starting-equipment set: {expected}");
            }
        }

        if (profile.Appearance.HeadParts.Count > 0)
        {
            var actualHeadParts = npc.HeadParts.Select(part => part.FormKey).ToHashSet();
            foreach (var expected in profile.Appearance.HeadParts.Select(part =>
                         Mutagen.Bethesda.Plugins.FormKey.Factory(part.FormKey)))
                if (!actualHeadParts.Contains(expected))
                    report.Add(ValidationSeverity.Error, "HEAD_PART_NOT_WRITTEN",
                        $"RaceMenu head part was not written to the NPC record: {expected}");
        }

        var actualSpells = npc.ActorEffect?.Select(s => s.FormKey).ToHashSet() ?? [];
        foreach (var expected in profile.Spells.Select(s => Mutagen.Bethesda.Plugins.FormKey.Factory(s.FormKey)))
            if (!actualSpells.Contains(expected))
                report.Add(ValidationSeverity.Error, "SPELL_NOT_WRITTEN",
                    $"Selected spell was not written to the NPC: {expected}");

        var actualPerks = npc.Perks?.Select(p => p.Perk.FormKey).ToHashSet() ?? [];
        foreach (var expected in profile.Perks.Select(p => Mutagen.Bethesda.Plugins.FormKey.Factory(p.FormKey)))
            if (!actualPerks.Contains(expected))
                report.Add(ValidationSeverity.Error, "PERK_NOT_WRITTEN",
                    $"Selected perk was not written to the NPC: {expected}");

        // Duplicate EditorIDs within the plugin corrupt lookups.
        var edids = mod.EnumerateMajorRecords()
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
