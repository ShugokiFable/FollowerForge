namespace FollowerForge.Domain;

/// <summary>
/// Result of read-only environment discovery (Phase 0). Everything the rest of the
/// pipeline needs to know about the machine, resolved once and passed around.
/// </summary>
public sealed record EnvironmentSnapshot
{
    public required string GameRootPath { get; init; }
    public required string GameDataPath { get; init; }
    public required string VortexGamePath { get; init; }
    public required string StagingPath { get; init; }
    public required string ProfilesPath { get; init; }
    public required string RuntimePluginsTxtPath { get; init; }
    public string? ActiveProfileId { get; init; }
    public string? ActiveProfileReason { get; init; }
    public string? DeploymentMethod { get; init; }
    public long DeploymentTimeUtcMs { get; init; }
    public int DeployedFileCount { get; init; }
    public int EnabledPluginCount { get; init; }
    public int LoadOrderCount { get; init; }
    public int StagingModCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>A single entry of the game load order, in load order.</summary>
public sealed record LoadOrderEntry(string PluginFileName, bool Enabled, int Index);
