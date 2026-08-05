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
    /// <summary>Record-side appearance parsed from the matching preset.</summary>
    public AppearanceSpec? PresetAppearance { get; init; }

    /// <summary>
    /// Why a build would reject this face, or null when it is usable. Single source of truth so
    /// the picker cannot say "ready" for something the builder then refuses — these are exactly
    /// the FACE_EXPORT_MISSING / FACE_PRESET_MISSING conditions.
    /// </summary>
    public string? Blocker =>
        !File.Exists(NifPath)
            ? "head export is gone from disk"
            : PresetAppearance is null
                ? "no matching RaceMenu preset (.jslot) — re-save the preset under the same name"
                : null;

    public bool IsUsable => Blocker is null;
}
