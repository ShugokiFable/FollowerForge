namespace FollowerForge.Domain;

/// <summary>Top-level description of a generated follower (manifest.json).</summary>
public sealed record FollowerManifest
{
    public required string Name { get; init; }
    public required string PluginName { get; init; }
    public required string NpcFormKey { get; init; }
    public required string NpcEditorId { get; init; }
    public required string PlacedFormKey { get; init; }
    public required OutputStrategy Strategy { get; init; }
    public required IReadOnlyList<string> Masters { get; init; }
    public string? RaceFormKey { get; init; }
    public string? VoiceFormKey { get; init; }
    public string? CombatStyleFormKey { get; init; }
    public bool CombatStyleCloned { get; init; }
    public bool HasFaceGen { get; init; }
    public string GeneratedBy { get; init; } = "Follower Forge";
    /// <summary>Deterministic: derived from the profile's fixed build timestamp, not wall-clock.</summary>
    public required string GeneratedAtUtc { get; init; }
}

/// <summary>One asset the follower relies on and where it came from (source-assets.json).</summary>
public sealed record SourceAssetEntry(
    string RelPath,
    string Container,        // "Loose" | "Bsa"
    string? SourceMod,       // Vortex staging folder
    bool Copied,             // true only in Portable Standalone mode
    string Role);            // e.g. "FaceGen NIF", "tint DDS", "voice", "body texture"

public sealed record SourceAssetsManifest(
    OutputStrategy Strategy,
    string? RedistributionPermission,
    IReadOnlyList<SourceAssetEntry> Assets);

/// <summary>Master requirement + the reason it is needed (dependency-report.json).</summary>
public sealed record DependencyEntry(string Plugin, string Reason, string? SourceMod);

public sealed record DependencyReport(
    IReadOnlyList<DependencyEntry> RequiredMasters,
    IReadOnlyList<string> RecommendedMods,   // asset mods the face/body needs
    IReadOnlyList<string> Warnings);
