using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Three things that were wrong at once for anyone building a beast follower in armour.
///
/// The transform picker silently excluded every creature race, which is the one class of race
/// the feature exists to offer. Choosing a legacy outfit together with any armour was a
/// guaranteed hard build error, because the validator checked the generated-outfit path even
/// when the compiler had taken the chosen-outfit path. And a RaceMenu preset's body shape has
/// never transferred, without the build ever saying so.
/// </summary>
public sealed class TransformAndOutfitTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static readonly string SourceRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static FollowerProfile Base(string name) => new()
    {
        Name = name,
        PluginName = $"FF_{name.Replace(" ", "")}.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec
        {
            Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()),
        },
    };

    // ---------- creature races are transformable ----------

    /// <summary>
    /// Every line that gives TransformRaceList its rows must use the creature-inclusive source.
    /// The bug was exactly this drifting apart: the focus card filled from vanilla+custom, the
    /// Expert Deck filled from the identity source, and neither ever showed a creature.
    /// </summary>
    [Fact]
    public void The_transform_race_picker_is_always_fed_the_creature_inclusive_source()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot, "Ui", "WizardWindow.axaml.cs"));

        var wiring = source.Split('\n')
            .Where(line => line.Contains("TransformRaceList", StringComparison.Ordinal))
            .Where(line => line.Contains("Fill(", StringComparison.Ordinal)
                        || line.Contains("Refilter(", StringComparison.Ordinal)
                        || line.Contains("SelectSingle(", StringComparison.Ordinal)
                        || line.Contains("PickerRecords(", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(wiring);
        foreach (var line in wiring)
            Assert.Contains("_transformRaces", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_transform_race_source_leads_with_creatures()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot, "Ui", "WizardWindow.axaml.cs"));
        var index = source.IndexOf("_transformRaces =>", StringComparison.Ordinal);

        Assert.True(index > 0, "_transformRaces is gone; the transform picker has no source of its own");
        var composition = source[index..source.IndexOf(';', index)];
        Assert.Contains("_creatureRaces", composition, StringComparison.Ordinal);
        Assert.True(
            composition.IndexOf("_creatureRaces", StringComparison.Ordinal)
                < composition.IndexOf("_vanillaRaces", StringComparison.Ordinal),
            "beast forms are the point of transforming and should lead the list");
    }

    /// <summary>
    /// The werewolf form this feature has always offered is itself classified as a creature —
    /// which is the proof that excluding creatures from the transform picker was incoherent.
    /// </summary>
    [Fact]
    public void The_vanilla_werewolf_form_is_a_creature_race()
    {
        var werewolf = new IndexedRecord
        {
            FormKey = VanillaForms.WerewolfBeastRace.ToString(),
            EditorId = "WerewolfBeastRace",
            DisplayName = "Werewolf",
            Type = IndexedRecordType.Race,
            SourcePlugin = "Skyrim.esm",
            WinningPlugin = "Skyrim.esm",
        };

        Assert.Equal(RaceClass.Creature, RaceSuitability.Classify(werewolf).Class);
        Assert.Empty(RaceSuitability.Offer([werewolf]));
        Assert.Single(RaceSuitability.Offer([werewolf], includeCreatures: true));
    }

    // ---------- legacy outfit + armour is not a build failure ----------

    /// <summary>
    /// The reported "Must be fixed": clothing in inventory AND a legacy outfit. The compiler
    /// honours the chosen outfit and leaves the pieces in her pack; the validator used to look
    /// for a Skyrim.esm outfit among this plugin's own records, find nothing, and report every
    /// piece as unwritten.
    /// </summary>
    [Fact]
    public void A_legacy_outfit_with_hand_picked_armor_builds()
    {
        var armor = new RecordRef("012E49:Skyrim.esm");
        var outfit = new RecordRef("0A6D7C:Skyrim.esm");
        var profile = Base("Outfit And Armor") with
        {
            Outfit = outfit,
            EquippedArmor = [armor],
            InventoryItems = [armor],
        };

        var report = new ValidationReport();
        FollowerValidator.Validate(new FollowerCompiler(Log).Compile(profile, location: null), profile, report);

        Assert.DoesNotContain(report.Findings, f => f.Code == "STARTING_ARMOR_NOT_WRITTEN");
        Assert.DoesNotContain(report.Findings, f => f.Code == "STARTING_EQUIPMENT_NOT_ASSIGNED");
        Assert.DoesNotContain(report.Findings, f => f.Code == "OUTFIT_NOT_ASSIGNED");
    }

    /// <summary>The generated-outfit path still has to be checked; only the branch changed.</summary>
    [Fact]
    public void Hand_picked_armor_without_an_outfit_still_reaches_the_starting_equipment_set()
    {
        var armor = new RecordRef("012E49:Skyrim.esm");
        var profile = Base("Armor Only") with
        {
            Outfit = null,
            EquippedArmor = [armor],
            InventoryItems = [armor],
        };

        var report = new ValidationReport();
        FollowerValidator.Validate(new FollowerCompiler(Log).Compile(profile, location: null), profile, report);

        Assert.DoesNotContain(report.Findings, f => f.Code == "STARTING_ARMOR_NOT_WRITTEN");
        Assert.DoesNotContain(report.Findings, f => f.Code == "STARTING_EQUIPMENT_NOT_ASSIGNED");
    }

    [Fact]
    public void Choosing_an_outfit_and_armor_explains_which_one_the_game_honours()
    {
        var report = new ValidationReport();
        var profile = Base("Both") with
        {
            Outfit = new RecordRef("0A6D7C:Skyrim.esm"),
            EquippedArmor = [new RecordRef("012E49:Skyrim.esm"), new RecordRef("012E4B:Skyrim.esm")],
        };

        FollowerBuilder.ReportOutfitOverridesArmor(profile, profile.Outfit!, catalog: null, report);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Equal("OUTFIT_OVERRIDES_ARMOR", finding.Code);
        Assert.Contains("2 armor piece(s)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_outfit_with_no_armor_picked_says_nothing()
    {
        var report = new ValidationReport();
        var profile = Base("Outfit Only") with { Outfit = new RecordRef("0A6D7C:Skyrim.esm") };

        FollowerBuilder.ReportOutfitOverridesArmor(profile, profile.Outfit!, catalog: null, report);

        Assert.Empty(report.Findings);
    }

    // ---------- the preset body shape that never transferred ----------

    [Fact]
    public void A_preset_that_shapes_a_body_warns_that_the_shape_stays_behind()
    {
        var report = new ValidationReport();
        FollowerBuilder.ReportPresetBodyShape(86, report);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Equal("PRESET_BODY_NOT_TRANSFERRED", finding.Code);
        Assert.Contains("86 body slider(s)", finding.Message, StringComparison.Ordinal);
        Assert.Contains("BodySlide", finding.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_preset_with_no_body_shape_stays_quiet()
    {
        var report = new ValidationReport();
        FollowerBuilder.ReportPresetBodyShape(0, report);
        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// RaceMenu writes the whole slider set whether it was touched or not — a reference preset
    /// on disk holds 119 entries of which 86 are actually shaped. Counting the untouched ones
    /// would warn on every single build.
    /// </summary>
    [Fact]
    public void Untouched_body_sliders_are_not_counted_as_a_body_shape()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_body_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Shaped.jslot");
        try
        {
            File.WriteAllText(path, """
                {
                  "bodyMorphs": [
                    { "name": "HipBone",        "keys": [ { "key": "OBody", "value": 0.659 } ] },
                    { "name": "ChubbyWaist",    "keys": [ { "key": "OBody", "value": 0.129 } ] },
                    { "name": "BreastFlatness", "keys": [ { "key": "OBody", "value": 0.0 } ] },
                    { "name": "NoKeysAtAll",    "keys": [] }
                  ]
                }
                """);

            Assert.Equal(2, new CharGenDiscovery(Log).ReadJslotBodyMorphCount(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void A_preset_without_a_body_block_counts_nothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_body_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Plain.jslot");
        try
        {
            File.WriteAllText(path, """{ "actor": { "weight": 50.0 } }""");
            Assert.Equal(0, new CharGenDiscovery(Log).ReadJslotBodyMorphCount(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
