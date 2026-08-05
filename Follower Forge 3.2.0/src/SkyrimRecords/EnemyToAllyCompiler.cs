using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Builds the Enemy-to-Ally chain: a hostile copy of the follower to find and beat, the spell
/// tome she carries, and the spell that summons her ally form afterwards.
///
/// Structure read out of the E2A mods (AsianFollowers.esp) rather than invented:
///   DaeEnemy   not essential, hostile/creature factions, tome IN INVENTORY
///   Book       "Spell Tome: Teleport Dae" teaches -> AAA_TeleportSpellBGDae
///   Spell      FireAndForget / Self -> magic effect with a Script archetype
///   ally ACHR  initiallyDisabled = true, persistent = true
/// The tome sits in her inventory, so beating her is what hands it over — there is no death
/// script anywhere in the chain, and there is none here either.
/// </summary>
public sealed class EnemyToAllyCompiler(ILogger log)
{
    /// <summary>Must match the compiled FF_Summon.pex shipped alongside the app.</summary>
    public const string ScriptName = "FF_Summon";

    public sealed record EnemyToAllyResult(
        FormKey EnemyNpc,
        FormKey Tome,
        FormKey Spell,
        FormKey MagicEffect);

    /// <param name="follower">The ordinary follower NPC — her hostile form is copied from it.</param>
    /// <param name="allyRef">Her placed reference, which this makes start disabled.</param>
    public EnemyToAllyResult? Compile(SkyrimMod mod, Npc follower, PlacedNpc allyRef, string prefix,
        string displayName, EnemyToAllySpec spec)
    {
        if (!spec.IsUsable) return null;

        // She does not exist in the world until summoned. Persistence is kept: the spell holds a
        // reference to her, which must resolve before her cell has ever loaded.
        allyRef.SkyrimMajorRecordFlags |= SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled;

        // --- her hostile form: the same character, minus the things that make her a follower ---
        var enemy = follower.Duplicate(mod.GetNextFormKey());
        enemy.EditorID = $"{prefix}Enemy";
        mod.Npcs.Add(enemy);
        enemy.FormVersion = 44;
        enemy.Configuration.Flags &= ~NpcConfiguration.Flag.Essential;
        enemy.Configuration.Flags &= ~NpcConfiguration.Flag.Protected;
        // A follower who is also an enemy would be recruitable before she is beaten.
        enemy.Factions.Clear();
        foreach (var faction in HostileFactions(spec.Company))
            enemy.Factions.Add(new RankPlacement { Faction = faction.ToLink<IFactionGetter>(), Rank = 0 });

        var magicEffect = new MagicEffect(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}SummonEffect",
            Name = $"Summon {displayName}",
            Archetype = new MagicEffectArchetype { Type = MagicEffectArchetype.TypeEnum.Script },
            Flags = MagicEffect.Flag.NoDeathDispel,
            CastType = CastType.FireAndForget,
            TargetType = TargetType.Self,
            VirtualMachineAdapter = new VirtualMachineAdapter(),
        };
        var effectScript = new ScriptEntry { Name = ScriptName, Flags = ScriptEntry.Flag.Local };
        effectScript.Properties.Add(Obj("AllyToSummon", allyRef.FormKey));
        effectScript.Properties.Add(Obj("PlayerRef", VanillaForms.PlayerRef));
        magicEffect.VirtualMachineAdapter!.Scripts.Add(effectScript);
        mod.MagicEffects.Add(magicEffect);

        var spell = new Spell(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}SummonSpell",
            Name = $"Summon {displayName}",
            Type = SpellType.Spell,
            CastType = CastType.FireAndForget,
            TargetType = TargetType.Self,
            EquipmentType = new FormLinkNullable<IEquipTypeGetter>(VanillaForms.EitherHandEquipType),
        };
        spell.Effects.Add(new Effect
        {
            BaseEffect = new FormLinkNullable<IMagicEffectGetter>(magicEffect.FormKey),
            Data = new EffectData { Magnitude = 0, Area = 0, Duration = 0 },
        });
        mod.Spells.Add(spell);

        var tome = new Book(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}SummonTome",
            Name = spec.TomeName is { Length: > 0 } t ? t : $"Spell Tome: Summon {displayName}",
            // Matches a vanilla spell tome (SpellTomeFlames: value 50, weight 1).
            Value = 50,
            Weight = 1,
            Teaches = new BookSpell { Spell = new FormLinkNullable<ISpellGetter>(spell.FormKey) },
            BookText = $"Summons {displayName} to your side.",
        };
        mod.Books.Add(tome);

        // The tome rides in her inventory: beating her IS how the player gets it.
        enemy.Items ??= [];
        enemy.Items.Add(new ContainerEntry
        {
            Item = new ContainerItem { Item = new FormLink<IItemGetter>(tome.FormKey), Count = 1 },
        });

        log.Information("Enemy-to-Ally: {Enemy} carries {Tome}, summoning {Ally}",
            enemy.EditorID, tome.EditorID, allyRef.FormKey.ID.ToString("X6"));
        return new EnemyToAllyResult(enemy.FormKey, tome.FormKey, spell.FormKey, magicEffect.FormKey);
    }

    /// <summary>
    /// Who her hostile form runs with. E2A gives each follower a mix so the dungeon's own
    /// inhabitants ignore her; these are the vanilla factions those mods draw from.
    /// </summary>
    private static IReadOnlyList<FormKey> HostileFactions(HostileCompany company) => company switch
    {
        HostileCompany.Draugr => [VanillaForms.DraugrFaction, VanillaForms.BanditFaction],
        HostileCompany.Warlocks => [VanillaForms.WarlockFaction, VanillaForms.BanditFaction],
        HostileCompany.Creatures => [VanillaForms.CreatureFaction, VanillaForms.PredatorFaction],
        _ => [VanillaForms.BanditFaction],
    };

    private static ScriptObjectProperty Obj(string name, FormKey key) =>
        new() { Name = name, Object = new FormLink<ISkyrimMajorRecordGetter>(key) };
}
