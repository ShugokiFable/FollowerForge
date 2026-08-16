using System.Text.RegularExpressions;

namespace FollowerForge.ModManagers;

/// <summary>
/// Finds an xVASynth install the same way <see cref="GameRootResolver"/> finds Skyrim: explicit
/// path first, then env, saved settings, every Steam library, then the well-known default.
/// Steam's own copy is often not on C: — that is why auto-detect used to miss it.
/// </summary>
public static partial class XvaSynthLocator
{
    public const string DefaultRoot =
        @"C:\Program Files (x86)\Steam\steamapps\common\xVASynth";

    private const string AppFolder = @"steamapps\common\xVASynth";

    private static readonly string[] SteamRoots =
    [
        @"Program Files (x86)\Steam", @"Program Files\Steam",
        "Steam", "SteamLibrary", @"Games\Steam", @"Games\SteamLibrary",
    ];

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();

    /// <summary>True when this folder can supply Skyrim voice models.</summary>
    public static bool HasModels(string? root) =>
        !string.IsNullOrWhiteSpace(root)
        && Directory.Exists(Path.Combine(root, "resources", "app", "models", "skyrim"));

    /// <summary>True when the folder looks like an xVASynth root even if Skyrim models are missing.</summary>
    public static bool LooksLikeRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
        if (HasModels(root)) return true;
        if (File.Exists(Path.Combine(root, "resources", "app", "cpython_cpu", "server.exe"))) return true;
        return File.Exists(Path.Combine(root, "xVASynth.exe"));
    }

    /// <summary>
    /// The root to use. A non-empty override is honoured even when the folder is incomplete, so
    /// the UI can show "not found at the path you set".
    /// </summary>
    public static string Resolve(
        string? overridePath = null,
        string? settingsPath = null,
        string? settingsDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath.Trim().Trim('"'));

        var env = Environment.GetEnvironmentVariable("FFORGE_XVASYNTH");
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env.Trim().Trim('"'));

        var saved = settingsPath ?? AppUserSettings.Load(settingsDirectory).XvaSynthRoot;
        if (!string.IsNullOrWhiteSpace(saved))
            return Path.GetFullPath(saved);

        foreach (var candidate in Candidates())
        {
            if (HasModels(candidate)) return Path.GetFullPath(candidate);
        }

        return DefaultRoot;
    }

    /// <summary>Best installed root, or null when nothing on this machine looks like xVASynth.</summary>
    public static string? Find(string? overridePath = null, string? settingsDirectory = null)
    {
        var resolved = Resolve(overridePath, settingsDirectory: settingsDirectory);
        return HasModels(resolved) ? resolved : null;
    }

    private static IEnumerable<string> Candidates()
    {
        yield return DefaultRoot;
        foreach (var drive in FixedDrives())
        {
            foreach (var suffix in SteamRoots)
            {
                var steam = Path.Combine(drive, suffix);
                yield return Path.Combine(steam, AppFolder);
                foreach (var library in LibrariesOf(steam))
                    yield return Path.Combine(library, AppFolder);
            }
            yield return Path.Combine(drive, "xVASynth");
        }
    }

    private static IEnumerable<string> FixedDrives()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch (IOException) { yield break; }

        foreach (var drive in drives)
        {
            var ready = false;
            try { ready = drive.IsReady && drive.DriveType == DriveType.Fixed; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (ready) yield return drive.RootDirectory.FullName;
        }
    }

    private static IEnumerable<string> LibrariesOf(string steamRoot)
    {
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        string text;
        try
        {
            if (!File.Exists(vdf)) yield break;
            text = File.ReadAllText(vdf);
        }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (Match m in LibraryPath().Matches(text))
        {
            var path = m.Groups[1].Value.Replace(@"\\", @"\");
            if (path.Length > 0) yield return path;
        }
    }
}
