using System.Text;
using System.Text.Json;

namespace FollowerForge.ModManagers;

public sealed record Mo2UserSelection(string InstanceRoot, string ProfileName);

/// <summary>
/// Stores only FollowerForge's manual MO2 selection. No MO2-owned INI or profile file is touched.
/// </summary>
public static class Mo2UserSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "mo2-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Mo2UserSelection? Load(
        string? settingsDirectory = null,
        Action<string>? warning = null)
    {
        var path = SettingsPath(settingsDirectory);
        if (!File.Exists(path)) return null;

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null)
            {
                warning?.Invoke($"MO2 settings could not be read: {path}");
                return null;
            }
            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                warning?.Invoke(
                    $"MO2 settings schema {document.SchemaVersion} is not supported; expected {CurrentSchemaVersion}: {path}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(document.InstanceRoot)
                || string.IsNullOrWhiteSpace(document.ProfileName))
            {
                warning?.Invoke($"MO2 settings are missing instanceRoot or profileName: {path}");
                return null;
            }

            return new Mo2UserSelection(
                Path.GetFullPath(document.InstanceRoot.Trim().Trim('"')),
                document.ProfileName.Trim());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            warning?.Invoke($"MO2 settings could not be read: {path}. {ex.Message}");
            return null;
        }
    }

    public static void Save(Mo2UserSelection selection, string? settingsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (string.IsNullOrWhiteSpace(selection.InstanceRoot))
            throw new ArgumentException("MO2 instance root is required.", nameof(selection));
        if (string.IsNullOrWhiteSpace(selection.ProfileName))
            throw new ArgumentException("MO2 profile name is required.", nameof(selection));

        var directory = ResolveSettingsDirectory(settingsDirectory);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, FileName);
        var temporary = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        var document = new SettingsDocument(
            CurrentSchemaVersion,
            Path.GetFullPath(selection.InstanceRoot.Trim().Trim('"')),
            selection.ProfileName.Trim());

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

    private static string ResolveSettingsDirectory(string? settingsDirectory) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(settingsDirectory)
            ? ManagerPreference.SettingsDirectory
            : settingsDirectory);

    private sealed record SettingsDocument(int SchemaVersion, string InstanceRoot, string ProfileName);
}
