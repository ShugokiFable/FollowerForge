using System.Text.RegularExpressions;

namespace FollowerForge.Ui;

/// <summary>
/// Turns a Vortex staging folder name into the mod's actual name.
///
/// Vortex appends its download bookkeeping to the folder: the Nexus mod id, the version parts,
/// and a unix timestamp — "Call Of The Deep-154194-1-1-1754764218". In a list of 20,000 armors
/// that suffix is on every single row and drowns the part a person reads. Only that exact shape
/// is removed, so a mod whose real name ends in numbers survives untouched.
///
/// The build report deliberately keeps the full folder name: there it is provenance, and the
/// mod id is how someone finds the download again.
/// </summary>
public static partial class ModNames
{
    [GeneratedRegex(@"-\d+(?:-\w+)*-\d{10}$")]
    private static partial Regex VortexSuffix();

    public static string Pretty(string? stagingFolder)
    {
        if (string.IsNullOrWhiteSpace(stagingFolder)) return "";
        var trimmed = VortexSuffix().Replace(stagingFolder, "").Trim(' ', '-');
        return trimmed.Length == 0 ? stagingFolder : trimmed;
    }
}
