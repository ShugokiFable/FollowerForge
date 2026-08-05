namespace FollowerForge.Domain;

/// <summary>
/// When a line is spoken. These map onto real Skyrim dialogue subtypes, verified against a
/// shipped follower mod rather than assumed.
/// </summary>
public enum DialogueTrigger
{
    /// <summary>Spoken when the player talks to her (subtype HELO).</summary>
    Hello = 0,
    /// <summary>Spoken as the conversation closes (subtype GBYE).</summary>
    Goodbye = 1,
    /// <summary>Ambient chatter to herself while following (subtype IDLE).</summary>
    Idle = 2,
    /// <summary>A topic the player picks from the menu; needs <see cref="DialogueLine.Prompt"/>.</summary>
    PlayerTopic = 3,

    // The combat set, taken from what shipped follower mods actually use. Karla Raven carries
    // exactly these, which is better evidence than the vanilla records: Mutagen's Subtype enum
    // and the engine's four-character code do NOT line up for the rarer values, so each of these
    // pairs was read off a working mod rather than inferred.
    /// <summary>Entering a fight (NOTC).</summary>
    EnteringCombat = 4,
    /// <summary>The fight is over (COTN).</summary>
    LeavingCombat = 5,
    /// <summary>Taunting an enemy (TAUT).</summary>
    Taunt = 6,
    /// <summary>Swinging at something (ATCK).</summary>
    Attack = 7,
    /// <summary>A power attack (POAT).</summary>
    PowerAttack = 8,
    /// <summary>Blocking a blow (BLOC).</summary>
    Block = 9,
    /// <summary>Being hurt (HIT_).</summary>
    Hurt = 10,
    /// <summary>Going down (BLED).</summary>
    Bleedout = 11,
    /// <summary>Dying (DETH).</summary>
    Death = 12,
}

/// <summary>
/// When a line applies, beyond the trigger itself. This is how a follower comments on caves,
/// inns or cities: the condition goes on the INFO, so one quest can hold every context.
///
/// Both condition functions are proven — Laci Living Doll gates her dungeon, inn, home and night
/// dialogue on exactly LocationHasKeyword and GetCurrentTime.
/// </summary>
public sealed record LineContext
{
    /// <summary>
    /// A LocType keyword the current location must carry: LocTypeDungeon, LocTypeInn,
    /// LocTypeCity, LocTypeAnimalDen and so on. Null means anywhere.
    /// </summary>
    public RecordRef? LocationKeyword { get; init; }

    /// <summary>Restricts the line to night (20:00-06:00) or to daylight.</summary>
    public TimeOfDay Time { get; init; } = TimeOfDay.Any;

    public bool IsEmpty => LocationKeyword is null && Time == TimeOfDay.Any;
}

public enum TimeOfDay { Any = 0, Day = 1, Night = 2 }

/// <summary>The eight emotions the engine can play on a face. Mirrors the game's own set.</summary>
public enum LineEmotion
{
    Neutral = 0, Anger = 1, Disgust = 2, Fear = 3, Sad = 4, Happy = 5, Surprise = 6, Puzzled = 7,
}

/// <summary>One spoken line.</summary>
public sealed record DialogueLine
{
    /// <summary>Subtitle text, and the text xVASynth speaks and FaceFXWrapper lipsyncs.</summary>
    public required string Text { get; init; }
    public DialogueTrigger Trigger { get; init; } = DialogueTrigger.Hello;
    public LineEmotion Emotion { get; init; } = LineEmotion.Neutral;
    /// <summary>Strength of the emotion, 0-100.</summary>
    public uint EmotionValue { get; init; } = 50;

    /// <summary>
    /// The menu entry the player clicks, for <see cref="DialogueTrigger.PlayerTopic"/> only.
    /// Lines sharing a prompt become alternative responses to the same topic.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>Optional: only say this in a certain kind of place, or at a certain time.</summary>
    public LineContext Context { get; init; } = new();
}

/// <summary>
/// Optional custom dialogue for a generated follower.
///
/// Scoping matters more than it looks: shipped follower mods gate their dialogue on
/// GetIsVoiceType because they ship a bespoke voice type nobody else has. Follower Forge lets
/// people pick a stock voice, so the same trick would put these lines in the mouth of every
/// Nord woman in Skyrim. We gate on the generated NPC instead — see DialogueCompiler.
/// </summary>
public sealed record DialogueSpec
{
    public IReadOnlyList<DialogueLine> Lines { get; init; } = [];

    /// <summary>
    /// Speak the lines with the user's xVASynth install and package .fuz audio. Off means the
    /// lines still appear as subtitles, but silently and with a still mouth.
    /// </summary>
    public bool Synthesize { get; init; } = true;

    /// <summary>Nothing to build when there are no lines.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasContent => Lines.Count > 0;
}
