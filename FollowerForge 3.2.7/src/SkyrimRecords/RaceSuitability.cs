using System.Text.Json;
using FollowerForge.Domain;

namespace FollowerForge.SkyrimRecords;

public enum RaceClass
{
    /// <summary>One of the ten vanilla player races — always safe, adds no requirement.</summary>
    Vanilla,
    /// <summary>A playable race from a mod: works, but everyone who installs her needs that mod.</summary>
    CustomPlayable,
    /// <summary>A mod race that is not flagged playable — usable but less tested.</summary>
    CustomOther,
    /// <summary>
    /// A creature: no head data, so no face can ever be built for her. Dragons, wolves, and the
    /// creature followers people actually ship (HSF Baby Dragon uses BabyDragonRaceFire, which
    /// has no head data at all). Hidden unless deliberately asked for, never silently offered.
    /// </summary>
    Creature,
    /// <summary>Child races and other records that must never become a follower.</summary>
    Unsuitable,
}

public sealed record RaceOption(
    string FormKey,
    string Name,
    RaceClass Class,
    string Note,
    string? SourcePlugin);

/// <summary>
/// Decides which races the wizard should offer. Skyrim has hundreds of RACE records — most are
/// creatures, vampire/werewolf variants or child races that would produce a broken follower.
/// </summary>
public static class RaceSuitability
{
    /// <summary>The ten playable races. Anything else came from a mod or a creature.</summary>
    private static readonly Dictionary<string, string> VanillaPlayable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NordRace"] = "Nord",
        ["ImperialRace"] = "Imperial",
        ["BretonRace"] = "Breton",
        ["RedguardRace"] = "Redguard",
        ["DarkElfRace"] = "Dark Elf",
        ["HighElfRace"] = "High Elf",
        ["WoodElfRace"] = "Wood Elf",
        ["OrcRace"] = "Orc",
        ["KhajiitRace"] = "Khajiit",
        ["ArgonianRace"] = "Argonian",
    };

    /// <summary>EditorID fragments that mean "not a person you can recruit".</summary>
    private static readonly string[] Disqualifying =
    [
        // "beastrace" covers the transformation forms (DLC1VampireBeastRace, WerewolfBeastRace)
        // while leaving ordinary vampire variants like NordRaceVampire available.
        "child", "beastrace", "werewolf", "werebear", "vampirelord", "vampire lord", "dragon", "draugr",
        "falmer", "chaurus", "spider", "wolf", "bear", "troll", "giant", "mammoth", "horse",
        "dog", "fox", "goat", "cow", "chicken", "skeever", "slaughterfish", "mudcrab", "hagraven",
        "spriggan", "wisp", "atronach", "dremora", "dwarven", "centurion", "sphere", "spider",
        "elder", "manakin", "mannequin", "test", "creature", "deer", "elk", "hare", "rabbit",
        "seeker", "lurker", "ash", "netch", "riekling", "boar", "bristleback", "horker",
    ];

    public static RaceOption Classify(IndexedRecord race)
    {
        var edid = race.EditorId ?? "";
        var display = !string.IsNullOrWhiteSpace(race.DisplayName) ? race.DisplayName! : edid;
        var plugin = race.WinningPlugin;

        if (VanillaPlayable.TryGetValue(edid, out var friendly))
            return new RaceOption(race.FormKey, friendly, RaceClass.Vanilla,
                "vanilla — works for everyone, no extra mods", plugin);

        // Children are excluded outright and are never offered by any option.
        if (edid.Contains("child", StringComparison.OrdinalIgnoreCase))
            return new RaceOption(race.FormKey, display, RaceClass.Unsuitable, "not a follower race", plugin);

        var (playable, hasHead) = ReadDetail(race);

        // Creatures: real, shippable followers use these (HSF Baby Dragon), but they can never
        // have a built face, so they are a deliberate choice rather than a hidden landmine.
        if (!hasHead || Disqualifying.Any(bad => edid.Contains(bad, StringComparison.OrdinalIgnoreCase)))
            return new RaceOption(race.FormKey, display, RaceClass.Creature,
                hasHead ? "creature race — no face, gear may not fit"
                        : "creature — no face at all, and no equipment", plugin);

        var sourceMod = race.SourceMod ?? plugin;
        return playable
            ? new RaceOption(race.FormKey, display, RaceClass.CustomPlayable,
                $"custom race — everyone needs {sourceMod}", plugin)
            : new RaceOption(race.FormKey, display, RaceClass.CustomOther,
                $"custom, non-playable — needs {sourceMod}", plugin);
    }

    /// <summary>Reads the playable flag and head-part presence from the stored race analysis.</summary>
    private static (bool Playable, bool HasHead) ReadDetail(IndexedRecord race)
    {
        if (race.DetailJson is null) return (false, true);
        try
        {
            using var doc = JsonDocument.Parse(race.DetailJson);
            var root = doc.RootElement;
            var playable = root.TryGetProperty("PlayableFlag", out var p) && p.GetBoolean();
            var male = root.TryGetProperty("HasMaleHeadData", out var m) && m.GetBoolean();
            var female = root.TryGetProperty("HasFemaleHeadData", out var f) && f.GetBoolean();
            return (playable, male || female);
        }
        catch (JsonException)
        {
            return (false, true);
        }
    }

    /// <summary>
    /// Vanilla first, then custom playable, then the rest. Creature races appear only when
    /// asked for; child races never do.
    /// </summary>
    public static IReadOnlyList<RaceOption> Offer(IEnumerable<IndexedRecord> races,
        bool includeCreatures = false) =>
        races.Select(Classify)
            .Where(r => r.Class != RaceClass.Unsuitable
                     && (includeCreatures || r.Class != RaceClass.Creature))
            .GroupBy(r => r.FormKey).Select(g => g.First())
            .OrderBy(r => r.Class)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
