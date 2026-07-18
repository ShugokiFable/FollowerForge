using System.Text.Json;
using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.AssetIndex;

/// <summary>
/// Finds RaceMenu CharGen head exports (SKSE\Plugins\CharGen\*.nif + .dds) and
/// matching presets (CharGen\Presets\*.jslot). Matching is fuzzy: "_head" suffix,
/// case and spacing differences are tolerated (both naming styles exist in the wild).
/// </summary>
public sealed class CharGenDiscovery(ILogger log)
{
    public const string CharGenRelDir = @"SKSE\Plugins\CharGen";

    public IReadOnlyList<CharGenExport> Discover(string gameDataPath)
    {
        var dir = Path.Combine(gameDataPath, CharGenRelDir);
        if (!Directory.Exists(dir))
        {
            log.Warning("No CharGen folder at {Dir}", dir);
            return [];
        }

        var presets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var presetsDir = Path.Combine(dir, "Presets");
        if (Directory.Exists(presetsDir))
        {
            foreach (var jslot in Directory.EnumerateFiles(presetsDir, "*.jslot"))
                presets[NormalizeKey(Path.GetFileNameWithoutExtension(jslot))] = jslot;
        }

        var exports = new List<CharGenExport>();
        foreach (var nif in Directory.EnumerateFiles(dir, "*.nif", SearchOption.TopDirectoryOnly))
        {
            var stem = Path.GetFileNameWithoutExtension(nif);
            var dds = Path.ChangeExtension(nif, ".dds");
            var key = NormalizeKey(stem);
            presets.TryGetValue(key, out var jslotPath);

            var requiredPlugins = jslotPath is not null
                ? ReadJslotPlugins(jslotPath)
                : [];

            exports.Add(new CharGenExport
            {
                Name = stem,
                NifPath = nif,
                TintDdsPath = File.Exists(dds) ? dds : null,
                JslotPath = jslotPath,
                RequiredPlugins = requiredPlugins,
            });
        }
        log.Information("CharGen discovery: {Count} head exports ({WithPreset} with matching jslot)",
            exports.Count, exports.Count(e => e.JslotPath is not null));
        return exports;
    }

    /// <summary>"A1_Nord_Natalie_head" and "A1_Nord_Natalie" must match the same preset.</summary>
    private static string NormalizeKey(string name)
    {
        var n = name.Trim();
        if (n.EndsWith("_head", StringComparison.OrdinalIgnoreCase)) n = n[..^5];
        return n.Replace(" ", "").ToLowerInvariant();
    }

    /// <summary>Reads the jslot "mods" array — the plugins the face requires.</summary>
    public IReadOnlyList<string> ReadJslotPlugins(string jslotPath)
    {
        try
        {
            using var stream = File.OpenRead(jslotPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
            {
                return mods.EnumerateArray()
                    .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            log.Warning("Unreadable jslot {Path}: {Error}", jslotPath, ex.Message);
        }
        return [];
    }
}
