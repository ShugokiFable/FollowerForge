using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Phase5Tests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    [Fact]
    public void CombatStyleAnalyzer_ExposesRawValuesAndInfersTags()
    {
        var cs = new CombatStyle(new FormKey(ModKey.FromFileName("Test.esp"), 0x800), VanillaForms.Release)
        {
            OffensiveMult = 1.2f,
            DefensiveMult = 0.25f,
            GroupOffensiveMult = 1.6f,
            EquipmentScoreMultMelee = 2.0f,
            EquipmentScoreMultRanged = 0.5f,
        };
        var detail = CombatStyleAnalyzer.Analyze(cs);

        // Raw values are always exposed, unchanged.
        Assert.Equal(1.2f, detail.OffensiveMult);
        Assert.Equal(0.25f, detail.DefensiveMult);
        // Inferred tags.
        Assert.Contains("aggressive", detail.Tags);
        Assert.Contains("prefers-melee", detail.Tags);
        Assert.Contains("group-tactics", detail.Tags);
    }

    [Fact]
    public void VoiceClassifier_VanillaFollowerVoice_IsFullyCapable()
    {
        var v = new VoiceType(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x13ADD), VanillaForms.Release)
        {
            EditorID = "FemaleEvenToned",
        };
        Assert.Equal(VoiceFollowerCapability.FullyCapable, VoiceClassifier.Classify(v).Capability);
    }

    [Fact]
    public void VoiceClassifier_SosVoice_IsResourceIntegratedAndVerifiesFiles()
    {
        var v = new VoiceType(new FormKey(ModKey.FromFileName("SOSVoices.esm"), 0x800), VanillaForms.Release)
        {
            EditorID = "VP_11_Aria",
        };
        var d = VoiceClassifier.Classify(v, voiceFileExists: path => path.Contains("VP_11_Aria"));
        Assert.Equal(VoiceFollowerCapability.ResourceIntegrated, d.Capability);
        Assert.True(d.FilesVerified);
    }

    [Fact]
    public void VoiceClassifier_UnknownModdedVoice_IsUnknownNotHidden()
    {
        var v = new VoiceType(new FormKey(ModKey.FromFileName("MyMod.esp"), 0x800), VanillaForms.Release)
        {
            EditorID = "MyCustomVoice",
            Flags = VoiceType.Flag.AllowDefaultDialog,
        };
        Assert.Equal(VoiceFollowerCapability.Unknown, VoiceClassifier.Classify(v).Capability);
    }

    [Fact]
    public void Detection_UsesExactPluginNames_NoShortSubstringFalsePositives()
    {
        var plugins = new[] { "Immersive Speechcraft.esp", "Water Effects Fix Exterior Module.esp", "nwsFollowerFramework.esp" };
        var report = Detection.Detect(plugins, _ => false);
        // NFF matched; AFT/EFF NOT falsely matched from "craft"/"Effects".
        Assert.Contains(report.Frameworks, f => f.Framework.Contains("Nether"));
        Assert.DoesNotContain(report.Frameworks, f => f.Framework.Contains("Amazing"));
        Assert.DoesNotContain(report.Frameworks, f => f.Framework.Contains("Extensible"));
    }

    [Fact]
    public void Detection_FindsBodySystemsFromStagingAndAssets()
    {
        var report = Detection.Detect(
            enabledPlugins: ["HIMBO.esp"],
            assetExists: prefix => prefix.Contains("bodyslide"),
            stagingModNames: ["CBBE 3BA whatever", "BHUNP pack"]);
        Assert.Contains(report.BodySystems, b => b.System == "BodySlide");
        Assert.Contains(report.BodySystems, b => b.System == "HIMBO");
        Assert.Contains(report.BodySystems, b => b.System.Contains("3BA"));
        Assert.Contains(report.BodySystems, b => b.System == "BHUNP");
    }

    [Fact]
    public void FollowerCompiler_ClonesCombatStyleIntoPlugin_NeverEditsOriginal()
    {
        var source = new CombatStyle(new FormKey(ModKey.FromFileName("Overhaul.esp"), 0x812), VanillaForms.Release)
        {
            OffensiveMult = 1.5f,
            DefensiveMult = 0.5f,
            EditorID = "Overhaul_Aggressive",
        };
        var profile = new FollowerProfile
        {
            Name = "Clone Test",
            PluginName = "FF_CloneTest.esp",
            Race = new RecordRef(VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
            Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
            CombatStyle = new CombatStyleChoice { Style = new RecordRef(source.FormKey.ToString()), CloneIntoPlugin = true },
        };

        var result = new FollowerCompiler(Log).Compile(profile, location: null, combatStyleToClone: source);

        // A new CSTY exists in the plugin with the source's values (original untouched).
        var cloned = Assert.Single(result.Mod.CombatStyles);
        Assert.Equal(1.5f, cloned.OffensiveMult);
        Assert.InRange(cloned.FormKey.ID, 0x800u, 0xFFFu);
        Assert.NotEqual(source.FormKey, cloned.FormKey);
        // The NPC points at the clone, not the original.
        Assert.Equal(cloned.FormKey, result.Mod.Npcs.First().CombatStyle.FormKey);
    }
}
