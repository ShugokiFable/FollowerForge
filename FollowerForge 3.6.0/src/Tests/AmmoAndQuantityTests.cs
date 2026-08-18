using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Two things users could not do before 3.3.0: give an archer arrows at all, and carry more than
/// one of anything. Ammo also has to reach the OUTFIT, not just the inventory — otherwise she
/// empties the quiver once and never gets another arrow, which is how vanilla archers stay armed.
/// </summary>
public sealed class AmmoAndQuantityTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    /// <summary>Steel Arrow (0003BE1B) and a Potion of Minor Healing (0003EADE) are real records.</summary>
    private const string SteelArrow = "01397F:Skyrim.esm";   // verified: Faendal carries this
    private const string Potion = "03EADE:Skyrim.esm";
    private const string SteelArmor = "013951:Skyrim.esm";

    private static FollowerProfile Base() => new()
    {
        Name = "Test Archer",
        PluginName = "FF_TestArcher.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        Female = true,
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec
        {
            Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()),
        },
    };

    private static Mutagen.Bethesda.Skyrim.INpcGetter CompileNpc(FollowerProfile profile) =>
        new FollowerCompiler(Log).Compile(profile, location: null).Mod.Npcs.First();

    [Fact]
    public void Ammo_LandsInInventoryWithItsStackCount()
    {
        var npc = CompileNpc(Base() with { Ammo = [new RecordRef(SteelArrow, 100)] });

        var entry = Assert.Single(npc.Items!);
        Assert.Equal(100, entry.Item.Count);
        Assert.Equal(0x01397Fu, entry.Item.Item.FormKey.ID);
    }

    /// <summary>
    /// Vanilla keeps ammo OUT of the outfit: Faendal carries Steel Arrow (01397F) in his Items
    /// and wears FarmClothesOutfit02Variant. No vanilla outfit references an ammo record, so
    /// neither does ours.
    /// </summary>
    [Fact]
    public void Ammo_StaysOutOfTheOutfit_LikeEveryVanillaArcher()
    {
        var result = new FollowerCompiler(Log).Compile(
            Base() with
            {
                EquippedArmor = [new RecordRef(SteelArmor)],
                Ammo = [new RecordRef(SteelArrow, 100)],
            },
            location: null);

        var outfit = Assert.Single(result.Mod.Outfits);
        Assert.DoesNotContain(outfit.Items!, i => i.FormKey.ID == 0x01397F);
    }

    [Fact]
    public void InventoryCount_IsHonoured_NotSilentlyOne()
    {
        var npc = CompileNpc(Base() with { InventoryItems = [new RecordRef(Potion, 5)] });

        Assert.Equal(5, Assert.Single(npc.Items!).Item.Count);
    }

    [Fact]
    public void MissingCount_DefaultsToOne_SoOlderProfileJsonStillLoads()
    {
        Assert.Equal(1, new RecordRef(Potion).Count);
    }

    [Theory]
    [InlineData("Elina")]
    [InlineData("Ëlïna Ökÿ")]   // Western accents encode fine in 1252
    public void EncodableNames_AreNotFlagged(string name)
    {
        var report = new ValidationReport();
        BuildPipeline.FollowerBuilder.ReportUnencodableName(name, report);
        Assert.DoesNotContain(report.Findings, f => f.Code == "NAME_CHARACTERS_LOST");
    }

    [Theory]
    [InlineData("Аня")]               // Cyrillic
    [InlineData("Ζωή")]               // Greek
    public void UnencodableNames_AreCalledOut_NotSilentlyTurnedIntoQuestionMarks(string name)
    {
        var report = new ValidationReport();
        BuildPipeline.FollowerBuilder.ReportUnencodableName(name, report);

        var note = Assert.Single(report.Findings, f => f.Code == "NAME_CHARACTERS_LOST");
        Assert.Equal(ValidationSeverity.Warning, note.Severity);
    }

    [Fact]
    public void ZeroCount_ClampsToOne_RatherThanWritingAnEmptyStack()
    {
        var npc = CompileNpc(Base() with { InventoryItems = [new RecordRef(Potion, 0)] });

        Assert.Equal(1, Assert.Single(npc.Items!).Item.Count);
    }
}
