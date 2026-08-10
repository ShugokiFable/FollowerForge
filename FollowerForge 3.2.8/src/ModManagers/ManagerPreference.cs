namespace FollowerForge.ModManagers;

/// <summary>
/// Persists which mod manager the GUI prefers when both Vortex and MO2 are present.
/// Stored under LocalAppData\FollowerForge so it survives restarts without env vars.
/// </summary>
public static class ManagerPreference
{
    public const string PreferMo2FileName = "prefer-mo2";

    public static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FollowerForge");

    public static string PreferMo2MarkerPath =>
        Path.Combine(SettingsDirectory, PreferMo2FileName);

    /// <summary>True when the user (or FFORGE_PREFER_MO2) wants MO2 tried before Vortex.</summary>
    public static bool PreferMo2
    {
        get
        {
            if (IsTruthy(Environment.GetEnvironmentVariable("FFORGE_PREFER_MO2")))
                return true;
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE")))
                return true;
            return File.Exists(PreferMo2MarkerPath);
        }
    }

    public static void SetPreferMo2(bool preferMo2)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var path = PreferMo2MarkerPath;
        if (preferMo2)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, "1\n");
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void TogglePreferMo2() => SetPreferMo2(!PreferMo2);

    private static bool IsTruthy(string? value) =>
        value is not null
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
}
