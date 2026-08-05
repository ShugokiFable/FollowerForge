using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;

namespace FollowerForge.Tests;

/// <summary>
/// Ordering the voice list alphabetically buried the entire SOS Voice Pack: on this load order
/// there are 1,018 voice types — 24 vanilla, 17 from the pack, 379 unverified modded and 598
/// creature/unique — so "VP_11_Aria" landed under V, hundreds of rows below the mudcrabs.
/// </summary>
public sealed class VoiceRankingTests
{
    [Fact]
    public void UsefulVoicesSortAboveTheRest()
    {
        var order = new[]
        {
            VoiceFollowerCapability.NonFollowerCapable,
            VoiceFollowerCapability.Unknown,
            VoiceFollowerCapability.ResourceIntegrated,
            VoiceFollowerCapability.FullyCapable,
        }
            .Select(VoiceRanking.TierOf)
            .OrderBy(t => t)
            .ToList();

        Assert.Equal(
            [VoiceTier.Vanilla, VoiceTier.VoicePack, VoiceTier.ModVoice, VoiceTier.NoFollowerLines],
            order);
    }

    [Fact]
    public void OnlyVoicesWithNoFollowerDialogueAreHiddenByDefault()
    {
        Assert.True(VoiceRanking.IsFollowerReady(VoiceTier.Vanilla));
        Assert.True(VoiceRanking.IsFollowerReady(VoiceTier.VoicePack));
        // An unverified mod voice may well work; hiding it would be us guessing on the user's behalf.
        Assert.True(VoiceRanking.IsFollowerReady(VoiceTier.ModVoice));
        Assert.False(VoiceRanking.IsFollowerReady(VoiceTier.NoFollowerLines));
    }

    [Fact]
    public void AnUnrecognisedCapability_FallsBackToTheUnverifiedTier_NotToVanilla()
    {
        // Catalogues built by an older version can carry a capability string we no longer know.
        Assert.Equal(VoiceFollowerCapability.Unknown, VoiceRanking.CapabilityOf("SomethingNew"));
        Assert.Equal(VoiceFollowerCapability.Unknown, VoiceRanking.CapabilityOf(null));
        Assert.Equal(VoiceTier.ModVoice, VoiceRanking.TierOf(VoiceRanking.CapabilityOf(null)));
    }

    [Fact]
    public void CapabilityStrings_MatchWhatTheIndexerActuallyWrites()
    {
        // The indexer serialises the enum by name, so these four are the only values on disk.
        foreach (var capability in Enum.GetValues<VoiceFollowerCapability>())
            Assert.Equal(capability, VoiceRanking.CapabilityOf(capability.ToString()));
    }

    [Fact]
    public void VoiceFolders_CoverEveryPluginThePackShipsVoicesUnder()
    {
        var folders = VoiceRanking.VoiceFolders("VP_11_Aria").ToList();

        // Verified on disk: the .fuz files live under the PART plugin, not under the master —
        // checking only SOSVoices.esm is why every pack voice read "not confirmed on disk".
        Assert.Contains(@"sound\voice\SOSVoices_Part1.esl\VP_11_Aria\", folders);
        Assert.Contains(@"sound\voice\SOSVoices_Part2.esl\VP_11_Aria\", folders);
        Assert.Contains(@"sound\voice\SOSVoices.esm\VP_11_Aria\", folders);
    }
}
