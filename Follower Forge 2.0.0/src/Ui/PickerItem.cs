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

/// <summary>A RaceMenu head export the user can pick for the face.</summary>
public sealed class FaceItem(CharGenExport export)
{
    public CharGenExport Export { get; } = export;

    public override string ToString()
    {
        var bits = new List<string>();
        bits.Add(Export.TintDdsPath is null ? "NO tint (face will be grey)" : "tint OK");
        if (Export.RequiredPlugins.Count > 0) bits.Add($"{Export.RequiredPlugins.Count} appearance mods");
        return $"{Export.Name}   —   {string.Join(", ", bits)}";
    }
}
