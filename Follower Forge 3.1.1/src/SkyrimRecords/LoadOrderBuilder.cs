using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Builds the game's actual load order from the active Vortex profile:
/// loadorder.txt gives the order; plugins.txt gives enablement; the base game
/// masters, Creation Club plugins and _ResourcePack.esl are implicitly enabled.
/// </summary>
public sealed class LoadOrderBuilder(ILogger log)
{
    public sealed record BuiltLoadOrder(
        ILoadOrder<IModListing<ISkyrimModGetter>> LoadOrder,
        IReadOnlyList<LoadOrderEntry> Entries,
        IReadOnlyList<string> MissingPlugins);

    /// <summary>Computes the effective entry list without opening any plugin.</summary>
    public (IReadOnlyList<LoadOrderEntry> Entries, IReadOnlyList<string> Missing) BuildEntryList(
        EnvironmentSnapshot env)
    {
        if (env.ActiveProfileId is null)
            throw new InvalidOperationException("No active Vortex profile detected.");

        var profDir = Path.Combine(env.ProfilesPath, env.ActiveProfileId);
        var loadOrderTxt = Path.Combine(profDir, "loadorder.txt");
        var pluginsTxt = Path.Combine(profDir, "plugins.txt");
        if (!File.Exists(loadOrderTxt) || !File.Exists(pluginsTxt))
            throw new FileNotFoundException($"Profile {env.ActiveProfileId} lacks loadorder.txt/plugins.txt");

        var order = PluginLists.ParseLoadOrderTxt(loadOrderTxt);
        var enabled = PluginLists.ParsePluginsTxt(pluginsTxt)
            .Where(e => e.Enabled)
            .Select(e => e.PluginFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = new List<LoadOrderEntry>(order.Count);
        var missing = new List<string>();
        foreach (var name in order)
        {
            var isEnabled = enabled.Contains(name) || IsImplicitlyEnabled(name);
            if (isEnabled && !File.Exists(Path.Combine(env.GameDataPath, name)))
            {
                missing.Add(name);
                isEnabled = false;
            }
            entries.Add(new LoadOrderEntry(name, isEnabled, entries.Count));
        }
        if (missing.Count > 0)
            log.Warning("{Count} enabled plugins missing from Data: {First}…", missing.Count, missing[0]);
        return (entries, missing);
    }

    /// <summary>Opens the enabled plugins with Mutagen (lazy binary overlays).</summary>
    public BuiltLoadOrder Build(EnvironmentSnapshot env)
    {
        var (entries, missing) = BuildEntryList(env);
        var listings = entries
            .Where(e => e.Enabled)
            .Select(e => new LoadOrderListing(ModKey.FromFileName(e.PluginFileName), enabled: true));

        log.Information("Importing load order: {Count} enabled plugins…", entries.Count(e => e.Enabled));
        var lo = LoadOrder.Import<ISkyrimModGetter>(
            new DirectoryPath(env.GameDataPath), listings, GameRelease.SkyrimSE);
        return new BuiltLoadOrder(lo, entries, missing);
    }

    public static bool IsImplicitlyEnabled(string pluginFileName) =>
        PluginLists.ImplicitBaseMasters.Contains(pluginFileName, StringComparer.OrdinalIgnoreCase)
        || pluginFileName.StartsWith("cc", StringComparison.OrdinalIgnoreCase)
           && (pluginFileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
               || pluginFileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
        || string.Equals(pluginFileName, "_ResourcePack.esl", StringComparison.OrdinalIgnoreCase);
}
