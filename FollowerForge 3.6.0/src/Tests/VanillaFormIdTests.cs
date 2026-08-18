using FollowerForge.SkyrimRecords;
using Xunit;

namespace FollowerForge.Tests;

/// <summary>
/// Every FormID here was read back out of the installed Skyrim.esm with houseCARL and matched to
/// its EditorID. A wrong digit does not fail a build — it writes a link to a record that does not
/// exist, which only shows up as a dangling reference in xEdit or in game. PlayerFaction shipped
/// as 0x0000DB instead of 0x000DB1 through 3.2.5 for exactly that reason.
/// </summary>
public class VanillaFormIdTests
{
    [Theory]
    // faction / relationship
    [InlineData(0x05C84D, "PotentialFollowerFaction")]
    [InlineData(0x05C84E, "CurrentFollowerFaction")]
    [InlineData(0x05C84C, "DismissedFollowerFaction")]
    [InlineData(0x000DB1, "PlayerFaction")]
    [InlineData(0x019809, "PotentialMarriageFaction")]
    [InlineData(0x000007, "Player")]
    // vampire / werewolf
    [InlineData(0x0A82BB, "Vampire")]
    [InlineData(0x013796, "ActorTypeUndead")]
    [InlineData(0x0CDD84, "WerewolfBeastRace")]
    [InlineData(0x0F8208, "WerewolfChangeFX")]
    // enemy-to-ally
    [InlineData(0x00003B, "XMarker")]
    [InlineData(0x013F44, "EitherHand")]
    [InlineData(0x01BCC0, "BanditFaction")]
    [InlineData(0x02430D, "DraugrFaction")]
    [InlineData(0x026724, "WarlockFaction")]
    [InlineData(0x000013, "CreatureFaction")]
    [InlineData(0x02E893, "PredatorFaction")]
    // packages / placement / defaults
    [InlineData(0x01B217, "DefaultSandboxEditorLocation512")]
    [InlineData(0x09361E, "DefaultSandboxEditorLocation256")]
    [InlineData(0x0956B8, "DefaultSandboxCurrentLocation256")]
    [InlineData(0x01B210, "DefaultSleepEditorLoc24x8")]
    [InlineData(0x01A26F, "WhiterunWorld")]
    [InlineData(0x013746, "NordRace")]
    [InlineData(0x013ADD, "FemaleEvenToned")]
    [InlineData(0x013AD2, "MaleEvenToned")]
    [InlineData(0x013176, "CombatWarrior1H")]
    [InlineData(0x01DC10, "FarmClothesOutfit01")]
    public void VerifiedAgainstSkyrimEsm(uint id, string editorId)
    {
        // The pairing is the documentation; this locks the digits so a typo cannot slip back in.
        Assert.True(id is > 0 and <= 0xFFFFFF, $"{editorId} is not a valid Skyrim.esm FormID");
        Assert.Contains(id, AllVanillaIds());
    }

    [Fact]
    public void PlayerFaction_IsNotTheTransposedValueThatShippedThrough325()
    {
        Assert.Equal(0x000DB1u, VanillaForms.PlayerFaction.ID);
        Assert.NotEqual(0x0000DBu, VanillaForms.PlayerFaction.ID);
    }

    private static uint[] AllVanillaIds() =>
    [
        VanillaForms.PotentialFollowerFaction.ID, VanillaForms.CurrentFollowerFaction.ID,
        VanillaForms.DismissedFollowerFaction.ID, VanillaForms.PlayerFaction.ID,
        VanillaForms.PotentialMarriageFaction.ID, VanillaForms.PlayerNpc.ID,
        VanillaForms.VampireKeyword.ID, VanillaForms.ActorTypeUndeadKeyword.ID,
        VanillaForms.WerewolfBeastRace.ID, VanillaForms.WerewolfChangeFx.ID,
        VanillaForms.XMarker.ID, VanillaForms.EitherHandEquipType.ID,
        VanillaForms.BanditFaction.ID, VanillaForms.DraugrFaction.ID,
        VanillaForms.WarlockFaction.ID, VanillaForms.CreatureFaction.ID,
        VanillaForms.PredatorFaction.ID,
        VanillaForms.SandboxEditorLocation512.ID, VanillaForms.SandboxEditorLocation256.ID,
        VanillaForms.SandboxCurrentLocation256.ID, VanillaForms.SleepEditorLoc24x8.ID,
        VanillaForms.WhiterunWorld.ID,
        VanillaForms.NordRace.ID, VanillaForms.FemaleEvenTonedVoice.ID,
        VanillaForms.MaleEvenTonedVoice.ID, VanillaForms.CombatWarrior1HClass.ID,
        VanillaForms.FarmClothesOutfit.ID,
    ];
}
