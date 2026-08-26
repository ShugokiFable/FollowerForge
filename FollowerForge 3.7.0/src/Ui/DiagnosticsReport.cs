using System.Text;
using FollowerForge.Domain;

namespace FollowerForge.Ui;

/// <summary>The follower-side half of a diagnostics report — what the user has chosen so far.</summary>
public sealed record DiagnosticsDraft(
    string FollowerName,
    string PluginName,
    string? Race,
    string? Face,
    string? Voice,
    string? Class,
    int ArmorCount,
    int WeaponCount,
    int AmmoCount,
    int SpellCount,
    int PerkCount,
    int CustomLineCount)
{
    public static DiagnosticsDraft Empty { get; } = new("", "", null, null, null, null, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Builds the text behind "Copy diagnostics".
///
/// This exists because every 3.x bug report so far arrived as a sentence — "body/face colour
/// mismatch", "can't add arrows", "MO2 can't find my mods" — with nothing underneath it, and
/// each one cost a round-trip before the actual work could start. One paste should answer
/// which manager, which profile, how much was indexed, what was picked, and what the last
/// build said.
///
/// Rendering is pure so it can be tested without a window: the window gathers, this formats.
/// </summary>
public static class DiagnosticsReport
{
    /// <summary>
    /// Home directories become %USERPROFILE% / %LOCALAPPDATA%. People paste these into public
    /// Nexus comments, and a raw path publishes the reporter's Windows account name — which is
    /// often their real name. The paths stay readable; only the identity goes.
    /// </summary>
    public static string Redact(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "(none)";

        // LocalAppData lives INSIDE the user profile, so it has to match first or the longer
        // path would already have been shortened to %USERPROFILE%\AppData\Local\...
        foreach (var (folder, token) in new[]
                 {
                     (Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%"),
                     (Environment.SpecialFolder.ApplicationData, "%APPDATA%"),
                     (Environment.SpecialFolder.UserProfile, "%USERPROFILE%"),
                 })
        {
            var root = Environment.GetFolderPath(folder);
            if (root.Length > 0 && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return token + path[root.Length..];
        }

        return path;
    }

    public static string Render(
        string appVersion,
        EnvironmentSnapshot? env,
        bool isIndexing,
        int knownPlaceCount,
        int exportedFaceCount,
        UiPreferences preferences,
        DiagnosticsDraft draft,
        IReadOnlyList<string> lastBuildMustFix,
        IReadOnlyList<string> lastBuildWarnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FollowerForge diagnostics");
        sb.AppendLine($"  version      : {appVersion}");
        sb.AppendLine($"  windows      : {Environment.OSVersion.Version}  ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");
        sb.AppendLine($"  ui           : theme {preferences.Theme}, {preferences.Experience} mode");
        sb.AppendLine();

        sb.AppendLine("Setup");
        if (env is null)
        {
            sb.AppendLine(isIndexing
                ? "  manager      : still being discovered when this was copied"
                : "  manager      : NOT RESOLVED — discovery failed or has not run");
        }
        else
        {
            sb.AppendLine($"  manager      : {env.ManagerLabel}");
            sb.AppendLine($"  profile      : {env.ActiveProfileId ?? "(none)"}{(env.ActiveProfileReason is { Length: > 0 } why ? $"  [{why}]" : "")}");
            sb.AppendLine($"  game root    : {Redact(env.GameRootPath)}");
            sb.AppendLine($"  instance     : {Redact(env.InstancePath)}");
            sb.AppendLine($"  staging      : {Redact(env.StagingPath)}");
            sb.AppendLine($"  plugins      : {env.EnabledPluginCount:N0} enabled of {env.LoadOrderCount:N0}");
            sb.AppendLine($"  staging mods : {env.StagingModCount:N0}");
            if (env.Mo2OverwritePath is { Length: > 0 } overwrite)
                sb.AppendLine($"  mo2 overwrite: {Redact(overwrite)}");
        }

        sb.AppendLine($"  catalogue    : {(isIndexing ? "STILL INDEXING when this was copied" : "ready")}");
        sb.AppendLine($"  places known : {knownPlaceCount:N0}");
        sb.AppendLine($"  faces found  : {exportedFaceCount:N0} RaceMenu export(s)");
        AppendList(sb, "Setup warnings", env?.Warnings ?? []);
        sb.AppendLine();

        sb.AppendLine("Draft");
        sb.AppendLine($"  name         : {Blank(draft.FollowerName)}");
        sb.AppendLine($"  plugin       : {Blank(draft.PluginName)}");
        sb.AppendLine($"  race         : {Blank(draft.Race)}");
        sb.AppendLine($"  face         : {Blank(draft.Face)}");
        sb.AppendLine($"  voice        : {Blank(draft.Voice)}");
        sb.AppendLine($"  class        : {Blank(draft.Class)}");
        sb.AppendLine(
            $"  picked       : {draft.ArmorCount} armor · {draft.WeaponCount} weapon · {draft.AmmoCount} ammo · " +
            $"{draft.SpellCount} spell · {draft.PerkCount} perk · {draft.CustomLineCount} custom line(s)");
        sb.AppendLine();

        sb.AppendLine("Last build");
        if (lastBuildMustFix.Count == 0 && lastBuildWarnings.Count == 0)
        {
            sb.AppendLine("  (nothing built in this session, or it reported no findings)");
        }
        else
        {
            AppendList(sb, "Must fix", lastBuildMustFix);
            AppendList(sb, "Check before building", lastBuildWarnings);
        }

        return sb.ToString();
    }

    private static string Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(not chosen yet)" : value;

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"  {heading}:");
        foreach (var item in items) sb.AppendLine($"    - {item}");
    }
}
