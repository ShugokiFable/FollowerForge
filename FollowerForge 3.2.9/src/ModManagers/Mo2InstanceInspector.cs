using System.Text.RegularExpressions;
using Serilog;

namespace FollowerForge.ModManagers;

public sealed record Mo2Inspection(
    string IniPath,
    string InstanceRoot,
    string BaseDirectory,
    string? GameRoot,
    string ModsPath,
    string ProfilesPath,
    string OverwritePath,
    string? SelectedProfile,
    IReadOnlyList<string> Profiles,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Read-only parser and validator for one concrete MO2 ModOrganizer.ini. This is deliberately
/// separate from instance discovery so the GUI can inspect a user-selected instance without
/// starting an index or guessing a different profile.
/// </summary>
public sealed class Mo2InstanceInspector(ILogger log)
{
    private static readonly Regex IniValue = new(
        @"^([A-Za-z0-9_]+)\s*=\s*(?:@ByteArray\((.*)\)|(.*))\s*$",
        RegexOptions.Compiled);

    public Mo2Inspection Inspect(string iniPath, string? gameRootOverride = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var requestedIni = (iniPath ?? string.Empty).Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(requestedIni))
        {
            errors.Add("Choose the ModOrganizer.ini file for the MO2 instance.");
            return Empty(requestedIni, errors, warnings);
        }

        string canonicalIni;
        try { canonicalIni = Path.GetFullPath(requestedIni); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"ModOrganizer.ini path is invalid: {requestedIni}");
            return Empty(requestedIni, errors, warnings);
        }

        if (!string.Equals(Path.GetFileName(canonicalIni), "ModOrganizer.ini", StringComparison.OrdinalIgnoreCase))
            errors.Add($"The selected file must be named ModOrganizer.ini: {canonicalIni}");
        if (!File.Exists(canonicalIni))
        {
            errors.Add($"ModOrganizer.ini does not exist: {canonicalIni}");
            return Empty(canonicalIni, errors, warnings);
        }

        var instanceRoot = Path.GetDirectoryName(canonicalIni)!;
        var ini = ReadIni(canonicalIni);
        var rawBase = GetIni(ini, "Settings", "base_directory");
        var baseDirectory = ResolveBaseDirectory(rawBase, instanceRoot);
        var modsPath = ResolveConfiguredPath(
            GetIni(ini, "Settings", "mod_directory"), "mods", baseDirectory);
        var profilesPath = ResolveConfiguredPath(
            GetIni(ini, "Settings", "profiles_directory"), "profiles", baseDirectory);
        var overwritePath = ResolveConfiguredPath(
            GetIni(ini, "Settings", "overwrite_directory"), "overwrite", baseDirectory);

        var rawGame = gameRootOverride ?? GetIni(ini, "General", "gamePath");
        var gameRoot = ResolveGameRoot(rawGame, instanceRoot);
        if (gameRoot is null || !Directory.Exists(Path.Combine(gameRoot, "Data")))
            errors.Add($"MO2 game root does not contain a Data directory: {gameRoot ?? "(not configured)"}");
        if (!Directory.Exists(modsPath))
            errors.Add($"MO2 mods directory does not exist: {modsPath}");
        if (!Directory.Exists(profilesPath))
            errors.Add($"MO2 profiles directory does not exist: {profilesPath}");
        if (!Directory.Exists(overwritePath))
            warnings.Add($"MO2 overwrite directory does not exist: {overwritePath}");

        var profiles = Directory.Exists(profilesPath)
            ? Directory.EnumerateDirectories(profilesPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        var selected = GetIni(ini, "General", "selected_profile")?.Trim().Trim('"');

        log.Debug(
            "Inspected MO2 instance {Instance}: base={Base}, profiles={Profiles}, selected={Selected}",
            instanceRoot, baseDirectory, profiles.Length, selected);

        return new Mo2Inspection(
            canonicalIni,
            instanceRoot,
            baseDirectory,
            gameRoot,
            modsPath,
            profilesPath,
            overwritePath,
            selected,
            profiles,
            errors,
            warnings);
    }

    internal static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
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

            var match = IniValue.Match(line);
            if (!match.Success) continue;
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            if (!result.ContainsKey(section))
                result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[section][key] = value.Trim().Trim('"');
        }
        return result;
    }

    internal static string? GetIni(
        Dictionary<string, Dictionary<string, string>> ini,
        string section,
        string key) =>
        ini.TryGetValue(section, out var values) && values.TryGetValue(key, out var value)
            ? value
            : null;

    private static string ResolveBaseDirectory(string? configured, string instanceRoot)
    {
        if (string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(instanceRoot);
        var expanded = ExpandEnvironment(configured);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(instanceRoot, expanded));
    }

    private static string ResolveConfiguredPath(string? configured, string fallbackName, string baseDirectory)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallbackName : configured;
        value = value.Replace("%BASE_DIR%", baseDirectory, StringComparison.OrdinalIgnoreCase);
        value = ExpandEnvironment(value);
        return Path.GetFullPath(Path.IsPathRooted(value)
            ? value
            : Path.Combine(baseDirectory, value));
    }

    private static string? ResolveGameRoot(string? configured, string instanceRoot)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;
        var value = ExpandEnvironment(configured);
        return Path.GetFullPath(Path.IsPathRooted(value)
            ? value
            : Path.Combine(instanceRoot, value));
    }

    private static string ExpandEnvironment(string path) =>
        Environment.ExpandEnvironmentVariables(
            path.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar));

    private static Mo2Inspection Empty(
        string iniPath,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings) =>
        new(
            iniPath,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            [],
            errors,
            warnings);
}
