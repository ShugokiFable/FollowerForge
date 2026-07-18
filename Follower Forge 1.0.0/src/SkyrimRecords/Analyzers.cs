using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.SkyrimRecords;

/// <summary>Raw CSTY values plus inferred descriptive tags. Raw values are always exposed.</summary>
public sealed record CombatStyleDetail(
    float OffensiveMult,
    float DefensiveMult,
    float GroupOffensiveMult,
    float MeleeScore,
    float RangedScore,
    float MagicScore,
    IReadOnlyList<string> Tags);

/// <summary>Custom-race capability evaluation used to avoid silently swapping a chosen race.</summary>
public sealed record RaceDetail(
    bool HasMaleSkeleton,
    bool HasFemaleSkeleton,
    bool HasMaleHeadData,
    bool HasFemaleHeadData,
    string? SkinFormKey,
    string? ArmorRaceFormKey,
    string? MorphRaceFormKey,
    string? MaleDefaultVoiceFormKey,
    string? FemaleDefaultVoiceFormKey,
    int EyeCount,
    int HairCount,
    bool PlayableFlag,
    IReadOnlyList<string> Notes);

public static class CombatStyleAnalyzer
{
    public static CombatStyleDetail Analyze(ICombatStyleGetter cs)
    {
        float off = cs.OffensiveMult, def = cs.DefensiveMult, grp = cs.GroupOffensiveMult;
        float melee = cs.EquipmentScoreMultMelee, ranged = cs.EquipmentScoreMultRanged, magic = cs.EquipmentScoreMultMagic;

        var tags = new List<string>();
        // Inferred tags — heuristic, never a substitute for the raw values above.
        if (off >= def * 1.25f) tags.Add("aggressive");
        else if (def >= off * 1.25f) tags.Add("defensive");
        else tags.Add("balanced");
        if (ranged > melee && ranged > magic) tags.Add("prefers-ranged");
        else if (magic > melee && magic > ranged) tags.Add("prefers-magic");
        else if (melee > 0) tags.Add("prefers-melee");
        if (grp >= 1.5f) tags.Add("group-tactics");
        if (cs.Flags?.HasFlag(CombatStyle.Flag.AllowDualWielding) == true) tags.Add("allows-dual-wield");

        return new CombatStyleDetail(off, def, grp, melee, ranged, magic, tags);
    }
}

public static class RaceAnalyzer
{
    public static RaceDetail Analyze(IRaceGetter race)
    {
        var notes = new List<string>();
        var playable = race.Flags.HasFlag(Race.Flag.Playable);

        var maleSkel = race.SkeletalModel?.Male?.File?.GivenPath is { Length: > 0 };
        var femSkel = race.SkeletalModel?.Female?.File?.GivenPath is { Length: > 0 };
        var maleHead = race.HeadData?.Male is not null;
        var femHead = race.HeadData?.Female is not null;

        if (!maleHead && !femHead)
            notes.Add("No head-part data on either gender; FaceGen may need the race mod's own head parts.");
        if (race.ArmorRace.IsNull)
            notes.Add("No ArmorRace set; armor add-ons may not fit — verify ARMA race compatibility.");
        if (race.Skin.IsNull)
            notes.Add("No default skin; body/skin comes from the race mod or a skin override.");

        return new RaceDetail(
            HasMaleSkeleton: maleSkel,
            HasFemaleSkeleton: femSkel,
            HasMaleHeadData: maleHead,
            HasFemaleHeadData: femHead,
            SkinFormKey: race.Skin.IsNull ? null : race.Skin.FormKey.ToString(),
            ArmorRaceFormKey: race.ArmorRace.IsNull ? null : race.ArmorRace.FormKey.ToString(),
            MorphRaceFormKey: race.MorphRace.IsNull ? null : race.MorphRace.FormKey.ToString(),
            MaleDefaultVoiceFormKey: race.Voices?.Male is { } mv && !mv.IsNull ? mv.FormKey.ToString() : null,
            FemaleDefaultVoiceFormKey: race.Voices?.Female is { } fv && !fv.IsNull ? fv.FormKey.ToString() : null,
            EyeCount: race.Eyes?.Count ?? 0,
            HairCount: race.Hairs?.Count ?? 0,
            PlayableFlag: playable,
            Notes: notes);
    }
}
