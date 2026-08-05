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
    public void ActualArmorWeaponsSpellsAndPerks_ReachTheNpcRecord()
    {
        var sword = "012EB7:Skyrim.esm";
        var armor = "012E49:Skyrim.esm";
        var spell = "012FCD:Skyrim.esm";   // Flames
        var perk = "0BABE4:Skyrim.esm";    // Armsman 1
        var npc = Compile(Base("Armed") with
        {
            InventoryItems = [new RecordRef(armor), new RecordRef(sword)],
            Spells = [new RecordRef(spell)],
            Perks = [new RecordRef(perk)],
        });

        Assert.Contains(npc.Items!, i => i.Item.Item.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(armor));
        Assert.Contains(npc.Items!, i => i.Item.Item.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(sword));
        Assert.Contains(npc.ActorEffect!, s => s.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(spell));
        Assert.Contains(npc.Perks!, p => p.Perk.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(perk));
    }

    [Fact]
    public void ActualArmor_GeneratesPrivateStartingEquipmentAndKeepsInventory()
    {
        var armor = new RecordRef("012E49:Skyrim.esm");
        var result = new FollowerCompiler(Log).Compile(Base("Inventory Gear") with
        {
            Outfit = null,
            EquippedArmor = [armor],
            InventoryItems = [armor],
        }, location: null);
        var npc = result.Mod.Npcs.First();
        var outfit = Assert.Single(result.Mod.Outfits);

        Assert.Equal(outfit.FormKey, npc.DefaultOutfit.FormKey);
        Assert.Contains(outfit.Items!, item =>
            item.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(armor.FormKey));
        Assert.Single(npc.Items!);
    }

    [Fact]
    public void RaceMenuRecordAppearance_ReachesTheNpcAndHairColorRecord()
    {
        var headPart = new RecordRef("05150F:Skyrim.esm");
        var result = new FollowerCompiler(Log).Compile(Base("Face") with
        {
            Appearance = new AppearanceSpec
            {
                Weight = 59f,
                HeadParts = [headPart],
                FaceMorphs = [0f, 0.2f, -1f],
                FacePresets = [13u, uint.MaxValue, 16u, 3u],
                TintLayers = [new TintLayerSpec(0, 0xFFFFFBFB)],
                HairColorArgb = 0xFF525357,
                SkinToneRgba = "FFFBFBFF",
                HeadTextureSet = new RecordRef("051648:Skyrim.esm"),
            },
        }, location: null);
        var npc = result.Mod.Npcs.First();

        Assert.Equal(59f, npc.Weight);
        // Without this she wears her race's default complexion under the preset's head.
        Assert.Equal(Mutagen.Bethesda.Plugins.FormKey.Factory("051648:Skyrim.esm"),
            npc.HeadTexture.FormKey);
        Assert.Contains(npc.HeadParts, part =>
            part.FormKey == Mutagen.Bethesda.Plugins.FormKey.Factory(headPart.FormKey));
        Assert.Equal(0.2f, npc.FaceMorph!.NoseUpVsDown);
        Assert.Equal(13u, npc.FaceParts!.Nose);
        Assert.Single(npc.TintLayers);
        Assert.False(npc.HairColor.IsNull);
        Assert.Single(result.Mod.Colors);
        var skin = npc.TextureLighting!.Value;
        Assert.Equal(255, skin.R);
        Assert.Equal(251, skin.G);
        Assert.Equal(251, skin.B);
    }

    [Fact]
    public void RaceMenuPartialTintAlpha_RoundTripsAsFractionalInterpolation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_tint_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = new FollowerCompiler(Log).Compile(Base("Tint") with
            {
                Appearance = new AppearanceSpec
                {
                    TintLayers = [new TintLayerSpec(11, 0x60A91FD2)],
                },
            }, location: null);
            var path = Path.Combine(dir, "FF_Tint.esp");
            new PluginWriter(Log).Write(result.Mod, path);

            using var reopened = SkyrimMod.CreateFromBinaryOverlay(path, VanillaForms.Release);
            var tint = Assert.Single(reopened.Npcs.First().TintLayers);
            // Skyrim stores TINV at two-decimal precision; 0x60 / 255 serializes as 0.38.
            Assert.InRange(tint.InterpolationValue!.Value, 0.37f, 0.39f);
            var color = tint.Color!.Value;
            Assert.Equal(0xA9, color.R);
            Assert.Equal(0x1F, color.G);
            Assert.Equal(0xD2, color.B);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
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
