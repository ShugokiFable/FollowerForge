namespace FollowerForge.Domain;

/// <summary>Which crowd her hostile form runs with, so she is not alone in the dungeon.</summary>
public enum HostileCompany
{
    /// <summary>Bandits — the safest default; her spot will usually be a bandit camp or fort.</summary>
    Bandits = 0,
    Draugr = 1,
    Warlocks = 2,
    /// <summary>Beasts and creatures, for a spot in a cave or an animal den.</summary>
    Creatures = 3,
}

/// <summary>
/// The Enemy-to-Ally loop: you meet her as an enemy, beat her, and she becomes recruitable.
///
/// Read out of the E2A mods rather than invented. The chain is almost entirely records, with a
/// single script for the summon:
///   1. A hostile copy of her waits at one of a few random spots, in creature/bandit factions so
///      the dungeon's own inhabitants leave her alone.
///   2. She carries a spell tome. Beat her and you loot it — no death script involved.
///   3. The tome teaches a self-cast spell whose effect enables her ALLY reference (placed but
///      disabled from the start) and brings it to the player.
///   4. That ally is the ordinary follower: essential, follower factions, full dialogue.
/// </summary>
public sealed record EnemyToAllySpec
{
    /// <summary>Off unless asked for. Normal followers are simply placed and recruited.</summary>
    public bool Enabled { get; init; }

    public HostileCompany Company { get; init; } = HostileCompany.Bandits;

    /// <summary>
    /// Where her hostile form can appear. Reuses the ordinary spawn-location library; up to four
    /// are used, and one of them is chosen at random each playthrough.
    /// </summary>
    public IReadOnlyList<string> LocationIds { get; init; } = [];

    /// <summary>Shown on the tome the player loots — "Spell Tome: Summon &lt;name&gt;" by default.</summary>
    public string? TomeName { get; init; }

    /// <summary>Nothing is emitted without somewhere for her hostile form to be found.</summary>
    public bool IsUsable => Enabled && LocationIds.Count > 0;
}
