namespace FollowerForge.Domain;

/// <summary>Which mod manager supplied the active profile and file provenance.</summary>
public enum ModManagerKind
{
    /// <summary>Vortex — plugins and loose files live in deployed game Data (hardlinks).</summary>
    Vortex = 0,
    /// <summary>Mod Organizer 2 — plugins and loose files live under the instance mods folders.</summary>
    Mo2 = 1,
}

/// <summary>
/// Result of read-only environment discovery. Everything the rest of the pipeline needs to know
/// about the machine, resolved once and passed around.
/// </summary>
public sealed record EnvironmentSnapshot
{
    public required ModManagerKind Manager { get; init; }
    /// <summary>Human label for logs and the wizard status line.</summary>
    public required string ManagerLabel { get; init; }
    public required string GameRootPath { get; init; }
    public required string GameDataPath { get; init; }
    /// <summary>
    /// Directory Mutagen should open for plugins. Vortex: same as <see cref="GameDataPath"/>.
    /// MO2: a FollowerForge-owned hardlink view of enabled plugins (never writes into the game).
    /// Call <c>Mo2DataView.Ensure</c> before reading under MO2.
    /// </summary>
    public required string PluginDataPath { get; init; }
    /// <summary>
    /// Directory that currently holds load-order plugin files for master expansion and Mutagen
    /// open-by-filename. Vortex equals <see cref="GameDataPath"/>. MO2 equals
    /// <see cref="PluginDataPath"/> (virtual view), never bare Steam Data alone.
    /// </summary>
    public string PluginReadRoot =>
        Manager == ModManagerKind.Mo2 ? PluginDataPath : GameDataPath;
    /// <summary>Manager instance root (Vortex game folder or MO2 instance folder).</summary>
    public required string InstancePath { get; init; }
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
    /// <summary>
    /// MO2 only: enabled mod folder names in priority order (last entry wins conflicts).
    /// Empty for Vortex.
    /// </summary>
    public IReadOnlyList<string> Mo2ModPriority { get; init; } = [];
    /// <summary>
    /// MO2 only: absolute path to the Overwrite folder from Settings/overwrite_directory
    /// (defaults to InstancePath\overwrite). Empty for Vortex.
    /// </summary>
    public string? Mo2OverwritePath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>A single entry of the game load order, in load order.</summary>
public sealed record LoadOrderEntry(string PluginFileName, bool Enabled, int Index);
