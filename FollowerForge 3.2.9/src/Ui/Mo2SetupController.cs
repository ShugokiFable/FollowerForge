using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.Ui;

public sealed record Mo2SetupState(
    Mo2Inspection Inspection,
    IReadOnlyList<string> Profiles,
    string Summary,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    Mo2UserSelection? Selection)
{
    public bool IsValid => Selection is not null && Errors.Count == 0;
}

/// <summary>
/// Testable, UI-agnostic validation for the MO2 setup dialog. It performs no indexing and writes
/// no settings; the window only displays this result and the wizard owns persistence/reload.
/// </summary>
public sealed class Mo2SetupController(ILogger log)
{
    public Mo2SetupState Inspect(string iniPath)
    {
        var inspection = new Mo2InstanceInspector(log).Inspect(iniPath);
        return State(inspection, inspection.Errors, selection: null);
    }

    public Mo2SetupState Validate(string iniPath, string? profileName)
    {
        var inspection = new Mo2InstanceInspector(log).Inspect(iniPath);
        var errors = inspection.Errors.ToList();
        Mo2UserSelection? selection = null;

        if (inspection.IsValid)
        {
            var requested = profileName?.Trim();
            var selected = inspection.Profiles.FirstOrDefault(name =>
                string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(requested))
            {
                errors.Add("Choose an MO2 profile.");
            }
            else if (selected is null)
            {
                errors.Add(
                    $"MO2 profile '{requested}' does not exist: "
                    + Path.Combine(inspection.ProfilesPath, requested));
            }
            else
            {
                var profileDir = Path.Combine(inspection.ProfilesPath, selected);
                var modlist = Path.Combine(profileDir, "modlist.txt");
                var plugins = Path.Combine(profileDir, "plugins.txt");
                var loadOrder = Path.Combine(profileDir, "loadorder.txt");
                if (!File.Exists(modlist))
                    errors.Add($"MO2 profile is missing modlist.txt: {modlist}");
                if (!File.Exists(plugins) && !File.Exists(loadOrder))
                    errors.Add($"MO2 profile needs plugins.txt or loadorder.txt: {plugins}; {loadOrder}");

                if (errors.Count == 0)
                    selection = new Mo2UserSelection(inspection.InstanceRoot, selected);
            }
        }

        return State(inspection, errors, selection);
    }

    private static Mo2SetupState State(
        Mo2Inspection inspection,
        IReadOnlyList<string> errors,
        Mo2UserSelection? selection)
    {
        var summary = string.IsNullOrWhiteSpace(inspection.InstanceRoot)
            ? "No MO2 instance resolved."
            : $"Instance: {inspection.InstanceRoot}{Environment.NewLine}"
              + $"Base directory: {inspection.BaseDirectory}{Environment.NewLine}"
              + $"Mods: {inspection.ModsPath}{Environment.NewLine}"
              + $"Profiles: {inspection.ProfilesPath}";
        return new Mo2SetupState(
            inspection,
            inspection.Profiles,
            summary,
            errors,
            inspection.Warnings,
            selection);
    }
}
