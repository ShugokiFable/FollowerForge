namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Turning a follower into a vampire needs no script and no new records — a vanilla vampire NPC
/// is simply the vampire variant of her race plus two keywords.
///
/// Read off Sybille Stentor rather than assumed:
///   NPC SybilleStentor  race=BretonRaceVampire
///     keyword Vampire
///     keyword ActorTypeUndead
///   ...and no vampire spells, abilities or faction on the record at all.
///
/// The variant is found by EditorID convention (&lt;race&gt;Vampire), which all eleven vanilla
/// playable races follow — NordRace/NordRaceVampire, ElderRace/ElderRaceVampire and so on. The
/// same convention is what custom-race mods use, so a custom race that ships a vampire form is
/// picked up automatically and one that does not is reported instead of silently ignored.
/// </summary>
public static class VampireRaces
{
    /// <summary>The EditorID a vampire form of this race would have.</summary>
    public static string VariantEditorId(string raceEditorId) => raceEditorId + "Vampire";

    /// <summary>True when this race is already a vampire form, so it needs no swap.</summary>
    public static bool IsVampireRace(string? raceEditorId) =>
        raceEditorId is not null
        && raceEditorId.EndsWith("Vampire", StringComparison.OrdinalIgnoreCase);
}
