using System.Text;

namespace FollowerForge.Tests;

/// <summary>
/// Guards against mojibake in the source tree.
///
/// This happened for real: editing WizardWindow.axaml through PowerShell 5.1's
/// Get-Content/Set-Content silently mangled it. Get-Content reads a BOM-less UTF-8 file using the
/// system ANSI code page, so every em dash, ellipsis and arrow came back as three Latin-1
/// characters, and Set-Content then wrote those back as UTF-8 - double-encoding them. Nothing
/// failed to build; the wizard simply showed the garbage to the user.
///
/// Any text tool that is not encoding-aware can do this, so the check is on the files rather
/// than on the tool. The markers below are built from escapes on purpose: spelling them
/// literally would make this file fail its own test.
/// </summary>
public sealed class SourceEncodingTests
{
    /// <summary>
    /// UTF-8 re-encodings of the mis-decoded lead bytes. U+00E2 U+20AC covers the punctuation
    /// range (em dash, ellipsis, quotes, arrows); the U+00C3 pairs cover accented letters.
    /// </summary>
    private static readonly string[] MojibakeMarkers =
    [
        "\u00e2\u20ac",
        "\u00c3\u201a",
        "\u00c3\u00a2",
    ];

    private static readonly string[] Extensions = [".cs", ".axaml", ".csproj", ".psc", ".md", ".txt"];

    [Fact]
    public void NoSourceFileContainsMojibake()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            var text = File.ReadAllText(file, Encoding.UTF8);
            for (var i = 0; i < MojibakeMarkers.Length; i++)
            {
                if (!text.Contains(MojibakeMarkers[i], StringComparison.Ordinal)) continue;
                offenders.Add($"{Path.GetRelativePath(root, file)} (marker #{i})");
                break;
            }
        }

        Assert.True(offenders.Count == 0,
            "These files look double-encoded - they were probably edited by a tool that read "
            + "UTF-8 as ANSI: " + string.Join(", ", offenders));
    }

    /// <summary>Walks up from the test binaries to the folder holding the source projects.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ui", "WizardWindow.axaml"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the source root from " + AppContext.BaseDirectory);
    }
}
