using System.Text.Json;
using System.Text.Json.Nodes;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

public sealed class FollowerStatsTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile SampleProfile() => new()
    {
        Name = "Stat Test",
        PluginName = "FF_StatTest.esp",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec
        {
            Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()),
        },
    };

    [Fact]
    public void DefaultProfile_KeepsAutoCalcStats()
    {
        var result = new FollowerCompiler(Log).Compile(SampleProfile(), null);
        var npc = Assert.Single(result.Mod.Npcs);

        Assert.True(npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.AutoCalcStats));
        Assert.Equal(FollowerStatsMode.AutoCalculate, SampleProfile().Stats.Mode);
    }

    [Fact]
    public void EveryPreset_ContainsAllEighteenSkillsWithinEditorRange()
    {
        foreach (var preset in Enum.GetValues<FollowerStatPreset>())
        {
            var stats = FollowerStats.FromPreset(preset);
            Assert.Equal(18, stats.Skills.Count);
            Assert.All(Enum.GetValues<FollowerSkill>(), skill =>
                Assert.InRange(stats.GetSkill(skill), (byte)0, (byte)100));
        }
    }

    [Fact]
    public void CustomStats_WriteAndReopenWithExactDnamValues()
    {
        var preset = FollowerStats.FromPreset(FollowerStatPreset.TwoHandedWarrior);
        var skills = new Dictionary<FollowerSkill, byte>(preset.Skills)
        {
            [FollowerSkill.TwoHanded] = 91,
            [FollowerSkill.Restoration] = 73,
            [FollowerSkill.Speech] = 24,
        };
        var profile = SampleProfile() with
        {
            Stats = preset with
            {
                Skills = skills,
                Health = 444,
                Magicka = 333,
                Stamina = 222,
            },
        };

        var dir = Path.Combine(Path.GetTempPath(), "ff_stats_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = new FollowerCompiler(Log).Compile(profile, null);
            var path = Path.Combine(dir, profile.PluginName);
            new PluginWriter(Log).Write(result.Mod, path);

            using var reopened = SkyrimMod.CreateFromBinaryOverlay(path, VanillaForms.Release);
            var npc = Assert.Single(reopened.Npcs);
            Assert.False(npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.AutoCalcStats));
            Assert.NotNull(npc.PlayerSkills);
            Assert.Equal((ushort)444, npc.PlayerSkills!.Health);
            Assert.Equal((ushort)333, npc.PlayerSkills.Magicka);
            Assert.Equal((ushort)222, npc.PlayerSkills.Stamina);

            foreach (var (profileSkill, skyrimSkill) in FollowerSkillMap.All)
            {
                Assert.Equal(profile.Stats.GetSkill(profileSkill), npc.PlayerSkills.SkillValues[skyrimSkill]);
                Assert.Equal((byte)0, npc.PlayerSkills.SkillOffsets[skyrimSkill]);
            }

            var validation = new ValidationReport();
            FollowerValidator.ValidateFile(path, profile, validation);
            Assert.False(validation.HasErrors,
                string.Join(Environment.NewLine, validation.Findings.Select(f => $"{f.Code}: {f.Message}")));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void ProfileWithoutStats_DeserializesAsAutomaticForBackwardCompatibility()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_profile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "legacy.json");
        try
        {
            var node = JsonNode.Parse(JsonSerializer.Serialize(SampleProfile(), ProfileIo.Options))!.AsObject();
            Assert.True(node.Remove("Stats"));
            File.WriteAllText(path, node.ToJsonString(ProfileIo.Options));

            var loaded = ProfileIo.Load(path);
            Assert.Equal(FollowerStatsMode.AutoCalculate, loaded.Stats.Mode);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
