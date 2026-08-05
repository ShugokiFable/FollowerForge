namespace FollowerForge.Domain;

/// <summary>The eighteen Skyrim skills stored in an NPC's DNAM player-skills block.</summary>
public enum FollowerSkill
{
    OneHanded,
    TwoHanded,
    Archery,
    Block,
    Smithing,
    HeavyArmor,
    LightArmor,
    Pickpocket,
    Lockpicking,
    Sneak,
    Alchemy,
    Speech,
    Alteration,
    Conjuration,
    Destruction,
    Illusion,
    Restoration,
    Enchanting,
}

/// <summary>How Skyrim determines the follower's skills and primary attributes.</summary>
public enum FollowerStatsMode
{
    /// <summary>Let the selected class and NPC level calculate skills and attributes.</summary>
    AutoCalculate = 0,
    /// <summary>Use the exact DNAM skill and attribute values supplied by the profile.</summary>
    Custom = 1,
}

/// <summary>User-facing starting points for the optional custom-stat editor.</summary>
public enum FollowerStatPreset
{
    BlankSlate,
    OneHandedWarrior,
    TwoHandedWarrior,
    Archer,
    PureMage,
    Spellsword,
    RestorationSupport,
    Rogue,
}

/// <summary>
/// Complete NPC skill and primary-attribute configuration. Automatic calculation remains the
/// default so profiles created before the custom editor continue to behave exactly as before.
/// </summary>
public sealed record FollowerStats
{
    public FollowerStatsMode Mode { get; init; } = FollowerStatsMode.AutoCalculate;
    public Dictionary<FollowerSkill, byte> Skills { get; init; } = AllSkills(15);
    public ushort Health { get; init; } = 100;
    public ushort Magicka { get; init; } = 100;
    public ushort Stamina { get; init; } = 100;

    public byte GetSkill(FollowerSkill skill) =>
        Skills.TryGetValue(skill, out var value) ? value : (byte)15;

    public static FollowerStats FromPreset(FollowerStatPreset preset)
    {
        var skills = AllSkills(15);

        void Set(FollowerSkill skill, byte value) => skills[skill] = value;

        var stats = preset switch
        {
            FollowerStatPreset.OneHandedWarrior => (Health: (ushort)300, Magicka: (ushort)80, Stamina: (ushort)220),
            FollowerStatPreset.TwoHandedWarrior => (Health: (ushort)350, Magicka: (ushort)60, Stamina: (ushort)250),
            FollowerStatPreset.Archer => (Health: (ushort)220, Magicka: (ushort)80, Stamina: (ushort)300),
            FollowerStatPreset.PureMage => (Health: (ushort)160, Magicka: (ushort)420, Stamina: (ushort)120),
            FollowerStatPreset.Spellsword => (Health: (ushort)250, Magicka: (ushort)260, Stamina: (ushort)200),
            FollowerStatPreset.RestorationSupport => (Health: (ushort)220, Magicka: (ushort)420, Stamina: (ushort)160),
            FollowerStatPreset.Rogue => (Health: (ushort)200, Magicka: (ushort)100, Stamina: (ushort)320),
            _ => (Health: (ushort)100, Magicka: (ushort)100, Stamina: (ushort)100),
        };

        switch (preset)
        {
            case FollowerStatPreset.OneHandedWarrior:
                Set(FollowerSkill.OneHanded, 65);
                Set(FollowerSkill.Block, 55);
                Set(FollowerSkill.HeavyArmor, 60);
                Set(FollowerSkill.Archery, 30);
                Set(FollowerSkill.Smithing, 30);
                Set(FollowerSkill.Restoration, 25);
                break;
            case FollowerStatPreset.TwoHandedWarrior:
                Set(FollowerSkill.TwoHanded, 70);
                Set(FollowerSkill.Block, 40);
                Set(FollowerSkill.HeavyArmor, 60);
                Set(FollowerSkill.Archery, 25);
                Set(FollowerSkill.Smithing, 30);
                Set(FollowerSkill.Restoration, 20);
                break;
            case FollowerStatPreset.Archer:
                Set(FollowerSkill.Archery, 70);
                Set(FollowerSkill.LightArmor, 55);
                Set(FollowerSkill.Sneak, 50);
                Set(FollowerSkill.OneHanded, 35);
                Set(FollowerSkill.Alchemy, 30);
                break;
            case FollowerStatPreset.PureMage:
                Set(FollowerSkill.Alteration, 50);
                Set(FollowerSkill.Conjuration, 55);
                Set(FollowerSkill.Destruction, 70);
                Set(FollowerSkill.Illusion, 45);
                Set(FollowerSkill.Restoration, 55);
                Set(FollowerSkill.Enchanting, 40);
                break;
            case FollowerStatPreset.Spellsword:
                Set(FollowerSkill.OneHanded, 60);
                Set(FollowerSkill.LightArmor, 45);
                Set(FollowerSkill.Alteration, 40);
                Set(FollowerSkill.Destruction, 55);
                Set(FollowerSkill.Restoration, 50);
                Set(FollowerSkill.Enchanting, 30);
                break;
            case FollowerStatPreset.RestorationSupport:
                Set(FollowerSkill.Restoration, 75);
                Set(FollowerSkill.Alteration, 50);
                Set(FollowerSkill.Illusion, 45);
                Set(FollowerSkill.Conjuration, 30);
                Set(FollowerSkill.OneHanded, 25);
                Set(FollowerSkill.LightArmor, 30);
                break;
            case FollowerStatPreset.Rogue:
                Set(FollowerSkill.Sneak, 70);
                Set(FollowerSkill.OneHanded, 55);
                Set(FollowerSkill.Archery, 50);
                Set(FollowerSkill.LightArmor, 55);
                Set(FollowerSkill.Lockpicking, 65);
                Set(FollowerSkill.Pickpocket, 50);
                Set(FollowerSkill.Alchemy, 45);
                break;
        }

        return new FollowerStats
        {
            Mode = FollowerStatsMode.Custom,
            Skills = skills,
            Health = stats.Health,
            Magicka = stats.Magicka,
            Stamina = stats.Stamina,
        };
    }

    private static Dictionary<FollowerSkill, byte> AllSkills(byte value) =>
        Enum.GetValues<FollowerSkill>().ToDictionary(skill => skill, _ => value);
}
