using FollowerForge.Domain;

namespace FollowerForge.Ui;

/// <summary>
/// What every wizard row shows: a name, one line of why it matters, and an optional chip.
/// One DataTemplate is written against this, so faces, places and records all read the same.
///
/// Chip colours are theme-driven: the row only reports a BadgeKind and the XAML template maps
/// it to Border.chip-* class styles that pull DynamicResource brushes. That is what lets a live
/// theme switch repaint every visible chip; code-resolved static brushes could not.
/// </summary>
public interface IPickerRow
{
    string Display { get; }
    string? Detail { get; }
    string? Badge { get; }
    /// <summary>"good", "ok", "warn", "bad" or anything else for a quiet dim chip.</summary>
    string BadgeKind { get; }
    bool HasBadge { get; }
    bool HasDetail { get; }

    // Reflection bindings resolve on the concrete class, so these stay plain properties on
    // every row type instead of default interface members.
    bool ChipGood { get; }
    bool ChipOk { get; }
    bool ChipWarn { get; }
    bool ChipBad { get; }
    bool ChipDim { get; }
}

/// <summary>Shared chip-class helpers so every row type agrees on the mapping.</summary>
internal static class ChipClass
{
    internal static bool Good(string kind) => kind == "good";
    internal static bool Ok(string kind) => kind == "ok";
    internal static bool Warn(string kind) => kind == "warn";
    internal static bool Bad(string kind) => kind == "bad";
    internal static bool Dim(string kind) => kind is not ("good" or "ok" or "warn" or "bad");
}

/// <summary>
/// One choice in a wizard list. The row template reads Display, Detail and Badge; ToString()
/// is kept for search, logs and the summary. Gear rows put the FormKey on Detail so variants
/// with the same name can be told apart.
/// </summary>
public sealed class PickerItem(
    string display, string formKey, string? detail = null,
    int tier = 0, string? badge = null, string? badgeKind = null) : IPickerRow
{
    public string Display { get; } = display;
    public string FormKey { get; } = formKey;
    public string? Detail { get; } = detail;

    /// <summary>Sort bucket — lower is more useful. Meaning is per-list; 0 for unranked lists.</summary>
    public int Tier { get; } = tier;

    /// <summary>Short chip at the right of the row, e.g. "VANILLA". Null hides the chip.</summary>
    public string? Badge { get; } = badge;

    /// <summary>"good", "ok", "warn", "bad" or "dim".</summary>
    public string BadgeKind { get; } = badgeKind ?? "dim";

    public bool HasBadge => Badge is { Length: > 0 };
    public bool HasDetail => Detail is { Length: > 0 };
    public bool ChipGood => ChipClass.Good(BadgeKind);
    public bool ChipOk => ChipClass.Ok(BadgeKind);
    public bool ChipWarn => ChipClass.Warn(BadgeKind);
    public bool ChipBad => ChipClass.Bad(BadgeKind);
    public bool ChipDim => ChipClass.Dim(BadgeKind);

    public override string ToString() => Detail is { Length: > 0 } d ? $"{Display}   —   {d}" : Display;
}

/// <summary>A relationship with someone else in the world, shown by name rather than FormKey.</summary>
public sealed class KinItem(
    NpcRelationship relationship, string displayName,
    FollowerForge.Domain.FollowerPronouns pronouns) : IPickerRow
{
    public NpcRelationship Relationship { get; } = relationship;
    public string DisplayName { get; } = displayName;
    public FollowerForge.Domain.FollowerPronouns Pronouns { get; } = pronouns;

    public string Display => DisplayName;
    public string? Detail => WizardCopy.KinTreats(Pronouns, Relationship.Rank.ToString());
    public string? Badge => Relationship.Rank.ToString();
    public string BadgeKind => "dim";
    public bool HasBadge => true;
    public bool HasDetail => true;
    public bool ChipGood => false;
    public bool ChipOk => false;
    public bool ChipWarn => false;
    public bool ChipBad => false;
    public bool ChipDim => true;

    public override string ToString() => WizardCopy.KinRank(Pronouns, DisplayName, Relationship.Rank.ToString());
}

/// <summary>One custom line, shown as when-she-says-it followed by the words.</summary>
public sealed class LineItem(DialogueLine line) : IPickerRow
{
    public DialogueLine Line { get; } = line;

    private string When => Line.Trigger switch
    {
        DialogueTrigger.Hello => "on greeting",
        DialogueTrigger.Goodbye => "on parting",
        DialogueTrigger.Idle => "idle",
        _ => $"topic “{Line.Prompt}”",
    };

