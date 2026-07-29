using FollowerForge.Domain;

namespace FollowerForge.Ui;

/// <summary>
/// One choice in a wizard list. ToString() is what the user reads, so the UI never has to show
/// a FormKey — that stays on the object for the build engine.
/// </summary>
public sealed class PickerItem(string display, string formKey, string? detail = null)
{
    public string Display { get; } = display;
    public string FormKey { get; } = formKey;
    public string? Detail { get; } = detail;

    public override string ToString() => Detail is { Length: > 0 } d ? $"{Display}   —   {d}" : Display;
}

/// <summary>A relationship with someone else in the world, shown by name rather than FormKey.</summary>
public sealed class KinItem(NpcRelationship relationship, string displayName)
{
    public NpcRelationship Relationship { get; } = relationship;
    public string DisplayName { get; } = displayName;

    public override string ToString() => $"{DisplayName}   —   her {Relationship.Rank}";
}

/// <summary>One custom line, shown as when-she-says-it followed by the words.</summary>
public sealed class LineItem(DialogueLine line)
{
    public DialogueLine Line { get; } = line;

    public override string ToString()
    {
        var when = Line.Trigger switch
        {
            DialogueTrigger.Hello => "on greeting",
            DialogueTrigger.Goodbye => "on parting",
            DialogueTrigger.Idle => "idle",
            _ => $"topic “{Line.Prompt}”",
        };
        var mood = Line.Emotion == LineEmotion.Neutral ? "" : $", {Line.Emotion.ToString().ToLowerInvariant()}";
        return $"[{when}{mood}]   {Line.Text}";
    }
}

/// <summary>A spawn point as the user sees it: place name, area, and how many mods trust it.</summary>
public sealed class LocationItem(SpawnLocation location)
{
    public SpawnLocation Location { get; } = location;

    public override string ToString()
    {
        var proof = Location.Popularity > 1
            ? $"used by {Location.Popularity} mods"
            : "used by 1 mod";
        var needs = Location.RequiredPlugin.Equals("Skyrim.esm", StringComparison.OrdinalIgnoreCase)
            ? "no extra requirement"
            : $"needs {Location.RequiredPlugin}";
        return $"{Location.Display}   —   {proof}, {needs}";
    }
}

/// <summary>
/// A RaceMenu head export the user can pick for the face.
///
/// The label states up front whether the build will actually accept it. These are the same
/// conditions FollowerBuilder enforces (FACE_EXPORT_MISSING / FACE_PRESET_MISSING), checked
/// here so a face can never look fine at step 2 and then fail at step 7.
/// </summary>
public sealed class FaceItem(CharGenExport export)
{
    public CharGenExport Export { get; } = export;

    /// <summary>Null when the face is usable; otherwise why the build would reject it.</summary>
    public string? Blocker => Export.Blocker;

    public bool IsUsable => Export.IsUsable;

    public override string ToString()
    {
        if (Blocker is { } why) return $"{Export.Name}   —   CANNOT BUILD: {why}";

        var bits = new List<string> { "ready" };
        if (Export.TintDdsPath is null) bits.Add("no tint — her face will be grey");
        if (Export.RequiredPlugins.Count > 0) bits.Add($"needs {Export.RequiredPlugins.Count} appearance mods");
        return $"{Export.Name}   —   {string.Join(", ", bits)}";
    }
}
