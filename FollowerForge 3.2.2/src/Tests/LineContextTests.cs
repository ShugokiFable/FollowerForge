using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Contextual reactions — "she says this in caves", "she says this at night".
///
/// Both condition functions come from Laci Living Doll, which gates her dungeon, inn, home and
/// night dialogue on exactly LocationHasKeyword and GetCurrentTime.
/// </summary>
public sealed class LineContextTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private static readonly FormKey LocTypeDungeon = new(ModKey.FromFileName("Skyrim.esm"), 0x0130DB);

    private static FollowerCompiler.CompileResult Compile(params DialogueLine[] lines) =>
        new FollowerCompiler(Log).Compile(
            new FollowerProfile
            {
                Name = "Test Follower",
                PluginName = "FF_TestFollower.esp",
                Race = new RecordRef(VanillaForms.NordRace.ToString()),
                VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
                Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
                Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
                Dialogue = new DialogueSpec { Lines = lines },
            },
            location: null, voiceTypeEditorId: "FemaleEvenToned");

    private static IReadOnlyList<IConditionGetter> ConditionsOfFirstLine(FollowerCompiler.CompileResult r) =>
        r.Mod.DialogTopics.SelectMany(t => t.Responses).First().Conditions;

    [Fact]
    public void NoContext_MeansNoConditionsOnTheLine()
    {
        // The speaker test lives on the quest, so an unconditioned line carries nothing at all.
        var result = Compile(new DialogueLine { Text = "Hello." });
        Assert.Empty(ConditionsOfFirstLine(result));
    }

    [Fact]
    public void PlaceContext_BecomesALocationHasKeywordCondition()
    {
        var result = Compile(new DialogueLine
        {
            Text = "I hate caves.",
            Trigger = DialogueTrigger.Idle,
            Context = new LineContext { LocationKeyword = new RecordRef(LocTypeDungeon.ToString()) },
        });

        var condition = Assert.Single(ConditionsOfFirstLine(result));
        var data = Assert.IsType<LocationHasKeywordConditionData>(condition.Data);
        Assert.Equal(LocTypeDungeon, data.Keyword.Link.FormKey);
    }

    [Fact]
    public void NightIsTwoConditionsOredTogether_BecauseTheRangeWrapsMidnight()
    {
        var result = Compile(new DialogueLine
        {
            Text = "It's late.",
            Trigger = DialogueTrigger.Idle,
            Context = new LineContext { Time = Domain.TimeOfDay.Night },
        });

        var conditions = ConditionsOfFirstLine(result);
        Assert.Equal(2, conditions.Count);
        Assert.All(conditions, c => Assert.IsType<GetCurrentTimeConditionData>(c.Data));
        // Without the OR flag this would mean "after 20:00 AND before 06:00", which is never true.
        Assert.True(conditions[0].Flags.HasFlag(Condition.Flag.OR));
        Assert.False(conditions[1].Flags.HasFlag(Condition.Flag.OR));
    }

    [Fact]
    public void DaytimeIsAPlainRange_WithNoOr()
    {
        var result = Compile(new DialogueLine
        {
            Text = "Fine morning.",
            Trigger = DialogueTrigger.Idle,
            Context = new LineContext { Time = Domain.TimeOfDay.Day },
        });

        var conditions = ConditionsOfFirstLine(result);
        Assert.Equal(2, conditions.Count);
        Assert.All(conditions, c => Assert.False(c.Flags.HasFlag(Condition.Flag.OR)));
    }

    [Fact]
    public void PlaceAndTimeCombine()
    {
        var result = Compile(new DialogueLine
        {
            Text = "Caves are worse after dark.",
            Trigger = DialogueTrigger.Idle,
            Context = new LineContext
            {
                LocationKeyword = new RecordRef(LocTypeDungeon.ToString()),
                Time = Domain.TimeOfDay.Night,
            },
        });

        Assert.Equal(3, ConditionsOfFirstLine(result).Count);
    }

    [Theory]
    // Pairs read off working follower mods, because Mutagen's Subtype enum and the engine's
    // four-character code do not line up for the rarer subtypes.
    [InlineData(DialogueTrigger.Hello, "HELO")]
    [InlineData(DialogueTrigger.Goodbye, "GBYE")]
    [InlineData(DialogueTrigger.Idle, "IDLE")]
    [InlineData(DialogueTrigger.PlayerTopic, "CUST")]
    [InlineData(DialogueTrigger.EnteringCombat, "NOTC")]
    [InlineData(DialogueTrigger.LeavingCombat, "COTN")]
    [InlineData(DialogueTrigger.Taunt, "TAUT")]
    [InlineData(DialogueTrigger.Attack, "ATCK")]
    [InlineData(DialogueTrigger.PowerAttack, "POAT")]
    [InlineData(DialogueTrigger.Block, "BLOC")]
    [InlineData(DialogueTrigger.Hurt, "HIT_")]
    [InlineData(DialogueTrigger.Bleedout, "BLED")]
    [InlineData(DialogueTrigger.Death, "DETH")]
    public void EachTriggerWritesTheCodeTheEngineReads(DialogueTrigger trigger, string expected)
    {
        var result = Compile(new DialogueLine
        {
            Text = "Line.",
            Trigger = trigger,
            Prompt = trigger == DialogueTrigger.PlayerTopic ? "Talk." : null,
        });

        Assert.Equal(expected, result.Mod.DialogTopics.First().SubtypeName.Type);
    }
}
