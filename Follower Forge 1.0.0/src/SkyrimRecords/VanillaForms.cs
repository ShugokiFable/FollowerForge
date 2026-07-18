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
    public static readonly FormKey PlayerFaction = K(0x000000DB);

    /// <summary>The Player NPC (RELA parent side); relationships link this to the follower.</summary>
    public static readonly FormKey PlayerNpc = K(0x00000007);

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
