using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Guards against profile fields that look supported but never reach the plugin. Marriage was
/// exactly that: the flag existed, the compiler ignored it, and the follower silently could not
/// be courted.
/// </summary>
public sealed class FollowerOptionsTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile Base(string name) => new()
    {
        Name = name,
        PluginName = $"FF_{name.Replace(" ", "")}.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
    };

    private static INpcGetter Compile(FollowerProfile p) =>
        new FollowerCompiler(Log).Compile(p, location: null).Mod.Npcs.First();

    [Fact]
    public void Marriageable_AddsThePotentialMarriageFaction()
    {
        var npc = Compile(Base("Wed") with { Marriageable = true });
        Assert.Contains(npc.Factions,
            f => f.Faction.FormKey == VanillaForms.PotentialMarriageFaction && f.Rank == 0);
    }

    [Fact]
    public void NotMarriageable_LeavesTheFactionOff()
    {
        var npc = Compile(Base("Unwed") with { Marriageable = false });
        Assert.DoesNotContain(npc.Factions, f => f.Faction.FormKey == VanillaForms.PotentialMarriageFaction);
    }

    [Fact]
    public void SkinArmor_SetsWornArmor_SoHerBodyIsPinned()
    {
        var skin = "012E46:Skyrim.esm";   // any ARMO; the point is that it is applied
        var npc = Compile(Base("Body") with { SkinArmor = new RecordRef(skin) });
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory(skin), npc.WornArmor.FormKey);
    }

    [Fact]
    public void NoSkinArmor_LeavesRaceDefault_SoSheUsesTheInstalledBodyMod()
    {
        var npc = Compile(Base("Default Body"));
        Assert.True(npc.WornArmor.IsNull);
    }

    [Fact]
    public void WeaponsAndSpells_ReachTheNpcRecord()
    {
        var sword = "0139B4:Skyrim.esm";
        var spell = "01CB07:Skyrim.esm";   // FormKey strings are six hex digits, not eight
        var npc = Compile(Base("Armed") with
        {
            InventoryItems = [new RecordRef(sword)],
            Spells = [new RecordRef(spell)],
        });

        Assert.Contains(npc.Items!, i => i.Item.Item.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(sword));
        Assert.Contains(npc.ActorEffect!, s => s.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(spell));
    }

    /// <summary>
    /// Every follower must still be recruitable regardless of the optional extras above.
    /// </summary>
    [Fact]
    public void OptionalExtras_DoNotDisturbTheFollowerFactions()
    {
        var npc = Compile(Base("Everything") with
        {
            Marriageable = true,
            Essential = true,
            InventoryItems = [new RecordRef("0139B4:Skyrim.esm")],
        });

        Assert.Contains(npc.Factions, f => f.Faction.FormKey == VanillaForms.PotentialFollowerFaction && f.Rank == 0);
        Assert.Contains(npc.Factions, f => f.Faction.FormKey == VanillaForms.CurrentFollowerFaction && f.Rank == -1);
    }
}
