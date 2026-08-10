using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Read-only discovery of the Vortex + Skyrim SE environment. Nothing here writes anything.
/// </summary>
public sealed class VortexDiscovery(ILogger log)
{
    public const string GameId = "skyrimse";

    /// <summary>Well-known default game location. Only one of several candidates — see
    /// <see cref="GameRootResolver"/>, because plenty of people keep Steam off C:.</summary>
    public static string DefaultGameRoot => GameRootResolver.DefaultGameRoot;

    /// <param name="gameRootOverride">Explicit game root (CLI --game-path); wins over defaults.</param>
    public EnvironmentSnapshot Discover(string? gameRootOverride = null)
    {
        var warnings = new List<string>();

        var vortexGamePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vortex", GameId);
        if (!Directory.Exists(vortexGamePath))
            throw new DirectoryNotFoundException($"Vortex game folder not found: {vortexGamePath}");

        var stagingPath = Path.Combine(vortexGamePath, "mods");
        var profilesPath = Path.Combine(vortexGamePath, "profiles");

        var gameRoot = ResolveGameRoot(gameRootOverride, warnings);
        var dataPath = Path.Combine(gameRoot, "Data");

        var runtimePluginsTxt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Skyrim Special Edition", "plugins.txt");

        // Deployment manifest header: authoritative staging/target cross-check.
        string? method = null;
        long deployTime = 0;
        var manifestPath = DeploymentManifest.Locate(dataPath);
        if (manifestPath is null)
        {
            warnings.Add($"No {DeploymentManifest.FileName} in {dataPath} — is the modpack deployed?");
        }
        else
        {
            var header = DeploymentManifest.ReadHeader(manifestPath);
            method = header.DeploymentMethod;
            deployTime = header.DeploymentTimeUtcMs;
            if (header.GameId is not null && !string.Equals(header.GameId, GameId, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Deployment manifest gameId '{header.GameId}' != '{GameId}'.");
            if (header.StagingPath is not null &&
                !PathsEqual(header.StagingPath, stagingPath))
            {
                warnings.Add($"Manifest stagingPath '{header.StagingPath}' differs from '{stagingPath}' — using manifest value.");
                stagingPath = header.StagingPath;
            }
            if (header.TargetPath is not null && !PathsEqual(header.TargetPath, dataPath))
                warnings.Add($"Manifest targetPath '{header.TargetPath}' differs from resolved Data path '{dataPath}'.");
        }

        var (activeProfile, reason) = ActiveProfileDetector.Detect(profilesPath, runtimePluginsTxt, warnings);

        var enabledCount = 0;
        var loadOrderCount = 0;
        if (activeProfile is not null)
        {
            var profDir = Path.Combine(profilesPath, activeProfile);
            var pluginsTxt = Path.Combine(profDir, "plugins.txt");
            if (File.Exists(pluginsTxt))
                enabledCount = PluginLists.ParsePluginsTxt(pluginsTxt).Count(e => e.Enabled);
            var loadOrderTxt = Path.Combine(profDir, "loadorder.txt");
            if (File.Exists(loadOrderTxt))
                loadOrderCount = PluginLists.ParseLoadOrderTxt(loadOrderTxt).Count;
        }

        var stagingModCount = Directory.Exists(stagingPath)
            ? Directory.EnumerateDirectories(stagingPath).Count()
            : 0;

        var snapshot = new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Vortex,
            ManagerLabel = "Vortex",
            GameRootPath = gameRoot,
            GameDataPath = dataPath,
            PluginDataPath = dataPath,
            InstancePath = vortexGamePath,
            StagingPath = stagingPath,
            ProfilesPath = profilesPath,
            RuntimePluginsTxtPath = runtimePluginsTxt,
            ActiveProfileId = activeProfile,
            ActiveProfileReason = reason,
            DeploymentMethod = method,
            DeploymentTimeUtcMs = deployTime,
            EnabledPluginCount = enabledCount,
            LoadOrderCount = loadOrderCount,
            StagingModCount = stagingModCount,
            Warnings = warnings,
        };

        log.Information("Environment: game={Game}, profile={Profile}, {Enabled} enabled plugins, {Mods} staging mods",
            gameRoot, activeProfile, enabledCount, stagingModCount);
        return snapshot;
    }

    /// <summary>Builds the write guard for a snapshot: game + staging + whole Vortex tree.</summary>
    public static WriteGuard CreateGuard(EnvironmentSnapshot env)
    {
        var guard = new WriteGuard();
        guard.Protect(env.GameRootPath);
        guard.Protect(env.StagingPath);
        guard.Protect(env.InstancePath);
        return guard;
    }

    private string ResolveGameRoot(string? overridePath, List<string> warnings)
    {
        if (overridePath is not null && !GameRootResolver.HasData(overridePath))
            throw new DirectoryNotFoundException($"--game-path has no Data folder: {overridePath}");

        var found = GameRootResolver.Find(overridePath);
        if (found is not null) return found;

        throw new DirectoryNotFoundException(
            "Could not find Skyrim Special Edition on any drive (checked every Steam library). "
            + "Pass --game-path, or set FFORGE_GAME_PATH to the folder containing SkyrimSE.exe.");
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                      Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                      StringComparison.OrdinalIgnoreCase);
}
