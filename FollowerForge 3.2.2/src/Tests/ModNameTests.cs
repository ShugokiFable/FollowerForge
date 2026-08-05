using FollowerForge.Ui;

namespace FollowerForge.Tests;

/// <summary>
/// Every armor, weapon and book row carries its source mod, so a name that reads badly reads
/// badly 20,000 times. All inputs here are real staging folder names off this machine.
/// </summary>
public sealed class ModNameTests
{
    [Theory]
    // Vortex's own suffix: mod id, version parts, unix timestamp.
    [InlineData("Call Of The Deep-154194-1-1-1754764218", "Call Of The Deep")]
    [InlineData("SOSVoicePack-92904-4-4-0-1759431689", "SOSVoicePack")]
    [InlineData("Ashtoreth Sea Queen's Raider 3BA-131546-1-1-1729448623", "Ashtoreth Sea Queen's Raider 3BA")]
    [InlineData("Milfactory Asset Hub - CBBE - CBBE Special - 3BA-80819-2-2-1719780310",
                "Milfactory Asset Hub - CBBE - CBBE Special - 3BA")]
    [InlineData("KS Hairdos SSE-6817-1-10-1747506054", "KS Hairdos SSE")]
    [InlineData("Complete Alchemy and Cooking Overhaul-19924-2-1-5-1714589473",
                "Complete Alchemy and Cooking Overhaul")]
    // Version segments are not always numeric.
    [InlineData("BHUNP EGIL Shadow of Akavir-72790-2022-b-1660144411", "BHUNP EGIL Shadow of Akavir")]
    public void VortexBookkeeping_IsRemoved(string folder, string expected) =>
        Assert.Equal(expected, ModNames.Pretty(folder));

    [Theory]
    // No trailing timestamp — nothing to strip, and guessing would eat real words.
    [InlineData("Relationship Dialogue Overhaul - RDO Final-1187-Final")]
    [InlineData("Bundled - High Poly Head v1.4.7z (v1.0.0)")]
    [InlineData("Skyrim.esm")]
    [InlineData("Mod 2000000000")]
    public void AnythingElse_IsLeftAlone(string folder) =>
        Assert.Equal(folder, ModNames.Pretty(folder));

    [Fact]
    public void AFolderThatIsNothingButBookkeeping_KeepsItsOriginalName()
    {
        // "1.3-107294-1-3-1703616712" is a real folder here; stripping leaves "1.3", and an
        // empty result would leave the row with no source at all.
        Assert.Equal("1.3", ModNames.Pretty("1.3-107294-1-3-1703616712"));
        Assert.Equal("-19924-2-1-5-1714589473", ModNames.Pretty("-19924-2-1-5-1714589473"));
    }

    [Fact]
    public void MissingSource_IsEmptyNotNull() => Assert.Equal("", ModNames.Pretty(null));
}
