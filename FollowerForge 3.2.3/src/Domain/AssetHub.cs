namespace FollowerForge.Domain;

/// <summary>What part of a follower's appearance an asset covers.</summary>
public enum FaceAssetKind
{
    SkinDiffuse,      // the actual skin colour — the one thing free hubs cannot supply
    SkinSupportMap,   // _msn normal, _s specular, _sk subsurface, detail maps
    Hair,
    Eyes,
    Brows,
    Mouth,
    HeadMesh,
    Other,
}

/// <summary>A free-to-use modder's resource that can stand in for someone else's assets.</summary>
public sealed record AssetHub
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string Url { get; init; }
    /// <summary>Plugin the hub ships, when it has one.</summary>
    public string? PluginName { get; init; }
    /// <summary>A file that proves the hub is installed.</summary>
    public required string MarkerPath { get; init; }
    /// <summary>What it can genuinely cover — deliberately narrow.</summary>
    public required IReadOnlyList<FaceAssetKind> Covers { get; init; }
    public required string Covering { get; init; }
    public required string NotCovering { get; init; }

    public bool Installed { get; init; }
    public string? SourceMod { get; init; }
}

/// <summary>One texture the follower's face uses, and where it came from.</summary>
public sealed record FaceAsset
{
    public required string RelPath { get; init; }
    public required FaceAssetKind Kind { get; init; }
    public required bool Resolved { get; init; }
    /// <summary>Vortex staging mod that supplies it, when known.</summary>
    public string? SourceMod { get; init; }
    public string? Container { get; init; }
    /// <summary>Id of a free hub that could supply this instead, if any.</summary>
    public string? CoverableByHub { get; init; }
    /// <summary>Set when the build actually redirected this path (own-hub mode).</summary>
    public string? RetargetedTo { get; init; }
}

/// <summary>How a follower's appearance assets should be handled.</summary>
public enum HubMode
{
    /// <summary>Reference whatever the user has; list it as a requirement. Copies nothing.</summary>
    ReferenceInstalled = 0,
    /// <summary>Prefer installed free modder's resources where they genuinely cover a slot.</summary>
    FreeHubs = 1,
    /// <summary>Copy the used assets into the author's own hub package (needs a declaration).</summary>
    OwnHub = 2,
}
