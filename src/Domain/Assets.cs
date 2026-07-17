namespace FollowerForge.Domain;

/// <summary>Where an asset file physically lives.</summary>
public enum AssetContainerKind
{
    /// <summary>Loose file deployed by Vortex (hardlink into game Data).</summary>
    Loose = 0,
    /// <summary>Inside a .bsa archive.</summary>
    Bsa = 1,
}

/// <summary>
/// One winning game asset (mesh/texture/sound/…), keyed by its Data-relative path
/// (lower-cased, backslash-separated — the game's own convention).
/// </summary>
public sealed record AssetFile
{
    public required string RelPath { get; init; }
    public required AssetContainerKind Container { get; init; }
    /// <summary>Staging mod folder (loose) or BSA file name (archive).</summary>
    public required string ContainerName { get; init; }
    /// <summary>Vortex staging mod folder that supplied the file, when known.</summary>
    public string? SourceMod { get; init; }
    public long Size { get; init; }
}

/// <summary>A discovered RaceMenu CharGen head export (NIF + tint DDS + optional preset).</summary>
public sealed record CharGenExport
{
    public required string Name { get; init; }
    public required string NifPath { get; init; }
    public string? TintDdsPath { get; init; }
    public string? JslotPath { get; init; }
    /// <summary>Plugins the jslot says the face needs (mods array), when a jslot matched.</summary>
    public IReadOnlyList<string> RequiredPlugins { get; init; } = [];
}
