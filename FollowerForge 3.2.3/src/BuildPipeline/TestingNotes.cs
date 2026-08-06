using System.Text;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Writes test-in-game.txt beside the follower: the console commands needed to reach her and to
/// exercise whichever optional features she was built with.
///
/// The commands use `help` rather than literal FormIDs on purpose. A light plugin's records live
/// at runtime IDs of the form FE-xxx-yyy where xxx is its slot in the load order, so the number
/// is only knowable once the game has loaded — printing one here would be wrong for everybody.
/// </summary>
public static class TestingNotes
{
    public static string Write(string packageRoot, FollowerProfile profile,
        FollowerCompiler.CompileResult compiled, SpawnLocation? location)
    {
        var text = new StringBuilder();
        var step = 0;
        string Step(string title) => $"{++step}. {title}";
        text.AppendLine($"Testing {profile.Name}  ({profile.PluginName})");
        text.AppendLine(new string('=', 60));
        text.AppendLine();
        text.AppendLine("Open the console with ~ and use these. Test on a save you can throw away.");
        text.AppendLine();

        text.AppendLine(Step("Find her records"));
        text.AppendLine($"   help \"{profile.Name}\" 0");
        text.AppendLine("   Light plugins load at FE-xxx-yyy, so the IDs differ per load order —");
        text.AppendLine("   `help` prints the ones your game actually assigned.");
        text.AppendLine();

        if (compiled.EnemyToAlly is not null)
        {
            // She does not exist until beaten, so the ordinary "go to her" steps do not apply.
            text.AppendLine(Step("Enemy to ally — she is NOT in the world yet"));
            text.AppendLine($"   Her hostile form is placed by {compiled.NpcEditorId}'s spawn quest at one of");
            text.AppendLine("   the places you chose. To skip the hunt:");
            text.AppendLine($"     help \"{profile.Name}\" 0        (find <enemyID> and <tomeID>)");
            text.AppendLine("     player.placeatme <enemyID> 1     spawn her hostile form here");
            text.AppendLine("     player.additem <tomeID> 1        or just take the tome directly");
            text.AppendLine("   Read the tome, then cast the spell. She should appear in front of you.");
            text.AppendLine("   If she does not: the ally reference is disabled by design, so check");
            text.AppendLine("     prid <allyRefID>");
            text.AppendLine("     getdisabled                      1 means the summon never ran");
            text.AppendLine();
        }
        else
        {
            text.AppendLine(Step("Go to her"));
            if (location?.CellEditorId is { Length: > 0 } cell)
                text.AppendLine($"   coc {cell}                     ({location.Display})");
            else
                text.AppendLine("   coc Riverwood                  then walk to where you placed her");
            text.AppendLine("   Or bring her to you:");
            text.AppendLine("     prid <herRefID>");
            text.AppendLine("     moveto player");
            text.AppendLine();
        }

        if (compiled.RandomSpawn is not null && compiled.EnemyToAlly is null)
        {
            text.AppendLine(Step("Random start"));
            text.AppendLine($"   She starts at one of {compiled.RandomSpawn.Markers.Count} places, chosen on a NEW game or");
            text.AppendLine("   the first load after installing. Reloading an old save will not re-roll it.");
            text.AppendLine("   If she is always in the same place, the quest never ran.");
            text.AppendLine();
        }

        if (compiled.Evolution is not null)
        {
            text.AppendLine(Step("Evolution (experimental)"));
            text.AppendLine("   Watch the counters move after a fight — this is the quickest way to tell");
            text.AppendLine("   whether the script is running at all:");
            text.AppendLine($"     help \"{Prefix(compiled.NpcEditorId)}Progress\" 0");
            text.AppendLine("     getglobalvalue <progressID>     rises by 1 per fight survived");
            text.AppendLine($"     help \"{Prefix(compiled.NpcEditorId)}Phase\" 0");
            text.AppendLine($"     getglobalvalue <phaseID>       1 .. {profile.Evolution.Phases}");
            text.AppendLine("   To jump ahead:  setglobalvalue <phaseID> 2");
            text.AppendLine("   If Progress never moves, OnCombatStateChanged is not firing.");
            text.AppendLine();
        }

        if (profile.Transformation.IsUsable)
        {
            text.AppendLine(Step("Transformation (experimental)"));
            text.AppendLine("   Start a fight near her and watch. She changes a couple of seconds in,");
            text.AppendLine("   and changes back when combat ends.");
            text.AppendLine("   The revert is the part to watch: it relies on SetRace() with no argument.");
            text.AppendLine("     prid <herRefID>");
            text.AppendLine("     getrace                        should be her own race again after");
            text.AppendLine();
        }

        if (profile.Dialogue.HasContent)
        {
            text.AppendLine(Step("Custom lines"));
            text.AppendLine("   Talk to her. If subtitles show but there is no sound, the .fuz files are");
            text.AppendLine("   not being found — check they deployed under");
            text.AppendLine($"     Data\\sound\\voice\\{profile.PluginName}\\<voice type>\\");
            text.AppendLine("   If nothing appears at all, the dialogue quest did not start:");
            text.AppendLine($"     help \"{Prefix(compiled.NpcEditorId)}Dialogue\" 0");
            text.AppendLine("     getquestrunning <questID>      0 means the SEQ file did not take");
            text.AppendLine();
        }

        text.AppendLine("Useful either way");
        text.AppendLine("   tdetect                          NPCs stop noticing you");
        text.AppendLine("   prid <herRefID> / resurrect      if a test goes badly");
        text.AppendLine("   sqv <questID>                    dump a quest's state and aliases");

        var path = Path.Combine(packageRoot, "test-in-game.txt");
        File.WriteAllText(path, text.ToString());
        return path;
    }

    /// <summary>"FF_Nadia_NPC" -> "FF_Nadia_", which is what the other EditorIDs share.</summary>
    private static string Prefix(string npcEditorId) =>
        npcEditorId.EndsWith("NPC", StringComparison.Ordinal)
            ? npcEditorId[..^"NPC".Length]
            : npcEditorId;
}
