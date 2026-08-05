using FollowerForge.Domain;
using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.SkyrimRecords;

public sealed record VoiceDetail(
    VoiceFollowerCapability Capability,
    bool AllowDefaultDialog,
    bool FilesVerified,
    string? SourcePlugin,
    string Reason);

/// <summary>
/// Classifies voice types into the four follower-capability buckets. Vanilla generic follower
/// voices are Fully-capable; the installed SOS Voice Pack voices are Resource-integrated (with
/// on-disk file verification); modded voices we cannot vouch for are Unknown (never hidden).
/// </summary>
public static class VoiceClassifier
{
    /// <summary>Vanilla voice EditorIDs that carry full generic follower dialogue (verified set).</summary>
    private static readonly HashSet<string> VanillaFollowerVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        "FemaleEvenToned", "FemaleYoungEager", "FemaleSultry", "FemaleCondescending",
        "FemaleCommander", "FemaleNord", "FemaleElfHaughty", "FemaleOrc", "FemaleKhajiit", "FemaleArgonian",
        "MaleEvenToned", "MaleNord", "MaleBrute", "MaleDarkElf", "MaleElfHaughty",
        "MaleOrc", "MaleKhajiit", "MaleArgonian", "MaleYoungEager", "MaleEvenTonedAccented",
        "MaleCommander", "MaleSlyCynical", "MaleCommoner", "MaleCommonerAccented",
    };

    /// <summary>Plugins that ship follower-capable voice dialogue as a resource.</summary>
    private static readonly HashSet<string> VoiceResourcePlugins = new(StringComparer.OrdinalIgnoreCase)
    {
        "SOSVoices.esm", "SOSVoices_Part1.esl", "SOSVoices_Part2.esl",
    };

    /// <summary>
    /// The same plugins, in order, for callers that need to look for their voice folders on disk.
    /// The voice files live under the PART plugin's folder, not the master's, so all three have
    /// to be tried.
    /// </summary>
    public static IReadOnlyList<string> VoiceResourcePluginNames { get; } =
        ["SOSVoices.esm", "SOSVoices_Part1.esl", "SOSVoices_Part2.esl"];

    /// <param name="voiceFileExists">
    /// Checks whether a sound\voice\&lt;plugin&gt;\&lt;voiceType&gt; folder has any file in the
    /// asset index — used to verify SOS voice resources are actually installed.
    /// </param>
    public static VoiceDetail Classify(IVoiceTypeGetter vtype, Func<string, bool>? voiceFileExists = null)
    {
        var edid = vtype.EditorID ?? "";
        var sourcePlugin = vtype.FormKey.ModKey.FileName.String;
        var allowsDefault = vtype.Flags.HasFlag(VoiceType.Flag.AllowDefaultDialog);

        if (VanillaFollowerVoices.Contains(edid))
            return new VoiceDetail(VoiceFollowerCapability.FullyCapable, allowsDefault, true, sourcePlugin,
                "Vanilla generic follower voice");

        if (VoiceResourcePlugins.Contains(sourcePlugin))
        {
            var verified = voiceFileExists is null
                ? false
                : VoiceResourcePlugins.Any(p => voiceFileExists($@"sound\voice\{p}\{edid}"));
            return new VoiceDetail(VoiceFollowerCapability.ResourceIntegrated, allowsDefault, verified, sourcePlugin,
                verified
                    ? "Simply Open Source Voice Pack (voice files present)"
                    : "Simply Open Source Voice Pack (voice files not confirmed on disk)");
        }

        if (!allowsDefault)
            return new VoiceDetail(VoiceFollowerCapability.NonFollowerCapable, false, false, sourcePlugin,
                "Does not allow default dialogue (likely a unique/creature voice)");

        return new VoiceDetail(VoiceFollowerCapability.Unknown, allowsDefault, false, sourcePlugin,
            "Modded voice with default dialogue allowed — follower coverage unverified");
    }
}
