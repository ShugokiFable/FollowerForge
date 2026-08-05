namespace FollowerForge.Domain;

/// <summary>
/// What she does with herself before you recruit her (and after you dismiss her).
///
/// Worth knowing: an NPC with no package is NOT frozen in place. The engine sandboxes actors by
/// default — 3,066 of Skyrim.esm's 5,118 NPCs carry no package on the record at all. Choosing a
/// package is about CONTROL: how far she strays, and whether she keeps to her spot or settles
/// wherever she happens to be.
/// </summary>
public enum IdleBehavior
{
    /// <summary>No package. The engine's default sandbox applies; she will still sit and eat.</summary>
    EngineDefault = 0,
    /// <summary>Keeps to her spot, wandering only a little (DefaultSandboxEditorLocation256).</summary>
    StaysPut = 1,
    /// <summary>Uses the room she was placed in (DefaultSandboxEditorLocation512).</summary>
    WandersNearby = 2,
    /// <summary>Settles wherever she currently is, not where she started (DefaultSandboxCurrentLocation256).</summary>
    SettlesWhereverSheIs = 3,
}

/// <summary>
/// How she regards the player. This is the RELA record's rank, and it is not cosmetic: mods that
/// key dialogue to relationship rank — Relationship Dialogue Overhaul above all — pick different
/// lines for a Friend than for a Confidant or a Rival.
/// </summary>
public enum RelationshipRank
{
    Lover = 0,
    Ally = 1,
    Confidant = 2,
    Friend = 3,
    Acquaintance = 4,
    Rival = 5,
    Foe = 6,
    Enemy = 7,
    Archnemesis = 8,
}

/// <summary>Her routine and how she feels about the player.</summary>
public sealed record BehaviorSpec
{
    public IdleBehavior Idle { get; init; } = IdleBehavior.WandersNearby;

    /// <summary>Adds the vanilla sleep package so she goes to bed at night (DefaultSleepEditorLoc24x8).</summary>
    public bool SleepsAtNight { get; init; } = true;

    /// <summary>
    /// Extra PACK records for people who know what they want. They are placed ahead of the
    /// sandbox package, because Skyrim runs the first package whose conditions hold and a broad
    /// sandbox would otherwise always win.
    /// </summary>
    public IReadOnlyList<RecordRef> ExtraPackages { get; init; } = [];

    public RelationshipRank Relationship { get; init; } = RelationshipRank.Ally;

    /// <summary>
    /// How she regards other people in the world. Shipped followers do this — Karla Raven carries
    /// a Friend relationship toward a vanilla NPC — and it is what makes a follower feel like she
    /// belongs somewhere rather than having appeared from nowhere.
    /// </summary>
    public IReadOnlyList<NpcRelationship> OtherRelationships { get; init; } = [];
}

/// <summary>A relationship between the follower and some other NPC.</summary>
public sealed record NpcRelationship
{
    public required RecordRef Npc { get; init; }
    public RelationshipRank Rank { get; init; } = RelationshipRank.Friend;
}
