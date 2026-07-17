using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Orchestrates a full catalogue rebuild: plugins, winning records, loose files, BSA contents.
/// All inputs are read-only; the only write target is the catalogue database.
/// </summary>
public sealed class CatalogBuilder(ILogger log)
{
    public static string DefaultDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FollowerForge", "catalog.db");

    public sealed record CatalogSummary(
        long Plugins, long Records, long Assets, TimeSpan Elapsed);

    public CatalogSummary Build(EnvironmentSnapshot env, string? dbPath = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        dbPath ??= DefaultDbPath;

        var guard = VortexDiscovery.CreateGuard(env);
        guard.EnsureWritable(dbPath);

        // Plugin file → Vortex staging mod (root-level manifest entries).
        var manifestPath = DeploymentManifest.Locate(env.GameDataPath);
        var pluginSourceMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (manifestPath is not null)
        {
            foreach (var entry in DeploymentManifest.ReadFiles(manifestPath))
            {
                if (!entry.RelPath.Contains('\\') && IsPluginFile(entry.RelPath))
                    pluginSourceMods[entry.RelPath] = entry.SourceMod;
            }
        }

        var builder = new LoadOrderBuilder(log);
        var built = builder.Build(env);

        using var db = new CatalogDb(dbPath, log);
        db.ReplacePlugins(built.Entries.Select(e =>
            (e, pluginSourceMods.TryGetValue(e.PluginFileName, out var src) ? src : null)));

        var indexer = new RecordIndexer(log);
        var recordCount = db.ReplaceRecords(indexer.EnumerateWinningRecords(built, pluginSourceMods));

        // BSA entries first, loose files last: same rel_path ⇒ loose wins (the game's rule).
        long assetCount = 0;
        var archive = new ArchiveIndexer(log);
        var loose = new LooseFileIndexer(log);
        IEnumerable<AssetFile> allAssets = archive.Enumerate(env.GameDataPath);
        if (manifestPath is not null)
            allAssets = allAssets.Concat(loose.Enumerate(manifestPath));
        assetCount = db.ReplaceAssets(allAssets);

        db.SetMeta("deployment_time", env.DeploymentTimeUtcMs.ToString());
        db.SetMeta("indexed_at_utc", DateTime.UtcNow.ToString("O"));
        db.SetMeta("profile_id", env.ActiveProfileId ?? "");
        db.SetMeta("plugin_count", built.Entries.Count.ToString());

        sw.Stop();
        log.Information("Catalogue complete in {Elapsed}: {Records} records, {Assets} assets",
            sw.Elapsed, recordCount, assetCount);
        return new CatalogSummary(built.Entries.Count, recordCount, assetCount, sw.Elapsed);
    }

    /// <summary>True when the stored catalogue matches the current deployment state.</summary>
    public static bool IsFresh(EnvironmentSnapshot env, string? dbPath = null)
    {
        dbPath ??= DefaultDbPath;
        if (!File.Exists(dbPath)) return false;
        using var db = new CatalogDb(dbPath, Serilog.Log.Logger);
        return db.GetMeta("deployment_time") == env.DeploymentTimeUtcMs.ToString();
    }

    private static bool IsPluginFile(string name) =>
        name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase);
}
