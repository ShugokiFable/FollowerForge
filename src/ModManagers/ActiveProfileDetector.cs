namespace FollowerForge.ModManagers;

/// <summary>
/// Determines the active Vortex profile without touching Vortex's LevelDB state:
/// the profile whose plugins.txt matches the game-runtime plugins.txt is active.
/// </summary>
public static class ActiveProfileDetector
{
    public static (string? ProfileId, string Reason) Detect(
        string profilesPath, string runtimePluginsTxt, List<string> warnings)
    {
        if (!Directory.Exists(profilesPath))
        {
            warnings.Add($"Vortex profiles folder missing: {profilesPath}");
            return (null, "profiles folder missing");
        }

        var profiles = Directory.EnumerateDirectories(profilesPath)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();

        if (profiles.Count == 0)
        {
            warnings.Add("No Vortex profiles found.");
            return (null, "no profiles");
        }

        if (!File.Exists(runtimePluginsTxt))
        {
            if (profiles.Count == 1)
                return (profiles[0], "single profile (runtime plugins.txt missing)");
            warnings.Add("Multiple profiles but no runtime plugins.txt to disambiguate.");
            return (null, "ambiguous: multiple profiles, no runtime plugins.txt");
        }

        var runtime = Normalize(File.ReadAllLines(runtimePluginsTxt));
        var runtimeSet = runtime.ToHashSet();

        // Exact sequence match first; otherwise highest Jaccard similarity. A profile edited
        // in Vortex but not yet re-deployed differs slightly from the runtime file — that is
        // still the active profile, so a near-match wins (with a warning).
        string? best = null;
        double bestScore = -1;
        var exact = new List<string>();
        foreach (var id in profiles)
        {
            var candidate = Path.Combine(profilesPath, id, "plugins.txt");
            if (!File.Exists(candidate)) continue;
            var lines = Normalize(File.ReadAllLines(candidate));
            if (runtime.SequenceEqual(lines)) exact.Add(id);
            var set = lines.ToHashSet();
            var union = (double)set.Union(runtimeSet).Count();
            var score = union == 0 ? 0 : set.Intersect(runtimeSet).Count() / union;
            if (score > bestScore) { bestScore = score; best = id; }
        }

        if (exact.Count == 1) return (exact[0], "plugins.txt matches runtime");
        if (exact.Count > 1)
        {
            warnings.Add($"Multiple profiles match runtime plugins.txt: {string.Join(", ", exact)}.");
            return (exact[0], "first of multiple identical matches");
        }
        if (best is not null && bestScore >= 0.9)
        {
            warnings.Add($"Profile {best} is a {bestScore:P0} match to the runtime plugins.txt — " +
                         "the profile has undeployed changes. Deploy in Vortex for an exact state.");
            return (best, $"closest match ({bestScore:P0}); deployment pending");
        }
        if (profiles.Count == 1)
        {
            warnings.Add("Single profile's plugins.txt differs substantially from runtime — profile may not be deployed.");
            return (profiles[0], "single profile (content mismatch with runtime)");
        }
        warnings.Add("No profile matches the runtime plugins.txt.");
        return (null, "no profile matches runtime plugins.txt");
    }

    private static List<string> Normalize(IEnumerable<string> lines) =>
        lines.Select(l => l.Trim())
             .Where(l => l.Length > 0 && !l.StartsWith('#'))
             .Select(l => l.ToLowerInvariant())
             .ToList();
}
