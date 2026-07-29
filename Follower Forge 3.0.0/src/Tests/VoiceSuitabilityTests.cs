using FollowerForge.SkyrimRecords;

namespace FollowerForge.Tests;

public sealed class VoiceSuitabilityTests
{
    [Theory]
    // Every child voice type present in the reference install.
    [InlineData("FemaleChild")]
    [InlineData("MaleChild")]
    [InlineData("CYRFemaleChild")]
    [InlineData("CYRMaleChild")]
    [InlineData("DBM_PatronHenryChildVoice")]
    [InlineData("DBM_PatronVernaChildVoice")]
    [InlineData("RigmorChildVoice")]
    [InlineData("ZoraFairChildVoice")]
    [InlineData("mihaillos_creatures_greychilddialogue")]
    [InlineData("CrGiantVoiceforkid")]
    [InlineData("SetteMaleBoy01")]
    [InlineData("SetteMaleBoy02")]
    public void ChildVoices_AreRefused(string editorId) =>
        Assert.False(VoiceSuitability.IsAllowed(editorId));

    [Theory]
    [InlineData("FemaleEvenToned")]
    [InlineData("MaleNord")]
    [InlineData("FemaleYoungEager")]
    [InlineData("MaleYoungEager")]
    [InlineData("FemaleSultry")]
    [InlineData("MaleCommonerAccented")]
    public void AdultFollowerVoices_AreAllowed(string editorId) =>
        Assert.True(VoiceSuitability.IsAllowed(editorId));

    [Theory]
    // "boy"/"girl" inside a longer word must not trip the filter.
    [InlineData("BoyleVoice")]
    [InlineData("GirlingtonGuard")]
    public void WordsMerelyContainingBoyOrGirl_AreNotMistakenForChildren(string editorId) =>
        Assert.True(VoiceSuitability.IsAllowed(editorId));

    [Fact]
    public void MissingEditorId_IsNotTreatedAsAChildVoice() =>
        Assert.True(VoiceSuitability.IsAllowed(null));
}
