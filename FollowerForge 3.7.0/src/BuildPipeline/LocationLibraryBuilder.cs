using System.Text.Json;
using System.Text.Json.Serialization;
using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Produces and loads the spawn-location library: the list of named, already-proven places a
/// follower can be put, harvested from the installed mods.
/// </summary>
public sealed class LocationLibraryBuilder(ILogger log)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FollowerForge", "locations.json");

    public LocationLibrary Build(EnvironmentSnapshot env, string? outputPath = null, string? dbPath = null)
    {
        outputPath ??= DefaultPath;
        var guard = EnvironmentDiscovery.CreateGuard(env);
        guard.EnsureWritable(outputPath);

        dbPath ??= CatalogBuilder.DefaultDbPath;
        CatalogDb? catalog = File.Exists(dbPath) ? new CatalogDb(dbPath, log) : null;
        if (catalog is null)
            log.Warning("No catalogue at {Db}; place names will fall back to EditorIDs. Run 'FollowerForge.Cli.exe index' for best results.", dbPath);

        try
        {
            // Plugin → Vortex staging mod, so the library can say which mod proved each spot.
            var pluginSourceMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var manifest = DeploymentManifest.Locate(env.GameDataPath);
            if (manifest is not null)
            {
                foreach (var entry in DeploymentManifest.ReadFiles(manifest))
                {
                    if (!entry.RelPath.Contains('\\') && IsPluginFile(entry.RelPath))
                        pluginSourceMods[entry.RelPath] = entry.SourceMod;
                }
            }

            var built = new LoadOrderBuilder(log).Build(env);
            var locations = new LocationScanner(log).Scan(
                built,
                cellNameLookup: catalog is null ? null : fk => catalog.GetRecord(fk)?.DisplayName,
                pluginSourceMods: pluginSourceMods);

            var library = new LocationLibrary
            {
                GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
                ScannedPlugins = built.Entries.Count(e => e.Enabled),
                Locations = locations,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(library, Json));
            log.Information("Location library written: {Path} ({Count} places)", outputPath, locations.Count);
            return library;
        }
        finally
        {
            catalog?.Dispose();
        }
    }

    /// <summary>Drops a stale library so the next Load rebuilds it from the current mods.</summary>
    public static void Invalidate(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A locked file just means the old library stays; not worth failing a build over.
        }
    }

    public static LocationLibrary? Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<LocationLibrary>(File.ReadAllText(path), Json);
    }

    /// <summary>Name/area/mod search over the library, best matches first.</summary>
    public static IReadOnlyList<SpawnLocation> Search(LocationLibrary library, string? text, int limit = 40)
    {
        IEnumerable<SpawnLocation> q = library.Locations;
        if (!string.IsNullOrWhiteSpace(text))
        {
            q = q.Where(l =>
                l.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (l.Area?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.CellEditorId?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || l.ProvenByMods.Any(m => m.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }
        return q.Take(limit).ToList();
    }

    private static bool IsPluginFile(string name) =>
        name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase);
}
