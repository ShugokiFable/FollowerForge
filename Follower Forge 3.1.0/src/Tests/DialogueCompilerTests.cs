using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Pins the dialogue structure against what a shipped follower mod actually contains. Every
/// assertion here corresponds to something that fails silently in game when it is wrong: the
/// quest never starts, the topic never appears, or the audio file is never found.
/// </summary>
public sealed class DialogueCompilerTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static FollowerProfile ProfileWith(DialogueSpec dialogue) => new()
    {
        Name = "Test Follower",
        PluginName = "FF_TestFollower.esp",
        EditorIdPrefix = "FFTest",
        Race = new RecordRef(VanillaForms.NordRace.ToString()),
        VoiceType = new RecordRef(VanillaForms.FemaleEvenTonedVoice.ToString()),
        Class = new RecordRef(VanillaForms.CombatWarrior1HClass.ToString()),
        Placement = new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
        Dialogue = dialogue,
    };

    private static FollowerCompiler.CompileResult Compile(params DialogueLine[] lines) =>
        new FollowerCompiler(Log).Compile(
            ProfileWith(new DialogueSpec { Lines = lines }),
            location: null, voiceTypeEditorId: "FemaleEvenToned");

    [Fact]
    public void NoLines_ProducesNoDialogueRecordsAtAll()
    {
        var result = new FollowerCompiler(Log).Compile(ProfileWith(new DialogueSpec()), location: null);

        Assert.Null(result.Dialogue);
        Assert.Empty(result.Mod.Quests);
        Assert.Empty(result.Mod.DialogTopics);
    }

    [Fact]
    public void Quest_IsStartGameEnabled()
    {
        // Without this bit the quest never runs and not one line is ever heard.
        var quest = Assert.Single(Compile(new DialogueLine { Text = "Hello there." }).Mod.Quests);
        Assert.True(quest.Flags.HasFlag(Quest.Flag.StartGameEnabled));
    }

    [Fact]
    public void Speaker_IsScopedToThisNpcNotItsVoiceType()
    {
        // Shipped follower mods use GetIsVoiceType, which is only safe with a bespoke voice type.
        // On a stock voice that would give these lines to every NPC sharing it.
        var result = Compile(new DialogueLine { Text = "Hello there." });
        var quest = Assert.Single(result.Mod.Quests);

        var condition = Assert.Single(quest.DialogConditions);
        var data = Assert.IsType<GetIsIDConditionData>(condition.Data);
        Assert.Equal(result.NpcFormKey, data.Object.Link.FormKey);
    }

    [Fact]
    public void EachTrigger_GetsItsOwnTopicWithTheRightSubtype()
    {
        var result = Compile(
            new DialogueLine { Text = "Hi.", Trigger = DialogueTrigger.Hello },
            new DialogueLine { Text = "Bye.", Trigger = DialogueTrigger.Goodbye },
            new DialogueLine { Text = "Cold out.", Trigger = DialogueTrigger.Idle });

        var subtypes = result.Mod.DialogTopics.Select(t => t.Subtype).ToList();
        Assert.Equal(
            [DialogTopic.SubtypeEnum.Hello, DialogTopic.SubtypeEnum.Goodbye, DialogTopic.SubtypeEnum.Idle],
            subtypes);
        Assert.All(result.Mod.DialogTopics, t => Assert.Equal(DialogTopic.CategoryEnum.Misc, t.Category));
    }

    [Fact]
    public void LinesSharingATrigger_BecomeAlternativesInOneTopic()
    {
        var result = Compile(
            new DialogueLine { Text = "Hi." },
            new DialogueLine { Text = "Hello." },
            new DialogueLine { Text = "Yes?" });

        var topic = Assert.Single(result.Mod.DialogTopics);
        Assert.Equal(3, topic.Responses.Count);
        // Random is what makes the engine pick between them instead of always using the first.
        Assert.All(topic.Responses, r => Assert.True(r.Flags!.Flags.HasFlag(DialogResponses.Flag.Random)));
    }

    [Fact]
    public void PlayerTopic_GetsATopLevelBranchOrItNeverAppearsInTheMenu()
    {
        var result = Compile(new DialogueLine
        {
            Text = "Gladly.",
            Trigger = DialogueTrigger.PlayerTopic,
            Prompt = "Follow me.",
        });

        var topic = Assert.Single(result.Mod.DialogTopics);
        var branch = Assert.Single(result.Mod.DialogBranches);
        Assert.Equal("Follow me.", topic.Name?.String);
        Assert.Equal(DialogTopic.SubtypeEnum.Custom, topic.Subtype);
        Assert.Equal(branch.FormKey, topic.Branch.FormKey);
        Assert.Equal(topic.FormKey, branch.StartingTopic.FormKey);
        Assert.Equal(DialogBranch.Flag.TopLevel, branch.Flags);
    }

    [Fact]
    public void PlayerTopicWithoutAPrompt_IsRefusedRatherThanShippedUnclickable()
    {
        var result = Compile(new DialogueLine { Text = "…", Trigger = DialogueTrigger.PlayerTopic });

        Assert.Empty(result.Mod.DialogTopics);
        Assert.Empty(result.Dialogue!.Lines);
    }

    [Fact]
    public void VoicePath_MatchesTheNamingTheGameLooksFor()
    {
        var result = Compile(new DialogueLine { Text = "Hello there." });
        var line = Assert.Single(result.Dialogue!.Lines);
        var topic = Assert.Single(result.Mod.DialogTopics);
        var info = Assert.Single(topic.Responses);

        // sound\voice\<plugin>\<voice type>\<quest, cut to 10>_<topic, cut to 15>_<localID>_1.fuz
        // The truncation is what the engine looks for; full names are silently never found.
        var quest = result.Dialogue.QuestEditorId[..VoiceFileNaming.QuestNameLimit];
        var shortTopic = topic.EditorID![..VoiceFileNaming.TopicNameLimit];
        Assert.Equal(
            Path.Combine("sound", "voice", "FF_TestFollower.esp", "FemaleEvenToned",
                $"{quest}_{shortTopic}_00{info.FormKey.ID & 0xFFFFFF:X6}_1.fuz"),
            line.VoiceRelativePath);
    }

    [Fact]
    public void SameProfile_AllocatesTheSameFormIdsTwice()
    {
        DialogueLine[] lines =
        [
            new() { Text = "Hi.", Trigger = DialogueTrigger.Hello },
            new() { Text = "Onwards.", Trigger = DialogueTrigger.PlayerTopic, Prompt = "Let's go." },
            new() { Text = "Bye.", Trigger = DialogueTrigger.Goodbye },
        ];

        var a = Compile(lines);
        var b = Compile(lines);

        Assert.Equal(a.Dialogue!.Quest, b.Dialogue!.Quest);
        Assert.Equal(
            a.Dialogue.Lines.Select(l => l.VoiceRelativePath),
            b.Dialogue.Lines.Select(l => l.VoiceRelativePath));
    }

    [Fact]
    public void AddingDialogue_DoesNotMoveTheNpcOrItsPlacement()
    {
        // Dialogue is allocated last so an existing follower keeps her FormIDs when lines are
        // added — otherwise saves referencing her would break on rebuild.
        var without = new FollowerCompiler(Log).Compile(ProfileWith(new DialogueSpec()), location: null);
        var with = Compile(new DialogueLine { Text = "Hi." });

        Assert.Equal(without.NpcFormKey, with.NpcFormKey);
        Assert.Equal(without.PlacedFormKey, with.PlacedFormKey);
    }
}
