using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Builds the dialogue half of a follower plugin: one dialogue quest, a topic per trigger, and
/// an INFO per line — plus the exact voice-file path each line's audio must be written to.
///
/// The structure is copied from a working follower mod (Laci Living Doll, 8 quests / 43 topics /
/// 1,674 voice files) rather than reconstructed from documentation, with one deliberate
/// departure described on <see cref="AddSpeakerCondition"/>.
/// </summary>
public sealed class DialogueCompiler(ILogger log)
{
    /// <summary>One line, resolved to the record and the file its audio belongs in.</summary>
    public sealed record PlannedLine(
        FormKey Info,
        string Text,
        string VoiceRelativePath,
        DialogueTrigger Trigger);

    public sealed record DialogueResult(
        FormKey Quest,
        string QuestEditorId,
        IReadOnlyList<PlannedLine> Lines);

    /// <param name="voiceTypeEditorId">
    /// EditorID of the follower's voice type — it names the folder the game looks in
    /// (sound\voice\&lt;plugin&gt;\&lt;voice type&gt;\), so a wrong value means silence.
    /// </param>
    public DialogueResult? Compile(SkyrimMod mod, FormKey npc, string prefix, DialogueSpec spec,
        string voiceTypeEditorId, string pluginFileName)
    {
        if (!spec.HasContent) return null;

        var questEditorId = $"{prefix}Dialogue";
        var quest = new Quest(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = questEditorId,
            FormVersion = 44,
        };
        mod.Quests.Add(quest);
        // 0x11 is what the Creation Kit writes for a start-game-enabled dialogue quest: the named
        // StartGameEnabled bit plus bit 4, which Mutagen has no name for but every CK-authored
        // quest carries. Without StartGameEnabled the quest never runs and no line is ever heard.
        quest.Flags = (Quest.Flag)0x0011;
        quest.Priority = 0;
        quest.Type = Quest.TypeEnum.None;
        AddSpeakerCondition(quest, npc);

        var planned = new List<PlannedLine>();

        // Fixed ordering (trigger, then prompt, then declaration order) keeps FormID allocation
        // deterministic: the same profile must always produce a byte-identical plugin.
        var groups = spec.Lines
            .Select((line, index) => (line, index))
            .GroupBy(x => (x.line.Trigger, Prompt: x.line.Prompt ?? string.Empty))
            .OrderBy(g => g.Key.Trigger)
            .ThenBy(g => g.Key.Prompt, StringComparer.Ordinal)
            .ToList();

        var topicNumber = 0;
        foreach (var group in groups)
        {
            var (trigger, prompt) = group.Key;
            if (trigger == DialogueTrigger.PlayerTopic && prompt.Length == 0)
            {
                log.Warning("Skipping player topic with no prompt text — it would be unclickable");
                continue;
            }

            var topicEditorId = $"{prefix}Topic{++topicNumber:D2}";

            // A player-facing topic needs a top-level branch, or it never appears in the menu.
            // The Creation Kit allocates the branch before the topic; we match that order.
            DialogBranch? branch = null;
            if (trigger == DialogueTrigger.PlayerTopic)
            {
                branch = new DialogBranch(mod.GetNextFormKey(), VanillaForms.Release)
                {
                    EditorID = $"{prefix}Branch{topicNumber:D2}",
                    FormVersion = 44,
                    Flags = DialogBranch.Flag.TopLevel,
                };
                branch.Quest.SetTo(quest.FormKey);
                mod.DialogBranches.Add(branch);
            }

            var topic = new DialogTopic(mod.GetNextFormKey(), VanillaForms.Release)
            {
                EditorID = topicEditorId,
                FormVersion = 44,
            };
            mod.DialogTopics.Add(topic);
            topic.Quest.SetTo(quest.FormKey);
            topic.Priority = 50;
            (topic.Category, topic.Subtype, topic.SubtypeName) = Shape(trigger);
            if (trigger == DialogueTrigger.PlayerTopic)
            {
                topic.Name = prompt;
                topic.Branch.SetTo(branch!.FormKey);
            }

            foreach (var (line, _) in group)
            {
                var info = new DialogResponses(mod.GetNextFormKey(), VanillaForms.Release)
                {
                    FormVersion = 44,
                    Flags = new DialogResponseFlags { Flags = InfoFlags(trigger) },
                };
                AddContext(info, line.Context);
                info.Responses.Add(new DialogResponse
                {
                    Text = line.Text,
                    ResponseNumber = 1,
                    Emotion = (Emotion)line.Emotion,
                    EmotionValue = Math.Clamp(line.EmotionValue, 0u, 100u),
                });
                topic.Responses.Add(info);

                planned.Add(new PlannedLine(
                    info.FormKey,
                    line.Text,
                    VoiceFileNaming.FuzPath(pluginFileName, voiceTypeEditorId,
                        questEditorId, topicEditorId, info.FormKey),
                    trigger));
            }

            if (branch is not null && topic.Responses.Count > 0)
                branch.StartingTopic.SetTo(topic.FormKey);
        }

        log.Information("Compiled dialogue: quest {Quest}, {Topics} topics, {Lines} lines",
            questEditorId, topicNumber, planned.Count);
        return new DialogueResult(quest.FormKey, questEditorId, planned);
    }

