namespace FollowerForge.Ui;

/// <summary>
/// Puts the Mutagen FormKey (the item's base ID plus its plugin) on a gear row so two
/// records with the same display name can be told apart without building the plugin.
/// Format matches the catalogue: "XXXXXX:Plugin.esp".
/// </summary>
public static class RecordIdDisplay
{
    public static string GearDetail(
        string formKey,
        string? editorId,
        string? displayName,
        string? rest)
    {
        var bits = new List<string> { formKey };
        if (!string.IsNullOrWhiteSpace(editorId)
            && !editorId.Equals(displayName, StringComparison.OrdinalIgnoreCase)
            && !editorId.Equals(formKey, StringComparison.OrdinalIgnoreCase))
        {
            bits.Add(editorId);
        }
        if (!string.IsNullOrWhiteSpace(rest))
            bits.Add(rest);
        return string.Join(" · ", bits);
    }
}
