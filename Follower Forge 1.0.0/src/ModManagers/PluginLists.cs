using FollowerForge.Domain;

namespace FollowerForge.ModManagers;

/// <summary>Parsers for Vortex-generated plugins.txt / loadorder.txt.</summary>
public static class PluginLists
{
    /// <summary>
    /// Parses a Vortex plugins.txt: '#' lines are comments, a leading '*' marks an enabled
    /// plugin, a bare name is present-but-disabled. Order is load order (post-ESM sorting
    /// is Vortex's job; the file is already in final order).
    /// </summary>
    public static IReadOnlyList<LoadOrderEntry> ParsePluginsTxt(string path)
    {
        var result = new List<LoadOrderEntry>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var enabled = line.StartsWith('*');
            var name = enabled ? line[1..].Trim() : line;
            if (name.Length == 0) continue;
            result.Add(new LoadOrderEntry(name, enabled, result.Count));
        }
        return result;
    }

    /// <summary>Parses loadorder.txt (bare plugin names, full order).</summary>
    public static IReadOnlyList<string> ParseLoadOrderTxt(string path)
    {
        var result = new List<string>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            result.Add(line);
        }
        return result;
    }

    /// <summary>
    /// The game's base masters that never appear in plugins.txt but are always loaded first.
    /// Creation Club content does appear in Vortex's lists, so it is not included here.
    /// </summary>
    public static readonly IReadOnlyList<string> ImplicitBaseMasters =
    [
        "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm",
    ];
}
