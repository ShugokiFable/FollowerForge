using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Read-only discovery of a Mod Organizer 2 instance. Paths come from ModOrganizer.ini,
/// environment overrides, or well-known instance locations — never from hard-coded usernames.
/// </summary>
public sealed class Mo2Discovery(ILogger log)
{
    /// <param name="instanceOverride">Explicit instance root (CLI --mo2-instance).</param>
    /// <param name="gameRootOverride">Explicit game root (CLI --game-path).</param>
    /// <param name="profileOverride">Exact profile selected by CLI or the GUI setup dialog.</param>
    /// <param name="strictOverride">Reject invalid manual choices without guessing another profile.</param>
    public EnvironmentSnapshot? TryDiscover(
        string? instanceOverride = null,
        string? gameRootOverride = null,
        string? profileOverride = null,
        bool strictOverride = false)
    {
        var warnings = new List<string>();
        var instance = ResolveInstanceRoot(instanceOverride, warnings);
        if (instance is null)
        {
            if (strictOverride)
                throw new DirectoryNotFoundException(
                    $"The selected MO2 instance does not contain ModOrganizer.ini: {instanceOverride}");
            return null;
        }

        var iniPath = Path.Combine(instance, "ModOrganizer.ini");
        var inspector = new Mo2InstanceInspector(log);
        var inspection = inspector.Inspect(iniPath, gameRootOverride);

        // A portable instance can retain an obsolete gamePath after being moved. Automatic mode
        // keeps the old drive-search recovery, while manual/strict choices stay exact.
        if (!strictOverride
            && string.IsNullOrWhiteSpace(gameRootOverride)
            && (inspection.GameRoot is null || !GameRootResolver.HasData(inspection.GameRoot)))
        {
            var foundGame = GameRootResolver.Find();
            if (foundGame is not null)
                inspection = inspector.Inspect(iniPath, foundGame);
        }

        warnings.AddRange(inspection.Warnings);
        if (!inspection.IsValid)
        {
            var message = string.Join(" ", inspection.Errors);
            if (strictOverride) throw new DirectoryNotFoundException(message);
            log.Warning("MO2 instance {Instance} is not usable: {Error}", instance, message);
            return null;
        }

        var requestedProfile = string.IsNullOrWhiteSpace(profileOverride)
            ? inspection.SelectedProfile ?? "Default"
            : profileOverride.Trim().Trim('"');
        var selected = inspection.Profiles.FirstOrDefault(name =>
            string.Equals(name, requestedProfile, StringComparison.OrdinalIgnoreCase));
        var usedFallback = false;

        if (selected is null)
        {
            if (strictOverride || !string.IsNullOrWhiteSpace(profileOverride))
                throw new DirectoryNotFoundException(
                    $"The selected MO2 profile '{requestedProfile}' does not exist: "
                    + Path.Combine(inspection.ProfilesPath, requestedProfile));

            selected = inspection.Profiles.FirstOrDefault();
            if (selected is null)
            {
                warnings.Add($"MO2 profiles directory is empty: {inspection.ProfilesPath}");
                return null;
            }
            warnings.Add($"MO2 selected profile '{requestedProfile}' missing; using '{selected}'.");
            usedFallback = true;
        }

        var profileDir = Path.Combine(inspection.ProfilesPath, selected);
        var pluginsTxt = Path.Combine(profileDir, "plugins.txt");
        var loadOrderTxt = Path.Combine(profileDir, "loadorder.txt");
        var modlistTxt = Path.Combine(profileDir, "modlist.txt");

        if (strictOverride || !string.IsNullOrWhiteSpace(profileOverride))
        {
            var missing = new List<string>();
            if (!File.Exists(modlistTxt)) missing.Add(modlistTxt);
            if (!File.Exists(pluginsTxt) && !File.Exists(loadOrderTxt))
                missing.Add($"{pluginsTxt} or {loadOrderTxt}");
            if (missing.Count > 0)
                throw new InvalidDataException(
                    $"MO2 profile '{selected}' is incomplete. Missing: {string.Join("; ", missing)}");
        }

        var enabledCount = File.Exists(pluginsTxt)
            ? PluginLists.ParsePluginsTxt(pluginsTxt).Count(entry => entry.Enabled)
            : 0;
        var loadOrderCount = File.Exists(loadOrderTxt)
            ? PluginLists.ParseLoadOrderTxt(loadOrderTxt).Count
            : 0;
        var modPriority = File.Exists(modlistTxt)
            ? PluginLists.ParseMo2ModList(modlistTxt)
            : [];
        if (Directory.Exists(inspection.OverwritePath))
            modPriority = modPriority.Append(Mo2PathResolver.OverwriteToken).ToList();

        var stagingModCount = Directory.EnumerateDirectories(inspection.ModsPath).Count();
        var deployStamp = MaxWriteTimeUtcMs(pluginsTxt, loadOrderTxt, modlistTxt);
        var runtimePluginsTxt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Skyrim Special Edition", "plugins.txt");
        var pluginView = Mo2DataView.ViewRootFor(inspection.InstanceRoot, selected);

        log.Information(
            "Environment (MO2): instance={Instance}, profile={Profile}, overwrite={Overwrite}, {Enabled} enabled plugins, {Mods} mods",
            inspection.InstanceRoot, selected, inspection.OverwritePath, enabledCount, stagingModCount);

        return new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Mo2,
            ManagerLabel = "Mod Organizer 2",
            GameRootPath = inspection.GameRoot!,
            GameDataPath = Path.Combine(inspection.GameRoot!, "Data"),
            PluginDataPath = pluginView,
            InstancePath = inspection.InstanceRoot,
            StagingPath = inspection.ModsPath,
            ProfilesPath = inspection.ProfilesPath,
            RuntimePluginsTxtPath = runtimePluginsTxt,
            ActiveProfileId = selected,
            ActiveProfileReason = !string.IsNullOrWhiteSpace(profileOverride)
                ? "explicit MO2 profile override"
                : usedFallback
                    ? "MO2 selected_profile fallback"
                    : "MO2 selected_profile",
            DeploymentMethod = "mo2-modlist",
            DeploymentTimeUtcMs = deployStamp,
            EnabledPluginCount = enabledCount,
            LoadOrderCount = loadOrderCount,
            StagingModCount = stagingModCount,
            Mo2ModPriority = modPriority,
            Mo2OverwritePath = inspection.OverwritePath,
            Warnings = warnings,
        };
    }

    public static WriteGuard CreateGuard(EnvironmentSnapshot env)
    {
        var guard = new WriteGuard();
        guard.Protect(env.GameRootPath);
        guard.Protect(env.StagingPath);
        guard.Protect(env.InstancePath);
        if (!string.IsNullOrWhiteSpace(env.ProfilesPath)) guard.Protect(env.ProfilesPath);
        if (!string.IsNullOrWhiteSpace(env.Mo2OverwritePath)) guard.Protect(env.Mo2OverwritePath!);
        return guard;
    }

    private string? ResolveInstanceRoot(string? instanceOverride, List<string> warnings)
    {
        foreach (var candidate in CandidateInstances(instanceOverride))
        {
            var full = Path.GetFullPath(candidate);
            if (!File.Exists(Path.Combine(full, "ModOrganizer.ini"))) continue;

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

        var envInstance = Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE")
            ?? Environment.GetEnvironmentVariable("SKYRIM_MO2_INSTANCE");
        if (!string.IsNullOrWhiteSpace(envInstance) && Directory.Exists(envInstance))
            yield return envInstance;

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var moGlobal = Path.Combine(local, "ModOrganizer");
        if (File.Exists(Path.Combine(moGlobal, "ModOrganizer.ini"))) yield return moGlobal;
        if (Directory.Exists(moGlobal))
        {
            foreach (var dir in Directory.EnumerateDirectories(moGlobal)) yield return dir;
        }

        foreach (var candidate in PortableCandidates()) yield return candidate;
        // Do not auto-add houseCARL-Shim — it is not MO2.
    }

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
                if (Directory.Exists(candidate)) yield return candidate;
            }
        }
    }

    private static long MaxWriteTimeUtcMs(params string[] paths)
    {
        long max = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            var ms = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeMilliseconds();
            if (ms > max) max = ms;
        }
        return max == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : max;
    }
}
