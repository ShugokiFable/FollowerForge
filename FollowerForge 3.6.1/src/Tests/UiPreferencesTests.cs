using System.Text;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class UiPreferencesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FollowerForge-UiPreferencesTests-" + Guid.NewGuid().ToString("N"));

    public UiPreferencesTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Missing_file_returns_safe_schema_one_defaults()
    {
        var value = UiPreferencesStore.Load(Path.Combine(_root, "missing.json"));

        Assert.Equal(1, value.SchemaVersion);
        Assert.Equal(UiTheme.ObsidianGold, value.Theme);
        Assert.Equal(ExperienceMode.Guided, value.Experience);
        Assert.Equal(1320, value.Window.Width);
        Assert.Equal(900, value.Window.Height);
        Assert.False(value.Window.Maximized);
        Assert.False(value.ExpertIntroductionSeen);
    }

    [Fact]
    public void Malformed_and_unknown_values_warn_and_fall_back_safely()
    {
        var path = Path.Combine(_root, "ui-settings.json");
        File.WriteAllText(path, "{ definitely-not-json", new UTF8Encoding(false));
        var warnings = new List<string>();

        var malformed = UiPreferencesStore.Load(path, warnings.Add);

        Assert.Equal(UiPreferences.Default, malformed);
        Assert.Single(warnings);

        File.WriteAllText(path, """
            {
              "schemaVersion": 99,
              "theme": "Infrared",
              "experience": "Oracle",
              "window": { "width": 200, "height": 300, "maximized": true }
            }
            """, new UTF8Encoding(false));

        var unknown = UiPreferencesStore.Load(path, warnings.Add);

        Assert.Equal(UiTheme.ObsidianGold, unknown.Theme);
        Assert.Equal(ExperienceMode.Guided, unknown.Experience);
        Assert.Equal(1040, unknown.Window.Width);
        Assert.Equal(700, unknown.Window.Height);
        Assert.True(unknown.Window.Maximized);
        Assert.Equal(2, warnings.Count);
    }

    [Theory]
    [InlineData(UiTheme.ObsidianGold)]
    [InlineData(UiTheme.ArcaneAmethyst)]
    [InlineData(UiTheme.NordicFrost)]
    [InlineData(UiTheme.ForgeTeal)]
    [InlineData(UiTheme.Light)]
    public void Preferences_round_trip_every_theme_without_a_utf8_bom(UiTheme theme)
    {
        var path = Path.Combine(_root, $"{theme}.json");
        var expected = new UiPreferences(
            1,
            theme,
            ExperienceMode.Expert,
            new WindowPlacement(1512, 944, true),
            ExpertIntroductionSeen: true);

        UiPreferencesStore.Save(expected, path);
        var bytes = File.ReadAllBytes(path);
        var actual = UiPreferencesStore.Load(path);

        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Failed_atomic_replacement_preserves_the_previous_valid_document()
    {
        var path = Path.Combine(_root, "locked.json");
        var original = UiPreferences.Default;
        UiPreferencesStore.Save(original, path);

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = Record.Exception(() => UiPreferencesStore.Save(
                original with { Theme = UiTheme.Light }, path));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
        }

        Assert.Equal(original, UiPreferencesStore.Load(path));
        Assert.Empty(Directory.GetFiles(_root, "locked.json.tmp-*"));
    }

    [Fact]
    public void Every_theme_supplies_the_complete_semantic_palette()
    {
        var palettes = Enum.GetValues<UiTheme>().Select(ThemeResources.Palette).ToList();

        Assert.Equal(5, palettes.Count);
        Assert.Equal(5, palettes.Select(p => p.Accent).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(palettes, palette =>
        {
            Assert.StartsWith("#", palette.Window);
            Assert.StartsWith("#", palette.Surface);
            Assert.StartsWith("#", palette.ElevatedSurface);
            Assert.StartsWith("#", palette.Border);
            Assert.StartsWith("#", palette.Text);
            Assert.StartsWith("#", palette.MutedText);
            Assert.StartsWith("#", palette.Accent);
            Assert.StartsWith("#", palette.AccentHover);
            Assert.StartsWith("#", palette.AccentPressed);
            Assert.StartsWith("#", palette.Success);
            Assert.StartsWith("#", palette.Info);
            Assert.StartsWith("#", palette.Warning);
            Assert.StartsWith("#", palette.Danger);
            Assert.StartsWith("#", palette.OnStatus);
            Assert.StartsWith("#", palette.Focus);
            Assert.StartsWith("#", palette.Selection);
            Assert.StartsWith("#", palette.Overlay);
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
