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
    /// <summary>
    /// Bumped whenever the set of indexed record types changes, so an older catalogue is
    /// rebuilt instead of silently missing the new types. 2 = Ammo added in 3.3.0.
    /// </summary>
    internal const string IndexVersion = "2";

    public static string DefaultDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FollowerForge", "catalog.db");

    public sealed record CatalogSummary(
        long Plugins, long Records, long Assets, TimeSpan Elapsed);

    public CatalogSummary Build(EnvironmentSnapshot env, string? dbPath = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        dbPath ??= DefaultDbPath;

        var guard = EnvironmentDiscovery.CreateGuard(env);
        guard.EnsureWritable(dbPath);

        // Plugin file → source mod folder (Vortex deployment manifest or MO2 mod name).
        var pluginSourceMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? manifestPath = null;
        if (env.Manager == Domain.ModManagerKind.Vortex)
        {
            manifestPath = DeploymentManifest.Locate(env.GameDataPath);
            if (manifestPath is not null)
            {
                foreach (var entry in DeploymentManifest.ReadFiles(manifestPath))
                {
                    if (!entry.RelPath.Contains('\\') && IsPluginFile(entry.RelPath))
                        pluginSourceMods[entry.RelPath] = entry.SourceMod;
                }
            }
        }
        else
        {
            foreach (var (modName, dir) in Mo2PathResolver.EnumerateModDirsLowestFirst(env))
            {
                foreach (var plugin in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(plugin);
                    if (IsPluginFile(name))
                        pluginSourceMods[name] = modName; // later mods overwrite → higher priority
                }
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
        IEnumerable<AssetFile> allAssets = archive.Enumerate(env.GameDataPath);
        if (env.Manager == Domain.ModManagerKind.Mo2)
        {
            // MO2 BSAs also live inside mod folders; scan those too.
            allAssets = allAssets.Concat(archive.EnumerateModFolders(env));
            allAssets = allAssets.Concat(new Mo2LooseFileIndexer(log).Enumerate(env));
        }
        else if (manifestPath is not null)
        {
            allAssets = allAssets.Concat(new LooseFileIndexer(log).Enumerate(manifestPath));
        }
        assetCount = db.ReplaceAssets(allAssets);

        db.SetMeta("deployment_time", env.DeploymentTimeUtcMs.ToString());
        db.SetMeta("indexed_at_utc", DateTime.UtcNow.ToString("O"));
        db.SetMeta("profile_id", env.ActiveProfileId ?? "");
        db.SetMeta("manager", env.Manager.ToString());
        db.SetMeta("index_version", IndexVersion);
        db.SetMeta("instance_path", CanonicalPath(env.InstancePath));
        db.SetMeta("staging_path", CanonicalPath(env.StagingPath));
        db.SetMeta("plugin_count", built.Entries.Count.ToString());

        sw.Stop();
        log.Information("Catalogue complete in {Elapsed}: {Records} records, {Assets} assets",
            sw.Elapsed, recordCount, assetCount);
        return new CatalogSummary(built.Entries.Count, recordCount, assetCount, sw.Elapsed);
    }

    /// <summary>
    /// True when the stored catalogue matches the exact manager, profile, instance, staging root,
    /// and deployment state. Profile/instance checks prevent two MO2 setups with matching file
    /// timestamps from reusing each other's catalogue.
    /// </summary>
    public static bool IsFresh(EnvironmentSnapshot env, string? dbPath = null)
    {
        dbPath ??= DefaultDbPath;
        if (!File.Exists(dbPath)) return false;
        using var db = new CatalogDb(dbPath, Serilog.Log.Logger);
        // Manager mismatch (e.g. a hung MO2/shim index after we switched back to Vortex) must rebuild.
        if (!string.Equals(db.GetMeta("manager"), env.Manager.ToString(), StringComparison.Ordinal))
            return false;
        // A catalogue built by a version that indexed fewer record types is not "fresh" just
        // because the deployment has not changed: 3.3.0 added Ammo, and without this every
        // existing user would open the arrows list and find it empty.
        if (!string.Equals(db.GetMeta("index_version"), IndexVersion, StringComparison.Ordinal))
            return false;
        if (!string.Equals(
                db.GetMeta("profile_id") ?? string.Empty,
                env.ActiveProfileId ?? string.Empty,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (!PathsMatch(db.GetMeta("instance_path"), env.InstancePath)) return false;
        if (!PathsMatch(db.GetMeta("staging_path"), env.StagingPath)) return false;
        return db.GetMeta("deployment_time") == env.DeploymentTimeUtcMs.ToString();
    }

    private static bool PathsMatch(string? stored, string current) =>
        !string.IsNullOrWhiteSpace(stored)
        && string.Equals(CanonicalPath(stored), CanonicalPath(current), StringComparison.OrdinalIgnoreCase);

    private static string CanonicalPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsPluginFile(string name) =>
        name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase);
}