    /// <summary>
    /// Narrows one line to a kind of place or a time of day.
    ///
    /// These go on the INFO rather than the quest, so a single quest holds every context — Laci
    /// Living Doll uses one quest per context instead, which is why she needs eight of them.
    /// Both functions are hers: LocationHasKeyword and GetCurrentTime.
    ///
    /// Night is expressed as two conditions OR'd together (after 20:00 or before 06:00), because
    /// the range wraps past midnight and a single comparison cannot express that.
    /// </summary>
    private static void AddContext(DialogResponses info, LineContext context)
    {
        if (context.IsEmpty) return;

        if (context.LocationKeyword is { } keyword)
        {
            var condition = new ConditionFloat
            {
                CompareOperator = CompareOperator.EqualTo,
                ComparisonValue = 1,
                Data = new LocationHasKeywordConditionData { RunOnType = Condition.RunOnType.Subject },
            };
            ((LocationHasKeywordConditionData)condition.Data).Keyword.Link
                .SetTo(FormKey.Factory(keyword.FormKey));
            info.Conditions.Add(condition);
        }

        switch (context.Time)
        {
            case Domain.TimeOfDay.Day:
                info.Conditions.Add(Time(CompareOperator.GreaterThanOrEqualTo, 6, or: false));
                info.Conditions.Add(Time(CompareOperator.LessThan, 20, or: false));
                break;
            case Domain.TimeOfDay.Night:
                // OR: after 20:00 OR before 06:00.
                info.Conditions.Add(Time(CompareOperator.GreaterThanOrEqualTo, 20, or: true));
                info.Conditions.Add(Time(CompareOperator.LessThan, 6, or: false));
                break;
        }
    }

    private static ConditionFloat Time(CompareOperator op, float hour, bool or) => new()
    {
        CompareOperator = op,
        ComparisonValue = hour,
        Flags = or ? Condition.Flag.OR : default,
        Data = new GetCurrentTimeConditionData { RunOnType = Condition.RunOnType.Subject },
    };

    /// <summary>
    /// Restricts every line in the quest to this one NPC.
    ///
    /// Shipped follower mods use GetIsVoiceType here, which is safe only because they ship a
    /// unique voice type. FollowerForge offers stock voices, where that condition would match
    /// hundreds of NPCs and put the user's custom lines in every one of their mouths. GetIsID on
    /// the generated NPC is exact regardless of which voice was chosen.
    /// </summary>
    private static void AddSpeakerCondition(Quest quest, FormKey npc)
    {
        quest.DialogConditions.Add(new ConditionFloat
        {
            CompareOperator = CompareOperator.EqualTo,
            ComparisonValue = 1,
            Data = new GetIsIDConditionData { RunOnType = Condition.RunOnType.Subject }
                .Also(d => d.Object.Link.SetTo(npc)),
        });
    }

