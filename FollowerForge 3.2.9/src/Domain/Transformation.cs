namespace FollowerForge.Domain;

/// <summary>What she turns into when a fight starts.</summary>
public enum TransformKind
{
    /// <summary>No transformation. The default.</summary>
    None = 0,
    /// <summary>Werewolf, using the game's own beast race and change effect.</summary>
    Werewolf = 1,
    /// <summary>
    /// Something from an installed mod: any race to become, any spell to cast, or both. This is
    /// the shape the transforming followers already use — a spell cast a few seconds into combat.
    /// </summary>
    Custom = 2,
}

/// <summary>
/// EXPERIMENTAL. She changes form in combat and changes back afterwards.
///
/// Like the evolution system this adds a script, so it is off unless asked for. Nothing is
/// authored by us: the race and spells are records the user already has, so a missing one leaves
/// an ordinary follower rather than a broken one.
/// </summary>
public sealed record TransformSpec
{
    public TransformKind Kind { get; init; } = TransformKind.None;

    /// <summary>Race to become. Filled in automatically for <see cref="TransformKind.Werewolf"/>.</summary>
    public RecordRef? BeastRace { get; init; }

    /// <summary>Visual effect cast as she changes. Defaults to the game's own for a werewolf.</summary>
    public RecordRef? TransformFx { get; init; }

    /// <summary>Optional spell cast once she has changed — a mod's transformation, a summon, a buff.</summary>
    public RecordRef? OnTransformSpell { get; init; }

    /// <summary>Change back when the fight ends.</summary>
    public bool RevertOutOfCombat { get; init; } = true;

    /// <summary>Beat before transforming, so it does not happen mid-swing.</summary>
    public float DelaySeconds { get; init; } = 2f;

    /// <summary>
    /// Nothing is emitted unless she actually has something to turn into. A "custom"
    /// transformation with neither a race nor a spell would attach a script that does nothing.
    /// </summary>
    public bool IsUsable => Kind switch
    {
        TransformKind.None => false,
        TransformKind.Werewolf => true,
        _ => BeastRace is not null || OnTransformSpell is not null,
    };
}
