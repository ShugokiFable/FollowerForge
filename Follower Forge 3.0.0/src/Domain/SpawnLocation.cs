namespace FollowerForge.Domain;

public enum LocationKind { Interior, Exterior }

/// <summary>
/// A proven follower spawn point, harvested from a mod that already places its own NPC there.
/// The point of the library: the user picks a place by name ("The Bannered Mare") instead of
/// typing coordinates, and the coordinates are ones a real mod author already shipped.
/// </summary>
public sealed record SpawnLocation
{
    /// <summary>Stable slug, e.g. "whiterun-the-bannered-mare". Used by profiles.</summary>
    public required string Id { get; init; }
    /// <summary>Friendly place name, e.g. "The Bannered Mare".</summary>
    public required string Name { get; init; }
    /// <summary>Broader area for grouping/search, e.g. "Whiterun" or "Skyrim".</summary>
    public string? Area { get; init; }
    public required LocationKind Kind { get; init; }

    /// <summary>Cell the ACHR is placed in ("123ABC:Skyrim.esm").</summary>
    public required string CellFormKey { get; init; }
    /// <summary>Worldspace for exterior placements.</summary>
    public string? WorldspaceFormKey { get; init; }
    public string? CellEditorId { get; init; }

    /// <summary>
    /// Exterior grid square, when the spot is an ordinary outdoor cell. Null means the
    /// reference lives in the worldspace's persistent cell — the safe place for a follower,
    /// because a persistent reference exists before its cell is ever loaded.
    /// </summary>
    public int? GridX { get; init; }
    public int? GridY { get; init; }

    /// <summary>
    /// True when Follower Forge can place a follower here without guessing engine structure:
    /// any interior, or an exterior persistent cell.
    /// </summary>
    public bool Placeable => Kind == LocationKind.Interior || GridX is null;

    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    /// <summary>Z rotation in radians, as stored by the engine.</summary>
    public float RotationZ { get; init; }

    /// <summary>
    /// Plugin that owns the cell — this is the only master a follower needs to spawn here.
    /// Usually Skyrim.esm, which is why most library entries add no new dependency.
    /// </summary>
    public required string RequiredPlugin { get; init; }

    /// <summary>Plugins observed placing their own NPC at this spot (the evidence).</summary>
    public IReadOnlyList<string> ProvenByPlugins { get; init; } = [];
    /// <summary>Vortex staging mods behind those plugins, when known.</summary>
    public IReadOnlyList<string> ProvenByMods { get; init; } = [];
    /// <summary>How many distinct mods place an NPC here — a decent proxy for "good spot".</summary>
    public int Popularity { get; init; }

    public string Display => Area is { Length: > 0 } a && !Name.Contains(a, StringComparison.OrdinalIgnoreCase)
        ? $"{Name} ({a})"
        : Name;
}

/// <summary>The serialized library plus provenance about how it was produced.</summary>
public sealed record LocationLibrary
{
    public required string GeneratedAtUtc { get; init; }
    public required int ScannedPlugins { get; init; }
    public required IReadOnlyList<SpawnLocation> Locations { get; init; }
}
