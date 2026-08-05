using FollowerForge.Domain;
using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.SkyrimRecords;

/// <summary>Single authoritative mapping between profile skills and Skyrim's DNAM keys.</summary>
public static class FollowerSkillMap
{
    public static readonly IReadOnlyList<(FollowerSkill Profile, Skill Skyrim)> All =
    [
        (FollowerSkill.OneHanded, Skill.OneHanded),
        (FollowerSkill.TwoHanded, Skill.TwoHanded),
        (FollowerSkill.Archery, Skill.Archery),
        (FollowerSkill.Block, Skill.Block),
        (FollowerSkill.Smithing, Skill.Smithing),
        (FollowerSkill.HeavyArmor, Skill.HeavyArmor),
        (FollowerSkill.LightArmor, Skill.LightArmor),
        (FollowerSkill.Pickpocket, Skill.Pickpocket),
        (FollowerSkill.Lockpicking, Skill.Lockpicking),
        (FollowerSkill.Sneak, Skill.Sneak),
        (FollowerSkill.Alchemy, Skill.Alchemy),
        (FollowerSkill.Speech, Skill.Speech),
        (FollowerSkill.Alteration, Skill.Alteration),
        (FollowerSkill.Conjuration, Skill.Conjuration),
        (FollowerSkill.Destruction, Skill.Destruction),
        (FollowerSkill.Illusion, Skill.Illusion),
        (FollowerSkill.Restoration, Skill.Restoration),
        (FollowerSkill.Enchanting, Skill.Enchanting),
    ];
}