    // The words come first here: when scanning a list of her lines, the line is the content and
    // the trigger is the label.
    public string Display => Line.Text;
    public string? Detail => Line.Emotion == LineEmotion.Neutral
        ? When
        : $"{When} · {Line.Emotion.ToString().ToLowerInvariant()}";
    public string? Badge => Line.Trigger.ToString();
    public string BadgeKind => "dim";
    public bool HasBadge => true;
    public bool HasDetail => true;
    public bool ChipGood => false;
    public bool ChipOk => false;
    public bool ChipWarn => false;
    public bool ChipBad => false;
    public bool ChipDim => true;

    public override string ToString() => $"[{Detail}]   {Line.Text}";
}

/// <summary>A spawn point as the user sees it: place name, area, and how many mods trust it.</summary>
public sealed class LocationItem(SpawnLocation location) : IPickerRow
{
    public SpawnLocation Location { get; } = location;

    private bool NeedsNothing =>
        Location.RequiredPlugin.Equals("Skyrim.esm", StringComparison.OrdinalIgnoreCase);

    public string Display => Location.Display;

    public string? Detail
    {
        get
        {
            var proof = Location.Popularity > 1
                ? $"{Location.Popularity} mods put an NPC here"
                : "1 mod puts an NPC here";
            return NeedsNothing ? $"{proof} · base game only" : $"{proof} · needs {Location.RequiredPlugin}";
        }
    }

    // A place that costs the downloader nothing is the one worth spotting at a glance.
    public string? Badge => NeedsNothing ? "BASE GAME" : "NEEDS A MOD";
    public string BadgeKind => NeedsNothing ? "good" : "dim";
    public bool HasBadge => true;
    public bool HasDetail => true;
    public bool ChipGood => NeedsNothing;
    public bool ChipOk => false;
    public bool ChipWarn => false;
    public bool ChipBad => false;
    public bool ChipDim => !NeedsNothing;

    public override string ToString() => $"{Display}   —   {Detail}";
}

/// <summary>
/// A RaceMenu head export the user can pick for the face.
///
/// The label states up front whether the build will actually accept it. These are the same
/// conditions FollowerBuilder enforces (FACE_EXPORT_MISSING / FACE_PRESET_MISSING), checked
/// here so a face can never look fine at step 2 and then fail at step 7.
/// </summary>
public sealed class FaceItem(CharGenExport export) : IPickerRow
{
    public CharGenExport Export { get; } = export;

    /// <summary>Null when the face is usable; otherwise why the build would reject it.</summary>
    public string? Blocker => Export.Blocker;

    public bool IsUsable => Export.IsUsable;

    public string Display => Export.Name;

    public string? Detail
    {
        get
        {
            if (Blocker is { } why) return why;
            var bits = new List<string>(Export.QualityNotes);
            if (Export.RequiredPlugins.Count > 0)
                bits.Add($"needs {Export.RequiredPlugins.Count} appearance mod(s)");
            if (bits.Count == 0) return "ready to build, nothing else needed";
            // Prefer the most important note first for the one-line detail.
            return string.Join(" · ", bits.Take(2));
        }
    }

    /// <summary>
    /// True when the jslot is slider-heavy with no sculpt deltas AND we have no verified head
    /// mesh shapes. RaceMenu Export Head bakes sliders into the NIF, so a sculpted/exported
    /// head with HeadShapeCount &gt; 0 is NOT slider-only even if jslot sculpt.data is empty.
    /// </summary>
    public bool SliderOnly =>
        Export.PresetAppearance is { SliderCount: >= 10, SculptedVertices: 0 }
        && Export.HeadShapeCount <= 0;

    public string? Badge => Blocker is not null ? "CANNOT BUILD"
        : Export.TintDdsPath is null ? "NO TINT"
        : SliderOnly ? "NO SCULPT"
        : "READY";

    public bool HasBadge => true;
    public bool HasDetail => true;

    public string BadgeKind => Blocker is not null ? "bad"
        : Export.TintDdsPath is null || SliderOnly ? "warn"
        : "good";

    public bool ChipGood => ChipClass.Good(BadgeKind);
    public bool ChipOk => false;
    public bool ChipWarn => ChipClass.Warn(BadgeKind);
    public bool ChipBad => ChipClass.Bad(BadgeKind);
    public bool ChipDim => false;

    public override string ToString() => $"{Display}   —   {Badge}: {Detail}";
}
