using FollowerForge.Domain;

namespace FollowerForge.ModManagers;

/// <summary>
/// Resolves files inside an MO2 instance the way the game would: enabled mods in
/// <see cref="EnvironmentSnapshot.Mo2ModPriority"/> order (last wins), then game Data.
/// </summary>
public static class Mo2PathResolver
{
    public const string OverwriteToken = "__mo2_overwrite__";

    public static string? ResolvePlugin(EnvironmentSnapshot env, string pluginFileName)
    {
        foreach (var modDir in EnumerateModDirsHighestFirst(env))
        {
            var candidate = Path.Combine(modDir, pluginFileName);
            if (File.Exists(candidate)) return candidate;
        }
        var inData = Path.Combine(env.GameDataPath, pluginFileName);
        return File.Exists(inData) ? inData : null;
    }

    /// <summary>Data-relative file (e.g. meshes\…\x.nif) → absolute winning path, or null.</summary>
    public static string? ResolveRelative(EnvironmentSnapshot env, string dataRelativePath)
    {
        var rel = dataRelativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart('\\');
        foreach (var modDir in EnumerateModDirsHighestFirst(env))
        {
            var candidate = Path.Combine(modDir, rel);
            if (File.Exists(candidate)) return candidate;
        }
        var inData = Path.Combine(env.GameDataPath, rel);
        return File.Exists(inData) ? inData : null;
    }

    /// <summary>Mod directories from highest priority to lowest (for first-hit-wins scans).</summary>
    public static IEnumerable<string> EnumerateModDirsHighestFirst(EnvironmentSnapshot env)
    {
        if (env.Manager != ModManagerKind.Mo2) yield break;

        for (var i = env.Mo2ModPriority.Count - 1; i >= 0; i--)
        {
            var name = env.Mo2ModPriority[i];
            if (string.Equals(name, OverwriteToken, StringComparison.Ordinal))
            {
                var overwrite = ResolveOverwriteDir(env);
                if (overwrite is not null) yield return overwrite;
                continue;
            }
            var dir = Path.Combine(env.StagingPath, name);
            if (Directory.Exists(dir)) yield return dir;
        }
    }

    /// <summary>Enabled mod folders lowest → highest (for reverse-overwrite asset indexing).</summary>
    public static IEnumerable<(string ModName, string Dir)> EnumerateModDirsLowestFirst(EnvironmentSnapshot env)
    {
        if (env.Manager != ModManagerKind.Mo2) yield break;

        foreach (var name in env.Mo2ModPriority)
        {
            if (string.Equals(name, OverwriteToken, StringComparison.Ordinal))
            {
                var overwrite = ResolveOverwriteDir(env);
                if (overwrite is not null) yield return ("overwrite", overwrite);
                continue;
            }
            var dir = Path.Combine(env.StagingPath, name);
            if (Directory.Exists(dir)) yield return (name, dir);
        }
    }

    /// <summary>
    /// Uses <see cref="EnvironmentSnapshot.Mo2OverwritePath"/> when discovery set it from
    /// Settings/overwrite_directory; otherwise InstancePath\overwrite.
    /// </summary>
    public static string? ResolveOverwriteDir(EnvironmentSnapshot env)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(env.Mo2OverwritePath))
            candidates.Add(env.Mo2OverwritePath!);
        if (!string.IsNullOrWhiteSpace(env.InstancePath))
            candidates.Add(Path.Combine(env.InstancePath, "overwrite"));

        foreach (var path in candidates)
        {
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full)) return full;
            }
            catch (Exception)
            {
                // ignore bad paths
            }
        }
        return null;
    }
}