    /// <summary>
    /// Category/subtype pair for a trigger. Both are stored and the game reads the four-character
    /// code, so each pair below was read off a WORKING follower mod rather than derived from
    /// Mutagen's enum — the two do not line up for the rarer subtypes. Hello/HELO, Goodbye/GBYE,
    /// Idle/IDLE and Custom/CUST are confirmed by Laci Living Doll; the combat set is confirmed
    /// by ColdSun's Visions - Karla Raven, which ships exactly these.
    /// </summary>
    private static (DialogTopic.CategoryEnum, DialogTopic.SubtypeEnum, RecordType) Shape(DialogueTrigger t) => t switch
    {
        DialogueTrigger.Hello => (DialogTopic.CategoryEnum.Misc, DialogTopic.SubtypeEnum.Hello, RecordTypes.HELO),
        DialogueTrigger.Goodbye => (DialogTopic.CategoryEnum.Misc, DialogTopic.SubtypeEnum.Goodbye, RecordTypes.GBYE),
        DialogueTrigger.Idle => (DialogTopic.CategoryEnum.Misc, DialogTopic.SubtypeEnum.Idle, RecordTypes.IDLE),
        DialogueTrigger.EnteringCombat => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.NormalToCombat, RecordTypes.NOTC),
        DialogueTrigger.LeavingCombat => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.CombatToNormal, RecordTypes.COTN),
        DialogueTrigger.Taunt => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Taunt, RecordTypes.TAUT),
        DialogueTrigger.Attack => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Attack, RecordTypes.ATCK),
        DialogueTrigger.PowerAttack => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.PowerAttack, RecordTypes.POAT),
        DialogueTrigger.Block => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Block, RecordTypes.BLOC),
        DialogueTrigger.Hurt => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Hit, RecordTypes.HIT),
        DialogueTrigger.Bleedout => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Bleedout, RecordTypes.BLED),
        DialogueTrigger.Death => (DialogTopic.CategoryEnum.Combat, DialogTopic.SubtypeEnum.Death, RecordTypes.DETH),
        _ => (DialogTopic.CategoryEnum.Topic, DialogTopic.SubtypeEnum.Custom, RecordTypes.CUST),
    };

    /// <summary>
    /// Random lets the engine pick between alternative lines instead of always using the first.
    /// A player topic also ends the conversation, matching how shipped followers behave.
    /// </summary>
    private static DialogResponses.Flag InfoFlags(DialogueTrigger t) =>
        t == DialogueTrigger.PlayerTopic
            ? DialogResponses.Flag.Random | DialogResponses.Flag.Goodbye
            : DialogResponses.Flag.Random;

    private static class RecordTypes
    {
        public static readonly RecordType HELO = new("HELO");
        public static readonly RecordType GBYE = new("GBYE");
        public static readonly RecordType IDLE = new("IDLE");
        public static readonly RecordType CUST = new("CUST");
        public static readonly RecordType NOTC = new("NOTC");
        public static readonly RecordType COTN = new("COTN");
        public static readonly RecordType TAUT = new("TAUT");
        public static readonly RecordType ATCK = new("ATCK");
        public static readonly RecordType POAT = new("POAT");
        public static readonly RecordType BLOC = new("BLOC");
        /// <summary>Note the trailing underscore — the code really is "HIT_".</summary>
        public static readonly RecordType HIT = new("HIT_");
        public static readonly RecordType BLED = new("BLED");
        public static readonly RecordType DETH = new("DETH");
    }
}

file static class Fluent
{
    /// <summary>Small helper so a condition's parameters can be set inline in an initializer.</summary>
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
