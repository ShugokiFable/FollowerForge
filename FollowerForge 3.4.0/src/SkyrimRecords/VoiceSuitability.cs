namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Keeps child voices out of generated followers.
///
/// Enforced in the build, not just hidden in the wizard, so a hand-written profile or a CLI build
/// cannot route around it. Measured against this install: 12 of 1,004 voice types match.
/// </summary>
public static class VoiceSuitability
{
    /// <summary>Matched anywhere in the EditorID.</summary>
    private static readonly string[] Anywhere = ["child", "kid"];

    /// <summary>
    /// Matched only when not part of a longer word, so "…Boy01" is caught while a name like
    /// "Boyle" is not.
    /// </summary>
    private static readonly string[] WholeWord = ["boy", "girl"];

    /// <summary>Why this voice cannot be used, or null when it is fine.</summary>
    public static string? Blocker(string? voiceTypeEditorId)
    {
        var edid = voiceTypeEditorId ?? "";
        if (edid.Length == 0) return null;

        foreach (var token in Anywhere)
            if (edid.Contains(token, StringComparison.OrdinalIgnoreCase))
                return "child voice";

        foreach (var token in WholeWord)
        {
            var at = edid.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                var after = at + token.Length;
                var continues = after < edid.Length && char.IsLower(edid[after]);
                if (!continues) return "child voice";
                at = edid.IndexOf(token, at + 1, StringComparison.OrdinalIgnoreCase);
            }
        }
        return null;
    }

    public static bool IsAllowed(string? voiceTypeEditorId) => Blocker(voiceTypeEditorId) is null;
}
