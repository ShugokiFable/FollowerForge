using FollowerForge.Domain;

namespace FollowerForge.SkyrimRecords;

public sealed record FrameworkHit(string Framework, string Plugin, string Integration);
public sealed record BodySystemHit(string System, string Evidence);

public sealed record DetectionReport(
    IReadOnlyList<FrameworkHit> Frameworks,
    IReadOnlyList<BodySystemHit> BodySystems,
    string Guidance);

/// <summary>
/// Detects follower frameworks and body systems from the load order (plugin names) and the
/// asset index. Generated followers stay vanilla-compatible by default; frameworks are only
/// reported so the user can opt in to an integration — never a hard dependency.
/// </summary>
public static class Detection
{
    // Specific plugin file names only (case-insensitive equality) — short substrings like
    // "AFT"/"EFF" produce false positives (Immersive SpeechcrAFT, Water EFFects Fix).
    private static readonly (string PluginFile, string Name, string Note)[] Frameworks =
    [
        ("nwsFollowerFramework.esp", "Nether's Follower Framework", "Add NFF keyword/faction only if the user opts in"),
        ("AmazingFollowerTweaks.esp", "Amazing Follower Tweaks", "AFT manages followers at runtime; no plugin edit needed"),
        ("EFFCore.esm", "Extensible Follower Framework", "EFF works with vanilla follower factions"),
        ("EFFDialogue.esp", "Extensible Follower Framework", "EFF works with vanilla follower factions"),
        ("My Home Is Your Home.esp", "My Home Is Your Home (follower home)", "Optional follower home framework"),
        ("BusyFollowerFramework.esp", "Busy Follower Framework", "Optional idle behaviors; opt-in only"),
        ("MultipleFollowersFramework.esp", "Multiple Followers Framework", "Raises follower cap; vanilla followers already work"),
    ];

    // pluginName/asset substring → body system
    private static readonly (string Match, string Name)[] BodyPluginHints =
    [
        ("CBBE", "CBBE"),
        ("3BA", "CBBE 3BA (3BBB)"),
        ("3BBB", "CBBE 3BA (3BBB)"),
        ("HIMBO", "HIMBO"),
        ("UNP", "UNP-family"),
        ("BHUNP", "BHUNP"),
    ];

    public static DetectionReport Detect(
        IEnumerable<string> enabledPlugins,
        Func<string, bool> assetExists,
        IEnumerable<string>? stagingModNames = null)
    {
        var plugins = enabledPlugins.ToList();
        var frameworks = new List<FrameworkHit>();
        foreach (var (pluginFile, name, note) in Frameworks)
        {
            var hit = plugins.FirstOrDefault(p => p.Equals(pluginFile, StringComparison.OrdinalIgnoreCase));
            if (hit is not null && !frameworks.Any(f => f.Framework == name))
                frameworks.Add(new FrameworkHit(name, hit, note));
        }

        var body = new List<BodySystemHit>();
        // BodySlide presence: CalienteTools\BodySlide is the canonical marker.
        if (assetExists(@"calienttools\bodyslide") || assetExists(@"calientetools\bodyslide"))
            body.Add(new BodySystemHit("BodySlide", "CalienteTools\\BodySlide present"));

        var haystack = plugins.Concat(stagingModNames ?? []).ToList();
        foreach (var (match, name) in BodyPluginHints)
        {
            var hit = haystack.FirstOrDefault(p => p.Contains(match, StringComparison.OrdinalIgnoreCase));
            if (hit is not null && !body.Any(b => b.System == name))
                body.Add(new BodySystemHit(name, hit));
        }

        var guidance = frameworks.Count > 0
            ? "Followers are built vanilla-compatible. Framework integration is optional and off by default."
            : "No follower framework detected; vanilla follower mechanics are used.";
        return new DetectionReport(frameworks, body, guidance);
    }
}
