using System.Globalization;
using System.Text.RegularExpressions;
using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Read-only discovery of a Mod Organizer 2 instance. Paths come from ModOrganizer.ini,
/// environment overrides, or well-known instance locations — never from hard-coded usernames.
/// </summary>
public sealed class Mo2Discovery(ILogger log)
{
    private static readonly Regex IniValue = new(
        @"^([A-Za-z0-9_]+)\s*=\s*(?:@ByteArray\((.*)\)|(.*))\s*$",
        RegexOptions.Compiled);

    /// <param name="instanceOverride">Explicit instance root (CLI --mo2-instance).</param>
    /// <param name="gameRootOverride">Explicit game root (CLI --game-path).</param>
    public EnvironmentSnapshot? TryDiscover(string? instanceOverride = null, string? gameRootOverride = null)
    {
        var warnings = new List<string>();
        var instance = ResolveInstanceRoot(instanceOverride, warnings);
        if (instance is null) return null;

        var iniPath = Path.Combine(instance, "ModOrganizer.ini");
        if (!File.Exists(iniPath))
        {
            warnings.Add($"MO2 instance has no ModOrganizer.ini: {instance}");
            return null;
        }

        var ini = ReadIni(iniPath);
        // MO2's own gamePath first, then the same drive-wide search Vortex uses. A portable
        // instance moved between machines often points at a path that no longer exists.
        var gameRoot = gameRootOverride ?? GetIni(ini, "General", "gamePath");
        if (!GameRootResolver.HasData(gameRoot ?? ""))
            gameRoot = GameRootResolver.Find(gameRootOverride);
        if (gameRoot is null)
        {
            log.Warning("MO2 instance {Instance}: no usable gamePath and Skyrim SE not found on any drive", instance);
            warnings.Add($"MO2 instance {instance} names no usable Skyrim SE path.");
            return null;
        }

        gameRoot = Path.GetFullPath(gameRoot.Trim().TrimEnd('\\', '/'));
        var dataPath = Path.Combine(gameRoot, "Data");

        var modsPath = GetIni(ini, "Settings", "mod_directory")
            ?? Path.Combine(instance, "mods");
        var profilesPath = GetIni(ini, "Settings", "profiles_directory")
            ?? Path.Combine(instance, "profiles");
        var overwritePath = GetIni(ini, "Settings", "overwrite_directory")
            ?? Path.Combine(instance, "overwrite");

        modsPath = Path.GetFullPath(ExpandEnv(modsPath));
        profilesPath = Path.GetFullPath(ExpandEnv(profilesPath));
        overwritePath = Path.GetFullPath(ExpandEnv(overwritePath));

        var selected = GetIni(ini, "General", "selected_profile") ?? "Default";
        // MO2 sometimes stores the profile as a QByteArray string with quotes stripped already.
        selected = selected.Trim().Trim('"');
        if (!Directory.Exists(Path.Combine(profilesPath, selected)))
        {
            var first = Directory.Exists(profilesPath)
                ? Directory.EnumerateDirectories(profilesPath).Select(Path.GetFileName).FirstOrDefault()
                : null;
            if (first is null)
            {
                warnings.Add($"MO2 profiles folder empty: {profilesPath}");
                return null;
            }
            warnings.Add($"MO2 selected profile '{selected}' missing; using '{first}'.");
            selected = first;
        }

        var profileDir = Path.Combine(profilesPath, selected);
        var pluginsTxt = Path.Combine(profileDir, "plugins.txt");
        var loadOrderTxt = Path.Combine(profileDir, "loadorder.txt");
        var modlistTxt = Path.Combine(profileDir, "modlist.txt");

        var enabledCount = File.Exists(pluginsTxt)
            ? PluginLists.ParsePluginsTxt(pluginsTxt).Count(e => e.Enabled)
            : 0;
        var loadOrderCount = File.Exists(loadOrderTxt)
            ? PluginLists.ParseLoadOrderTxt(loadOrderTxt).Count
            : 0;

        var modPriority = File.Exists(modlistTxt)
            ? PluginLists.ParseMo2ModList(modlistTxt)
            : [];
        // Overwrite always wins in MO2.
        if (Directory.Exists(overwritePath))
            modPriority = modPriority.Append("__mo2_overwrite__").ToList();

        var stagingModCount = Directory.Exists(modsPath)
            ? Directory.EnumerateDirectories(modsPath).Count()
            : 0;

        var deployStamp = MaxWriteTimeUtcMs(pluginsTxt, loadOrderTxt, modlistTxt);

        var runtimePluginsTxt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Skyrim Special Edition", "plugins.txt");

        // Temporary hardlink view is filled later by Mo2DataView when the catalogue builds.
        var pluginView = Mo2DataView.ViewRootFor(instance, selected);

        if (!Directory.Exists(overwritePath))
            warnings.Add($"MO2 overwrite folder missing: {overwritePath}");

        log.Information(
            "Environment (MO2): instance={Instance}, profile={Profile}, overwrite={Overwrite}, {Enabled} enabled plugins, {Mods} mods",
            instance, selected, overwritePath, enabledCount, stagingModCount);

        return new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Mo2,
            ManagerLabel = "Mod Organizer 2",
            GameRootPath = gameRoot,
            GameDataPath = dataPath,
            PluginDataPath = pluginView,
            InstancePath = instance,
            StagingPath = modsPath,
            ProfilesPath = profilesPath,
            RuntimePluginsTxtPath = runtimePluginsTxt,
            ActiveProfileId = selected,
            ActiveProfileReason = "MO2 selected_profile",
            DeploymentMethod = "mo2-modlist",
            DeploymentTimeUtcMs = deployStamp,
            EnabledPluginCount = enabledCount,
            LoadOrderCount = loadOrderCount,
            StagingModCount = stagingModCount,
            Mo2ModPriority = modPriority,
            Mo2OverwritePath = overwritePath,
            Warnings = warnings,
        };
    }

    public static WriteGuard CreateGuard(EnvironmentSnapshot env)
    {
        var guard = new WriteGuard();
        guard.Protect(env.GameRootPath);
        guard.Protect(env.StagingPath);
        guard.Protect(env.InstancePath);
        // Profiles may live outside the instance root via Settings/profiles_directory.
        if (!string.IsNullOrWhiteSpace(env.ProfilesPath))
            guard.Protect(env.ProfilesPath);
        if (!string.IsNullOrWhiteSpace(env.Mo2OverwritePath))
            guard.Protect(env.Mo2OverwritePath!);
        return guard;
    }

    private string? ResolveInstanceRoot(string? instanceOverride, List<string> warnings)
    {
        foreach (var candidate in CandidateInstances(instanceOverride))
        {
            var full = Path.GetFullPath(candidate);
            if (!File.Exists(Path.Combine(full, "ModOrganizer.ini")))
                continue;

            // houseCARL builds a Vortex-shaped "MO2" shim with thousands of junctions. Indexing it
            // like a real MO2 instance re-reads every staged Vortex mod and can hang for hours.
            if (IsHouseCarlShim(full))
            {
                var allow = Environment.GetEnvironmentVariable("FFORGE_ALLOW_HOUSECARL_SHIM");
                var forced = !string.IsNullOrWhiteSpace(instanceOverride)
                    && PathsEqual(instanceOverride, full);
                if (!forced && !IsTruthy(allow))
                {
                    warnings.Add(
                        $"Skipped houseCARL Vortex shim at {full} (not a real MO2 instance). "
                        + "FollowerForge will use Vortex when available. "
                        + "Set FFORGE_ALLOW_HOUSECARL_SHIM=1 only if you really mean it.");
                    log.Warning("Skipping houseCARL shim MO2 candidate {Path}", full);
                    continue;
                }
            }

            return full;
        }
        return null;
    }

    private static bool IsHouseCarlShim(string instanceRoot) =>
        File.Exists(Path.Combine(instanceRoot, "HOUSECARL-SHIM.txt"))
        || instanceRoot.EndsWith("houseCARL-Shim", StringComparison.OrdinalIgnoreCase)
        || instanceRoot.EndsWith("housecarl-shim", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruthy(string? value) =>
        value is not null
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> CandidateInstances(string? instanceOverride)
    {
        if (!string.IsNullOrWhiteSpace(instanceOverride) && Directory.Exists(instanceOverride))
            yield return instanceOverride;

        // FFORGE_MO2_INSTANCE is FollowerForge's own knob. SKYRIM_MO2_INSTANCE is shared with
        // houseCARL and often points at a Vortex shim — still accepted as a fallback candidate,
        // but ResolveInstanceRoot will skip houseCARL shims unless explicitly allowed.
        var envInstance = Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE")
            ?? Environment.GetEnvironmentVariable("SKYRIM_MO2_INSTANCE");
        if (!string.IsNullOrWhiteSpace(envInstance) && Directory.Exists(envInstance))
            yield return envInstance;

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var moGlobal = Path.Combine(local, "ModOrganizer");
        // Portable / multi-instance: each subfolder of %LOCALAPPDATA%\ModOrganizer may be an instance,
        // or the folder itself may hold ModOrganizer.ini.
        if (File.Exists(Path.Combine(moGlobal, "ModOrganizer.ini")))
            yield return moGlobal;
        if (Directory.Exists(moGlobal))
        {
            foreach (var dir in Directory.EnumerateDirectories(moGlobal))
                yield return dir;
        }

        // Portable instances are the common MO2 setup and live wherever the user unpacked them —
        // never under LOCALAPPDATA. Probe the folder names people actually use, on every drive.
        foreach (var candidate in PortableCandidates())
            yield return candidate;
        // Do not auto-add houseCARL-Shim — it is not MO2.
    }

    /// <summary>Folder names portable MO2 installs actually use, checked on every fixed drive.</summary>
    private static readonly string[] PortableNames =
    [
        "MO2", "ModOrganizer", "Mod Organizer 2", "Mod Organizer",
        @"Modding\MO2", @"Modding\Mod Organizer 2", @"Games\MO2", @"Games\Mod Organizer 2",
        @"Skyrim\MO2", @"Tools\MO2",
    ];

    private static IEnumerable<string> PortableCandidates()
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
            if (!ready) continue;

            foreach (var name in PortableNames)
            {
                var candidate = Path.Combine(drive.RootDirectory.FullName, name);
                // ResolveInstanceRoot re-checks for ModOrganizer.ini; this only avoids the noise.
                if (Directory.Exists(candidate)) yield return candidate;
            }
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                if (!result.ContainsKey(section))
                    result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var m = IniValue.Match(line);
            if (!m.Success) continue;
            var key = m.Groups[1].Value;
            var value = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            if (!result.ContainsKey(section))
                result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[section][key] = value.Trim().Trim('"');
        }
        return result;
    }

    private static string? GetIni(
        Dictionary<string, Dictionary<string, string>> ini, string section, string key) =>
        ini.TryGetValue(section, out var bag) && bag.TryGetValue(key, out var value) ? value : null;

    private static string ExpandEnv(string path) =>
        Environment.ExpandEnvironmentVariables(path.Replace('/', Path.DirectorySeparatorChar));

    private static long MaxWriteTimeUtcMs(params string[] paths)
    {
        long max = 0;
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            var ms = new DateTimeOffset(File.GetLastWriteTimeUtc(p)).ToUnixTimeMilliseconds();
            if (ms > max) max = ms;
        }
        return max == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : max;
    }
}
