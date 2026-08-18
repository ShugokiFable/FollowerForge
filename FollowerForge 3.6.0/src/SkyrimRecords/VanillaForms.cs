using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Verified vanilla FormKeys used when building followers. Every value here is cross-checked
/// against Skyrim.esm and the racemenu-followers reference table — never invented.
/// </summary>
public static class VanillaForms
{
    public static readonly ModKey Skyrim = ModKey.FromNameAndExtension("Skyrim.esm");
    public static readonly ModKey Update = ModKey.FromNameAndExtension("Update.esm");

    private static FormKey K(uint id) => new(Skyrim, id);

    // Factions (racemenu-followers/references/follower-vanilla-ids.md, verified in Skyrim.esm).
    public static readonly FormKey PotentialFollowerFaction = K(0x0005C84D); // rank 0
    public static readonly FormKey CurrentFollowerFaction = K(0x0005C84E);   // rank -1 until hired
    public static readonly FormKey DismissedFollowerFaction = K(0x0005C84C);
    /// <summary>
    /// 0x000DB1, NOT 0x0000DB. The transposed value shipped through 3.2.5 and made every built
    /// follower carry a dangling faction link: verified against Lydia (HousecarlWhiterun
    /// 0A2C8E:Skyrim.esm), whose Factions[0] is 000DB1:Skyrim.esm.
    /// </summary>
    public static readonly FormKey PlayerFaction = K(0x00000DB1);
    /// <summary>Gates the "are you interested in me?" dialogue (with an Amulet of Mara).</summary>
    public static readonly FormKey PotentialMarriageFaction = K(0x00019809);

    /// <summary>The Player NPC (RELA parent side); relationships link this to the follower.</summary>
    public static readonly FormKey PlayerNpc = K(0x00000007);

    // The only two keywords a vanilla vampire NPC carries beyond her race — verified on
    // SybilleStentor, who has no vampire spells or faction on her record at all.
    public static readonly FormKey VampireKeyword = K(0x000A82BB);
    public static readonly FormKey ActorTypeUndeadKeyword = K(0x00013796);

    // Werewolf. Skyrim has ONE werewolf race rather than a variant per race, which is why
    // werewolves are a transformation and vampires are not.
    public static readonly FormKey WerewolfBeastRace = K(0x000CDD84);
    /// <summary>
    /// WerewolfChangeFX. Kept for identification only — do not cast this on a follower.
    /// Its magic effect runs WerewolfTransformVisual, which Wait(10) then SetRace(Werewolf)
    /// and will undo a scripted revert after combat.
    /// </summary>
    public static readonly FormKey WerewolfChangeFx = K(0x000F8208);

    /// <summary>The invisible marker used for alternate spawn spots.</summary>
    public static readonly FormKey XMarker = K(0x0000003B);

    // Enemy-to-Ally. PlayerRef carries no EditorID; 0x14 is what the E2A mods' own summon
    // effect binds, and EitherHand is the equip type vanilla spells like Flames use.
    public static readonly FormKey PlayerRef = K(0x00000014);
    public static readonly FormKey EitherHandEquipType = K(0x00013F44);
    public static readonly FormKey BanditFaction = K(0x0001BCC0);
    public static readonly FormKey DraugrFaction = K(0x0002430D);
    public static readonly FormKey WarlockFaction = K(0x00026724);
    public static readonly FormKey CreatureFaction = K(0x00000013);
    public static readonly FormKey PredatorFaction = K(0x0002E893);

    // AI packages. Vanilla records, referenced not copied — usage counts measured in Skyrim.esm,
    // which is why these and not others: a package 307 NPCs already rely on is a proven default.
    /// <summary>Wanders the room she was placed in. Used by 307 vanilla NPCs, and by Willow.</summary>
    public static readonly FormKey SandboxEditorLocation512 = K(0x0001B217);
    /// <summary>Keeps close to her spot. Used by Laci Living Doll.</summary>
    public static readonly FormKey SandboxEditorLocation256 = K(0x0009361E);
    /// <summary>Settles wherever she currently is rather than where she started.</summary>
    public static readonly FormKey SandboxCurrentLocation256 = K(0x000956B8);
    /// <summary>Sleeps 8 hours from midnight at her own spot. Used by 254 vanilla NPCs.</summary>
    public static readonly FormKey SleepEditorLoc24x8 = K(0x0001B210);

    // Placement: WhiterunWorld exterior persistent cell (probed: TopCell = 01A270, 485 persistent refs).
    public static readonly FormKey WhiterunWorld = K(0x0001A26F);
    public static readonly FormKey WhiterunWorldPersistentCell = K(0x0001A270);

    // Sensible vanilla defaults for a plain follower.
    public static readonly FormKey NordRace = K(0x00013746);
    public static readonly FormKey FemaleEvenTonedVoice = K(0x00013ADD);
    public static readonly FormKey MaleEvenTonedVoice = K(0x00013AD2);
    public static readonly FormKey CombatWarrior1HClass = K(0x00013176);
    public static readonly FormKey FarmClothesOutfit = K(0x0001DC10);

    /// <summary>Verified Whiterun exterior open-ground spot (near the Monique reference coords).</summary>
    public static readonly (float X, float Y, float Z) WhiterunDefaultPos = (28878f, -4122f, -2618f);

    public static SkyrimRelease Release => SkyrimRelease.SkyrimSE;
}
