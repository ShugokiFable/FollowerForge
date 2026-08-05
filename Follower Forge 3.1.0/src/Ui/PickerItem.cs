using Avalonia.Media;
using FollowerForge.Domain;

namespace FollowerForge.Ui;

/// <summary>
/// What every wizard row shows: a name, one line of why it matters, and an optional chip.
/// One DataTemplate is written against this, so faces, places and records all read the same.
/// </summary>
public interface IPickerRow
{
    string Display { get; }
    string? Detail { get; }
    string? Badge { get; }
    IBrush BadgeBrush { get; }
    IBrush BadgeText { get; }
    bool HasBadge { get; }
    bool HasDetail { get; }
}

/// <summary>Chip palette, kept beside the rows that use it so the two never drift apart.</summary>
internal static class Chips
{
    internal static readonly IBrush Good = new SolidColorBrush(Color.Parse("#8fd694"));
    internal static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#7fc4e8"));
    internal static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#e8b96a"));
    internal static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#e88b7f"));
    internal static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#2e2e38"));
    internal static readonly IBrush OnLight = new SolidColorBrush(Color.Parse("#12121a"));
    internal static readonly IBrush OnDark = new SolidColorBrush(Color.Parse("#9aa0aa"));

    /// <summary>
    /// Chips are resolved in code, not XAML: a style selector cannot key off a data value, and
    /// binding a colour string to a brush leans on an implicit conversion that fails quietly.
    /// </summary>
    internal static IBrush Fill(string kind) => kind switch
    {
        "good" => Good,
        "ok" => Ok,
        "warn" => Warn,
        "bad" => Bad,
        _ => Dim,
    };

    internal static IBrush Ink(string kind) => kind is "good" or "ok" or "warn" or "bad"
        ? OnLight
        : OnDark;
}

/// <summary>
/// One choice in a wizard list. The row template reads Display, Detail and Badge; ToString()
/// is kept for search, logs and the summary. A FormKey is never shown — that stays on the
/// object for the build engine.
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

    /// <summary>"good", "ok", "warn", "bad" or "dim" — see <see cref="Chips"/>.</summary>
    public string BadgeKind { get; } = badgeKind ?? "dim";

    public bool HasBadge => Badge is { Length: > 0 };
    public bool HasDetail => Detail is { Length: > 0 };
    public IBrush BadgeBrush => Chips.Fill(BadgeKind);
    public IBrush BadgeText => Chips.Ink(BadgeKind);

    public override string ToString() => Detail is { Length: > 0 } d ? $"{Display}   —   {d}" : Display;
}

/// <summary>A relationship with someone else in the world, shown by name rather than FormKey.</summary>
public sealed class KinItem(NpcRelationship relationship, string displayName) : IPickerRow
{
    public NpcRelationship Relationship { get; } = relationship;
    public string DisplayName { get; } = displayName;

    public string Display => DisplayName;
    public string? Detail => $"she treats them as {Relationship.Rank}";
    public string? Badge => Relationship.Rank.ToString();
    public bool HasBadge => true;
    public bool HasDetail => true;
    public IBrush BadgeBrush => Chips.Fill("dim");
    public IBrush BadgeText => Chips.Ink("dim");

    public override string ToString() => $"{DisplayName}   —   her {Relationship.Rank}";
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
    public bool HasBadge => true;
    public bool HasDetail => true;
    public IBrush BadgeBrush => Chips.Fill("dim");
    public IBrush BadgeText => Chips.Ink("dim");

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
    public bool HasBadge => true;
    public bool HasDetail => true;
    public IBrush BadgeBrush => Chips.Fill(NeedsNothing ? "good" : "dim");
    public IBrush BadgeText => Chips.Ink(NeedsNothing ? "good" : "dim");

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
            var bits = new List<string>();
            if (Export.TintDdsPath is null) bits.Add("no tint — her face will be grey");
            if (Export.RequiredPlugins.Count > 0)
                bits.Add($"needs {Export.RequiredPlugins.Count} appearance mod(s)");
            return bits.Count == 0 ? "ready to build, nothing else needed" : string.Join(" · ", bits);
        }
    }

    public string? Badge => Blocker is not null ? "CANNOT BUILD"
        : Export.TintDdsPath is null ? "NO TINT"
        : "READY";

    public bool HasBadge => true;
    public bool HasDetail => true;

    private string Kind => Blocker is not null ? "bad" : Export.TintDdsPath is null ? "warn" : "good";
    public IBrush BadgeBrush => Chips.Fill(Kind);
    public IBrush BadgeText => Chips.Ink(Kind);

    public override string ToString() => $"{Display}   —   {Badge}: {Detail}";
}
