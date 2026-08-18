namespace FollowerForge.Domain;

/// <summary>
/// Third-person pronouns for wizard copy. Female is the historical default; Male is the
/// other option the Sex box already asks for on step 1.
/// </summary>
public readonly record struct FollowerPronouns(
    string Subject,
    string Object,
    string Possessive,
    string PossessiveNoun,
    string Reflexive)
{
    public static FollowerPronouns Female { get; } = new("she", "her", "her", "hers", "herself");
    public static FollowerPronouns Male { get; } = new("he", "him", "his", "his", "himself");

    public static FollowerPronouns FromFemale(bool female) => female ? Female : Male;

    public string SubjectCap => Capitalize(Subject);
    public string ObjectCap => Capitalize(Object);
    public string PossessiveCap => Capitalize(Possessive);

    /// <summary>
    /// Fills named slots. Possessive and object are different tokens on purpose — both are
    /// "her" for a woman and "his"/"him" for a man, so a naive her→him replace is wrong.
    /// </summary>
    public string Fill(string template) =>
        template
            .Replace("{Subject}", SubjectCap, StringComparison.Ordinal)
            .Replace("{subject}", Subject, StringComparison.Ordinal)
            .Replace("{Object}", ObjectCap, StringComparison.Ordinal)
            .Replace("{object}", Object, StringComparison.Ordinal)
            .Replace("{Possessive}", PossessiveCap, StringComparison.Ordinal)
            .Replace("{possessive}", Possessive, StringComparison.Ordinal)
            .Replace("{possessiveNoun}", PossessiveNoun, StringComparison.Ordinal)
            .Replace("{reflexive}", Reflexive, StringComparison.Ordinal);

    private static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
