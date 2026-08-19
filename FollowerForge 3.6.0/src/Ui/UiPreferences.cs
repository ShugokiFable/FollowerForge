using System.Text;
using System.Text.Json;

namespace FollowerForge.Ui;

public enum UiTheme
{
    ObsidianGold,
    ArcaneAmethyst,
    NordicFrost,
    ForgeTeal,
    Light,
}

public enum ExperienceMode
{
    Guided,
    Expert,
}

public sealed record WindowPlacement(double Width, double Height, bool Maximized);

public sealed record UiPreferences(
    int SchemaVersion,
    UiTheme Theme,
    ExperienceMode Experience,
    WindowPlacement Window,
    bool ExpertIntroductionSeen)
{
    public static UiPreferences Default { get; } = new(
        1,
        UiTheme.ObsidianGold,
        ExperienceMode.Guided,
        new WindowPlacement(1320, 900, false),
        ExpertIntroductionSeen: false);
}

public static class UiPreferencesStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FollowerForge",
        "ui-settings.json");

    public static UiPreferences Load(string? path = null, Action<string>? warning = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return UiPreferences.Default;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The root value must be an object.");

            var root = document.RootElement;
            var hadFallback = false;
            var schema = ReadInt(root, "schemaVersion", 1);
            if (schema != 1)
            {
                schema = 1;
                hadFallback = true;
            }

            var theme = ReadEnum(root, "theme", UiTheme.ObsidianGold, ref hadFallback);
            var experience = ReadEnum(root, "experience", ExperienceMode.Guided, ref hadFallback);

            var window = UiPreferences.Default.Window;
            if (root.TryGetProperty("window", out var windowNode) && windowNode.ValueKind == JsonValueKind.Object)
            {
                window = new WindowPlacement(
                    Math.Max(1040, ReadDouble(windowNode, "width", window.Width)),
                    Math.Max(700, ReadDouble(windowNode, "height", window.Height)),
                    ReadBool(windowNode, "maximized", window.Maximized));
            }

            var expertSeen = ReadBool(root, "expertIntroductionSeen", false);
            if (hadFallback)
                warning?.Invoke($"Some values in {path} were unknown and safe UI defaults were used.");

            return new UiPreferences(schema, theme, experience, window, expertSeen);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            warning?.Invoke($"Could not read UI preferences from {path}: {ex.Message}");
            return UiPreferences.Default;
        }
    }

    public static void Save(UiPreferences value, string? path = null)
    {
        path ??= DefaultPath;
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        var normalized = value with
        {
            SchemaVersion = 1,
            Window = value.Window with
            {
                Width = Math.Max(1040, value.Window.Width),
                Height = Math.Max(700, value.Window.Height),
            },
        };
        var payload = new
        {
            schemaVersion = normalized.SchemaVersion,
            theme = normalized.Theme.ToString(),
            experience = normalized.Experience.ToString(),
            window = new
            {
                width = normalized.Window.Width,
                height = normalized.Window.Height,
                maximized = normalized.Window.Maximized,
            },
            expertIntroductionSeen = normalized.ExpertIntroductionSeen,
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static TEnum ReadEnum<TEnum>(
        JsonElement root,
        string property,
        TEnum fallback,
        ref bool hadFallback)
        where TEnum : struct, Enum
    {
        if (root.TryGetProperty(property, out var node)
            && node.ValueKind == JsonValueKind.String
            && Enum.TryParse<TEnum>(node.GetString(), ignoreCase: true, out var value)
            && Enum.IsDefined(value))
            return value;

        if (root.TryGetProperty(property, out _)) hadFallback = true;
        return fallback;
    }

    private static int ReadInt(JsonElement root, string property, int fallback) =>
        root.TryGetProperty(property, out var node) && node.TryGetInt32(out var value) ? value : fallback;

    private static double ReadDouble(JsonElement root, string property, double fallback) =>
        root.TryGetProperty(property, out var node) && node.TryGetDouble(out var value) ? value : fallback;

    private static bool ReadBool(JsonElement root, string property, bool fallback) =>
        root.TryGetProperty(property, out var node) && node.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? node.GetBoolean()
            : fallback;
}
