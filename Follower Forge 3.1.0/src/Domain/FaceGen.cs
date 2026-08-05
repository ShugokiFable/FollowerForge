namespace FollowerForge.Domain;

/// <summary>Outcome of the FaceGen dirty-swap for one follower.</summary>
public sealed record FaceGenResult
{
    public required bool Success { get; init; }
    /// <summary>True when conversion was declined and a CK handoff report was written instead.</summary>
    public bool NeedsCreationKit { get; init; }
    public string? FaceGeomPath { get; init; }
    public string? FaceTintPath { get; init; }
    public string? CkHandoffPath { get; init; }
    public IReadOnlyList<string> ShapeNames { get; init; } = [];
    /// <summary>Texture paths referenced by the head NIF and whether each resolves in the asset index.</summary>
    public IReadOnlyList<TextureResolution> Textures { get; init; } = [];
    public IReadOnlyList<ValidationFinding> Findings { get; init; } = [];
}

public sealed record TextureResolution(string Path, bool Resolved, string? Container);

/// <summary>Data for the Creation Kit handoff when automatic FaceGen conversion is unsafe.</summary>
public sealed record CkHandoff(
    string PluginName,
    string ActorEditorId,
    string NpcFormKey,
    string ExpectedFaceGeomPath,
    string ExpectedFaceTintPath,
    string Reason,
    IReadOnlyList<string> Steps);
