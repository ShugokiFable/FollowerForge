namespace FollowerForge.Domain;

/// <summary>
/// EXPERIMENTAL. Lets a follower start timid and grow into the job, the way Melana the War
/// Maiden does: she counts the fights she comes through beside you and steps up in phases,
/// gaining confidence and combat skill each time.
///
/// This is the only feature that puts a SCRIPT in a generated follower. Everything else
/// Follower Forge writes is plain records, which is a real safety property — scripts persist in
/// save files in a way records do not. So it is off unless asked for, and it is built to fail
/// harmlessly: if the script never runs she is simply a normal follower at her starting values.
/// </summary>
public sealed record EvolutionSpec
{
    /// <summary>Off unless the user explicitly turns it on.</summary>
    public bool Enabled { get; init; }

    /// <summary>How many phases she passes through, including the one she starts in.</summary>
    public int Phases { get; init; } = 3;

    /// <summary>Fights survived before she moves up a phase.</summary>
    public int CombatsPerPhase { get; init; } = 25;

    /// <summary>Confidence in phase one — 0 cowardly … 4 foolhardy.</summary>
    public byte StartConfidence { get; init; }

    /// <summary>Confidence once she reaches the final phase.</summary>
    public byte EndConfidence { get; init; } = 4;

    /// <summary>Combat skill added for each phase beyond the first.</summary>
    public int SkillPerPhase { get; init; } = 15;

    public float HealthPerPhase { get; init; } = 30f;
    public float StaminaPerPhase { get; init; } = 20f;
    public float MagickaPerPhase { get; init; } = 10f;

    /// <summary>Nothing is emitted unless this holds.</summary>
    public bool IsUsable => Enabled && Phases > 1 && CombatsPerPhase > 0;
}
