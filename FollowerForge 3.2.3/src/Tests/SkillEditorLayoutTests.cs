using System.Reflection;
using System.Text.RegularExpressions;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

/// <summary>
/// Avalonia's NumericUpDown spends most of a narrow field on its two spinner buttons, so an
/// under-sized one shows the arrows and hides the number. This has now shipped twice — first in
/// the skill editors, then again in the evolution panel, where "Phases" rendered as two buttons
/// and no value.
///
/// The original test only checked the constant used by the code-generated skill editors, which
/// is exactly why it did not catch a spinner written by hand in XAML. The second test reads the
/// XAML itself and would have.
/// </summary>
public sealed class SkillEditorLayoutTests
{
    private const double MinimumUsableWidth = 140;

    [Fact]
    public void SkillValueEditors_ReserveEnoughWidthForValueAndSpinnerButtons()
    {
        var field = typeof(WizardWindow).GetField(
            "SkillValueEditorWidth",
            BindingFlags.NonPublic | BindingFlags.Static);

        var width = Assert.IsType<double>(field?.GetRawConstantValue());
        Assert.True(width >= MinimumUsableWidth, $"Skill value editor width {width} clips its numeric value.");
    }

    [Fact]
    public void EveryNumericSpinnerInTheWizardReservesEnoughWidth()
    {
        var xaml = File.ReadAllText(WizardXamlPath());
        var spinners = Regex.Matches(xaml, @"<NumericUpDown\b[^>]*>", RegexOptions.Singleline);

        Assert.NotEmpty(spinners);
        var undersized = new List<string>();

        foreach (Match spinner in spinners)
        {
            var name = Regex.Match(spinner.Value, @"x:Name=""([^""]+)""").Groups[1].Value;
            var declared = Regex.Match(spinner.Value, @"\b(?:MinWidth|Width)=""([\d.]+)""");

            if (!declared.Success)
            {
                undersized.Add($"{(name.Length == 0 ? "(unnamed)" : name)}: no MinWidth or Width");
                continue;
            }
            if (double.Parse(declared.Groups[1].Value) < MinimumUsableWidth)
                undersized.Add($"{name}: {declared.Groups[1].Value}");
        }

        Assert.True(undersized.Count == 0,
            "These numeric spinners are too narrow to show their value alongside the spinner "
            + $"buttons (need at least {MinimumUsableWidth}): {string.Join(", ", undersized)}");
    }

    /// <summary>Walks up from the test binaries to the checked-in XAML.</summary>
    private static string WizardXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Ui", "WizardWindow.axaml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate Ui/WizardWindow.axaml from " + AppContext.BaseDirectory);
    }
}
