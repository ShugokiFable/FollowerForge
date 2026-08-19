using System.Text.RegularExpressions;
using Avalonia.Media;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

/// <summary>
/// Guards the 3.6.0 theme-leak fix: badge chips and setup windows must draw from the live
/// theme tokens, never from hardcoded brushes. A hardcoded chip brush is what kept the yellow
/// warning chip frozen on Obsidian Gold colours while the rest of the app followed the theme.
/// </summary>
public sealed class ThemeCoverageTests
{
    [Fact]
    public void Every_theme_supplies_chip_status_and_on_status_ink()
    {
        foreach (var theme in Enum.GetValues<UiTheme>())
        {
            var palette = ThemeResources.Palette(theme);
            Assert.StartsWith("#", palette.Info);
            Assert.StartsWith("#", palette.OnStatus);
            // Ink must contrast against the status fills it sits on: identical is a bug.
            Assert.NotEqual(palette.Success, palette.OnStatus, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(palette.Warning, palette.OnStatus, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(palette.Info, palette.OnStatus, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(palette.Danger, palette.OnStatus, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ui_markup_contains_no_hardcoded_hex_colors()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(UiDir(), "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, "#[0-9A-Fa-f]{6,8}\\b"))
                offenders.Add($"{Path.GetFileName(file)}: {match.Value}");
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Row_chips_are_class_driven_and_theme_bound()
    {
        var xaml = File.ReadAllText(Path.Combine(UiDir(), "WizardWindow.axaml"));

        // The template switches chip classes from row bindings…
        Assert.Contains("Classes.chip-warn=\"{Binding ChipWarn}\"", xaml);
        Assert.Contains("Classes.chip-good=\"{Binding ChipGood}\"", xaml);
        // …and every chip class paints from a live theme token.
        foreach (var kind in new[] { "good", "ok", "warn", "bad", "dim" })
            Assert.Contains($"Border.chip-{kind}", xaml);
        Assert.DoesNotContain("{Binding BadgeBrush}", xaml);
        Assert.DoesNotContain("{Binding BadgeText}", xaml);

        // The rows themselves carry no colour code at all.
        var rows = File.ReadAllText(Path.Combine(UiDir(), "PickerItem.cs"));
        Assert.DoesNotContain("Color.Parse", rows);
        Assert.DoesNotContain("SolidColorBrush", rows);
    }

    [Theory]
    [InlineData("good", true, false, false, false, false)]
    [InlineData("ok", false, true, false, false, false)]
    [InlineData("warn", false, false, true, false, false)]
    [InlineData("bad", false, false, false, true, false)]
    [InlineData("dim", false, false, false, false, true)]
    [InlineData(null, false, false, false, false, true)]
    public void Picker_row_exposes_exactly_one_chip_class(
        string? kind, bool good, bool ok, bool warn, bool bad, bool dim)
    {
        var row = new PickerItem("Iron Sword", "00012E46:Skyrim.esm", badge: "VANILLA", badgeKind: kind);

        Assert.Equal(good, row.ChipGood);
        Assert.Equal(ok, row.ChipOk);
        Assert.Equal(warn, row.ChipWarn);
        Assert.Equal(bad, row.ChipBad);
        Assert.Equal(dim, row.ChipDim);
    }

    [Fact]
    public void Status_hues_are_visibly_distinct_between_themes()
    {
        // Pass 3: pass 2 made chips follow the theme token, but every token held nearly the
        // same amber/red hex, so a theme switch never repainted statuses in practice — the
        // "error is still yellow no matter the theme" report. Pin distinctness per theme.
        var themes = Enum.GetValues<UiTheme>();
        Assert.Equal(themes.Length, themes.Select(t => ThemeResources.Palette(t).Warning).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(themes.Length, themes.Select(t => ThemeResources.Palette(t).Danger).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_theme_supplies_translucent_soft_status_fills()
    {
        foreach (var theme in Enum.GetValues<UiTheme>())
        {
            var palette = ThemeResources.Palette(theme);
            foreach (var soft in new[] { palette.SuccessSoft, palette.InfoSoft, palette.WarningSoft, palette.DangerSoft })
            {
                var color = Color.Parse(soft);
                Assert.InRange(color.A, (byte)10, (byte)90); // a tint, never a solid block
            }
        }
    }

    private static string UiDir()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(src, "Ui");
    }
}
