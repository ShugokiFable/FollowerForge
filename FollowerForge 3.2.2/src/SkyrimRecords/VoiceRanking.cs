using FollowerForge.Domain;

namespace FollowerForge.SkyrimRecords;

/// <summary>Where a voice sits in the wizard's list. Lower is more useful for a follower.</summary>
public enum VoiceTier
{
    /// <summary>Vanilla generic follower voices — 24 of them, and the safest choice.</summary>
    Vanilla = 0,
    /// <summary>Voices from an installed pack that supplies its own follower lines (SOS).</summary>
    VoicePack = 1,
    /// <summary>Modded voices that allow generic dialogue but nobody has verified.</summary>
    ModVoice = 2,
    /// <summary>Creature and unique voices with no follower dialogue at all.</summary>
    NoFollowerLines = 3,
}

/// <summary>
/// Orders the voice list by how useful a voice actually is, not alphabetically.
///
/// Measured on a real load order: 1,018 voice types — 24 vanilla, 17 from the SOS pack, 379
/// unverified modded, 598 creature/unique. Sorting those by name buries the whole SOS pack
/// ("VP_11_Aria" lands under V) among hundreds of mudcrab and draugr voices.
/// </summary>
public static class VoiceRanking
{
    public static VoiceTier TierOf(VoiceFollowerCapability capability) => capability switch
    {
        VoiceFollowerCapability.FullyCapable => VoiceTier.Vanilla,
        VoiceFollowerCapability.ResourceIntegrated => VoiceTier.VoicePack,
        VoiceFollowerCapability.NonFollowerCapable => VoiceTier.NoFollowerLines,
        _ => VoiceTier.ModVoice,
    };

    /// <summary>Parses the capability the record indexer stored, tolerating older catalogues.</summary>
    public static VoiceFollowerCapability CapabilityOf(string? storedCapability) => storedCapability switch
    {
        "FullyCapable" => VoiceFollowerCapability.FullyCapable,
        "ResourceIntegrated" => VoiceFollowerCapability.ResourceIntegrated,
        "NonFollowerCapable" => VoiceFollowerCapability.NonFollowerCapable,
        _ => VoiceFollowerCapability.Unknown,
    };

    /// <summary>Chip text for the row: short enough to read at a glance.</summary>
    public static string Badge(VoiceTier tier) => tier switch
    {
        VoiceTier.Vanilla => "VANILLA",
        VoiceTier.VoicePack => "SOS PACK",
        VoiceTier.ModVoice => "MOD",
        _ => "NO LINES",
    };

    /// <summary>Colour class for the chip. Matched by a XAML style selector, not a brush here.</summary>
    public static string BadgeKind(VoiceTier tier) => tier switch
    {
        VoiceTier.Vanilla => "good",
        VoiceTier.VoicePack => "ok",
        VoiceTier.ModVoice => "warn",
        _ => "dim",
    };

    /// <summary>The tiers offered when the user has not asked to see everything.</summary>
    public static bool IsFollowerReady(VoiceTier tier) => tier != VoiceTier.NoFollowerLines;

    /// <summary>
    /// Folder an installed voice pack keeps its files in. Existence of that folder is the only
    /// honest way to say a pack's lines are actually on disk.
    /// </summary>
    public static IEnumerable<string> VoiceFolders(string voiceTypeEditorId) =>
        VoiceClassifier.VoiceResourcePluginNames
            .Select(plugin => $@"sound\voice\{plugin}\{voiceTypeEditorId}\");
}
