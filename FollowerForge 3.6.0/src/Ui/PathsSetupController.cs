using FollowerForge.Domain;
using FollowerForge.ModManagers;

namespace FollowerForge.Ui;

public sealed record PathsSetupState(
    string XvaSynthRoot,
    string WorkspaceRoot,
    string Summary,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validates the Paths dialog without writing settings. Empty boxes mean automatic detection.
/// </summary>
public static class PathsSetupController
{
    public static PathsSetupState Validate(
        string? xvaSynthRoot,
        string? workspaceRoot,
        EnvironmentSnapshot? env = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var xva = string.IsNullOrWhiteSpace(xvaSynthRoot) ? null : Path.GetFullPath(xvaSynthRoot.Trim().Trim('"'));
        var workspace = string.IsNullOrWhiteSpace(workspaceRoot) ? null : Path.GetFullPath(workspaceRoot.Trim().Trim('"'));

        if (xva is not null)
        {
            if (!Directory.Exists(xva))
                errors.Add($"xVASynth folder does not exist: {xva}");
            else if (!XvaSynthLocator.LooksLikeRoot(xva))
                errors.Add(
                    "That folder does not look like an xVASynth install. Pick the folder that contains "
                    + "xVASynth.exe or resources\\app\\cpython_cpu\\server.exe.");
            else if (!XvaSynthLocator.HasModels(xva))
                warnings.Add(
                    "xVASynth is there, but resources\\app\\models\\skyrim is missing. Custom lines "
                    + "need the Skyrim voice models installed in xVASynth.");
        }

        if (workspace is not null)
        {
            if (LooksLikeGameData(workspace, env))
                errors.Add("The output folder cannot be Skyrim's Data folder or the game install.");
            else if (LooksLikeSaves(workspace))
                errors.Add("The output folder cannot be the Skyrim saves folder.");
            else
            {
                try
                {
                    var probe = Path.GetFullPath(workspace);
                    var parent = Directory.Exists(probe) ? probe : Path.GetDirectoryName(probe);
                    if (parent is null || !Directory.Exists(parent))
                        errors.Add($"Cannot create the output folder; its parent does not exist: {workspace}");
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    errors.Add($"Output folder is not a usable path: {ex.Message}");
                }
            }
        }

        var resolvedXva = XvaSynthLocator.Resolve(xva);
        var xvaStatus = XvaSynthLocator.HasModels(resolvedXva)
            ? $"xVASynth models: {resolvedXva}"
            : xva is null
                ? $"xVASynth: not found (will try {XvaSynthLocator.DefaultRoot} and every Steam library)"
                : $"xVASynth: set to {resolvedXva} (no Skyrim models there yet)";

        var dest = workspace ?? AppUserSettings.DefaultWorkspaceRoot;
        var destStatus = workspace is null
            ? $"Output: {dest}\\builds\\<name>  (FollowerForge default)"
            : $"Output: {dest}\\<name>  (your folder; each follower is a subfolder you can install)";

        return new PathsSetupState(
            xva ?? "",
            workspace ?? "",
            xvaStatus + Environment.NewLine + destStatus,
            errors,
            warnings);
    }

    internal static bool LooksLikeGameData(string path, EnvironmentSnapshot? env)
    {
        var full = Path.GetFullPath(path);
        if (env is not null)
        {
            if (Under(full, env.GameRootPath) || Under(full, env.GameDataPath))
                return true;
        }

        var normalized = full.Replace('/', Path.DirectorySeparatorChar);
        return normalized.Contains(
                   Path.Combine("steamapps", "common", "Skyrim Special Edition"),
                   StringComparison.OrdinalIgnoreCase)
               && (normalized.EndsWith("Data", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains($"{Path.DirectorySeparatorChar}Data{Path.DirectorySeparatorChar}",
                       StringComparison.OrdinalIgnoreCase)
                   || normalized.EndsWith("Skyrim Special Edition", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool LooksLikeSaves(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Contains(Path.Combine("My Games", "Skyrim Special Edition"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Under(string full, string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        var trimmed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return full.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || string.Equals(full, trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
