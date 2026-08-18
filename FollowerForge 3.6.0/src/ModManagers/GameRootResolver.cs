using System.Text.RegularExpressions;

namespace FollowerForge.ModManagers;

/// <summary>
/// Finds the Skyrim SE install without assuming it sits on C:. Steam libraries live wherever the
/// user put them, so the well-known default path is the LAST resort, not the only one.
/// Order: explicit override, FFORGE_GAME_PATH, every Steam library on every fixed drive, default.
/// </summary>
public static partial class GameRootResolver
{
    /// <summary>Well-known default. Kept as a final fallback, never as the only candidate.</summary>
    public const string DefaultGameRoot =
        @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition";

    private const string AppFolder = @"steamapps\common\Skyrim Special Edition";

    /// <summary>Steam roots people actually use, relative to a drive.</summary>
    private static readonly string[] SteamRoots =
    [
        @"Program Files (x86)\Steam", @"Program Files\Steam",
        "Steam", "SteamLibrary", @"Games\Steam", @"Games\SteamLibrary",
    ];

    /// <summary>Reads library paths out of a libraryfolders.vdf: lines of "path" "D:\\SteamLibrary".</summary>
    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();

    public static bool HasData(string root) =>
        !string.IsNullOrWhiteSpace(root) && Directory.Exists(Path.Combine(root, "Data"));

    /// <summary>The resolved game root, or null when nothing on this machine looks like Skyrim SE.</summary>
    public static string? Find(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return HasData(overridePath) ? Path.GetFullPath(overridePath) : null;

        var env = Environment.GetEnvironmentVariable("FFORGE_GAME_PATH");
        if (!string.IsNullOrWhiteSpace(env) && HasData(env)) return Path.GetFullPath(env);

        foreach (var candidate in Candidates())
        {
            if (HasData(candidate)) return Path.GetFullPath(candidate);
        }
        return HasData(DefaultGameRoot) ? DefaultGameRoot : null;
    }

    private static IEnumerable<string> Candidates()
    {
        foreach (var drive in FixedDrives())
        {
            foreach (var suffix in SteamRoots)
            {
                var steam = Path.Combine(drive, suffix);
                yield return Path.Combine(steam, AppFolder);
                // A Steam install lists its other libraries here, including ones on drives whose
                // layout we would never guess.
                foreach (var library in LibrariesOf(steam))
                    yield return Path.Combine(library, AppFolder);
            }
            // Some people drop the game straight on a drive with no Steam tree above it.
            yield return Path.Combine(drive, "Skyrim Special Edition");
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
            // vdf escapes backslashes.
            var path = m.Groups[1].Value.Replace(@"\\", @"\");
            if (path.Length > 0) yield return path;
        }
    }
}
