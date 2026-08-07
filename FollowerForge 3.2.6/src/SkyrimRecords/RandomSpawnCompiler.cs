using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Makes a follower start somewhere different each playthrough, the way the Enemy-to-Ally mods do.
///
/// An invisible XMarker is placed at each alternate spot using the same cell machinery the
/// follower herself uses — so the room's own lighting and flags are preserved exactly as they are
/// for a normal placement — and a start-game-enabled quest moves her to one of them at random.
/// </summary>
public sealed class RandomSpawnCompiler(ILogger log)
{
    /// <summary>Must match the compiled FF_RandomSpawn.pex shipped alongside the app.</summary>
    public const string ScriptName = "FF_RandomSpawn";

    /// <summary>The script has four spot properties, so four is the ceiling.</summary>
    public const int MaxSpots = 4;

    public sealed record RandomSpawnResult(FormKey Quest, IReadOnlyList<FormKey> Markers);

    /// <param name="followerRef">The follower's own placed reference — she is moved, not respawned.</param>
    /// <param name="spots">
    /// Alternate locations, already resolved from the library, paired with their cell record so
    /// the marker's cell override carries the room's real lighting.
    /// </param>
    /// <param name="enemyBase">
    /// Enemy-to-Ally: the hostile form to place at the chosen spot. When given, the follower is
    /// NOT moved — she stays disabled until summoned.
    /// </param>
    public RandomSpawnResult? Compile(SkyrimMod mod, FormKey followerRef, string prefix,
        IReadOnlyList<(SpawnLocation Location, ICellGetter? CellSource)> spots,
        FormKey? enemyBase = null)
    {
        var usable = spots.Where(s => s.Location.Placeable).Take(MaxSpots).ToList();
        if (usable.Count == 0) return null;

        var markers = new List<FormKey>();
        var index = 0;
        foreach (var (location, cellSource) in usable)
        {
            var marker = new PlacedObject(mod.GetNextFormKey(), VanillaForms.Release)
            {
                EditorID = $"{prefix}SpawnMarker{++index:D2}",
                Base = new FormLinkNullable<IPlaceableObjectGetter>(VanillaForms.XMarker),
                Placement = new Placement
                {
                    Position = new P3Float(location.X, location.Y, location.Z),
                    Rotation = new P3Float(0, 0, location.RotationZ),
                },
            };
            // Persistent, like the follower's own reference: a script property must resolve
            // before the marker's cell has ever been loaded.
            marker.SkyrimMajorRecordFlags |= (SkyrimMajorRecord.SkyrimMajorRecordFlag)0x00000400;
            CellPlacer.Place(mod, location, marker, cellSource);
            markers.Add(marker.FormKey);
        }

        var quest = new Quest(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{prefix}SpawnQuest",
            FormVersion = 44,
            // Start game enabled, and the bit the Creation Kit always sets. The E2A quests use
            // 0x111 (this plus Run Once); ours is safe to re-run, so Run Once is not needed.
            Flags = (Quest.Flag)0x0011,
            VirtualMachineAdapter = new QuestAdapter(),
        };

        var entry = new ScriptEntry { Name = ScriptName, Flags = ScriptEntry.Flag.Local };
        if (enemyBase is { } enemy) entry.Properties.Add(Obj("SpawnBase", enemy));
        else entry.Properties.Add(Obj("Follower", followerRef));
        for (var i = 0; i < markers.Count; i++)
            entry.Properties.Add(Obj($"Spot{i + 1}", markers[i]));
        quest.VirtualMachineAdapter!.Scripts.Add(entry);
        mod.Quests.Add(quest);

        log.Information("Random spawn: {Count} spot(s) — {Places}",
            markers.Count, string.Join(", ", usable.Select(s => s.Location.Display)));
        return new RandomSpawnResult(quest.FormKey, markers);
    }

    private static ScriptObjectProperty Obj(string name, FormKey key) =>
        new() { Name = name, Object = new FormLink<ISkyrimMajorRecordGetter>(key) };
}
