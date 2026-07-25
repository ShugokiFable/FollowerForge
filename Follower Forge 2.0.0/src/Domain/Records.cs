namespace FollowerForge.Domain;

/// <summary>Record signatures Follower Forge indexes from the winning load order.</summary>
public enum IndexedRecordType
{
    Npc, Race, CombatStyle, Class, VoiceType, HeadPart, Outfit, Armor, ArmorAddon,
    Weapon, Spell, Perk, Package, Faction, Relationship, Cell, Location, Keyword,
    TextureSet, FormList
}

/// <summary>
/// One winning record from the active modpack. FormKey format follows Mutagen:
/// "123ABC:Plugin.esp" (6-hex local id + source plugin file name).
/// </summary>
public sealed record IndexedRecord
{
    public required string FormKey { get; init; }
    public required IndexedRecordType Type { get; init; }
    public string? EditorId { get; init; }
    public string? DisplayName { get; init; }
    /// <summary>Plugin that defines the record (FormKey master).</summary>
    public required string SourcePlugin { get; init; }
    /// <summary>Plugin whose version of the record wins in the load order.</summary>
    public required string WinningPlugin { get; init; }
    /// <summary>Masters of the winning plugin — what a referencing mod must inherit.</summary>
    public IReadOnlyList<string> RequiredMasters { get; init; } = [];
    public uint MajorFlags { get; init; }
    /// <summary>Vortex staging mod folder that supplied the winning plugin, when known.</summary>
    public string? SourceMod { get; init; }
    /// <summary>Type-specific data as JSON (raw CSTY values, race capabilities, …).</summary>
    public string? DetailJson { get; init; }
}

/// <summary>How usable a voice type is for a vanilla-recruitable follower.</summary>
public enum VoiceFollowerCapability
{
    Unknown = 0,
    /// <summary>Covered by vanilla follower dialogue (DialogueFollower voice list).</summary>
    FullyCapable = 1,
    /// <summary>Provided with follower dialogue by an installed voice resource (e.g. SOS Voice Pack).</summary>
    ResourceIntegrated = 2,
    /// <summary>No known follower dialogue coverage.</summary>
    NonFollowerCapable = 3,
}
