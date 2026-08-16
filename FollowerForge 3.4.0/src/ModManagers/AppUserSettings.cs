using System.Text;
using System.Text.Json;

namespace FollowerForge.ModManagers;

/// <summary>
/// Persisted GUI overrides that are not MO2-specific: xVASynth install and where built
/// followers are written. Empty strings mean "detect / use the default".
/// </summary>
public sealed record AppUserSelection(string? XvaSynthRoot, string? WorkspaceRoot);

/// <summary>Stores FollowerForge path overrides under LocalAppData\FollowerForge.</summary>
public static class AppUserSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "app-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static AppUserSelection Load(
        string? settingsDirectory = null,
        Action<string>? warning = null)
    {
        var path = SettingsPath(settingsDirectory);
        if (!File.Exists(path)) return new AppUserSelection(null, null);

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null)
            {
                warning?.Invoke($"App settings could not be read: {path}");
                return new AppUserSelection(null, null);
            }
            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                warning?.Invoke(
                    $"App settings schema {document.SchemaVersion} is not supported; expected {CurrentSchemaVersion}: {path}");
                return new AppUserSelection(null, null);
            }

            return new AppUserSelection(
                Clean(document.XvaSynthRoot),
                Clean(document.WorkspaceRoot));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            warning?.Invoke($"App settings could not be read: {path}. {ex.Message}");
            return new AppUserSelection(null, null);
        }
    }

    public static void Save(AppUserSelection selection, string? settingsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var directory = ResolveSettingsDirectory(settingsDirectory);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, FileName);
        var temporary = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        var document = new SettingsDocument(
            CurrentSchemaVersion,
            Clean(selection.XvaSynthRoot),
            Clean(selection.WorkspaceRoot));

        try
        {
            using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(JsonSerializer.Serialize(document, JsonOptions));
                writer.WriteLine();
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Clear(string? settingsDirectory = null)
    {
        var path = SettingsPath(settingsDirectory);
        if (File.Exists(path)) File.Delete(path);
    }

    public static string SettingsPath(string? settingsDirectory = null) =>
        Path.Combine(ResolveSettingsDirectory(settingsDirectory), FileName);

    public static string DefaultWorkspaceRoot =>
        Path.Combine(ManagerPreference.SettingsDirectory, "workspace");

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().Trim('"');
        return trimmed.Length == 0 ? null : Path.GetFullPath(trimmed);
    }

    private static string ResolveSettingsDirectory(string? settingsDirectory) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(settingsDirectory)
            ? ManagerPreference.SettingsDirectory
            : settingsDirectory);

    private sealed record SettingsDocument(int SchemaVersion, string? XvaSynthRoot, string? WorkspaceRoot);
}
