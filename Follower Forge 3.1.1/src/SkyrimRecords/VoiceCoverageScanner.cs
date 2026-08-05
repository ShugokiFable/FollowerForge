using System.Text.Json;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>Extra dialogue one installed mod contributes to a particular voice type.</summary>
public sealed record VoiceContribution(string Plugin, string ModName, int Lines);

/// <summary>Everything a follower with this voice type inherits for free.</summary>
public sealed record VoiceCoverage(
    string VoiceFormKey,
    string VoiceEditorId,
    int TotalLines,
    IReadOnlyList<VoiceContribution> Contributions);

/// <summary>
/// Which plugins let a voice type get married. <paramref name="Vanilla"/> means it is in the
/// base game's VoicesMarriageNeutral list and so needs nothing installed; every other voice
/// marries only because a mod supplies the dialogue, which becomes a requirement.
/// </summary>
public sealed record MarriageSupport(string VoiceFormKey, bool Vanilla, IReadOnlyList<string> Plugins);

public sealed record VoiceCoverageLibrary(
    DateTimeOffset BuiltUtc,
    int PluginsScanned,
    IReadOnlyList<VoiceCoverage> Voices,
    IReadOnlyList<MarriageSupport> Marriage);

/// <summary>
/// Answers "if I give her this voice, what does she already say?"
///
/// Mods like Relationship Dialogue Overhaul do not need any support in the generated plugin —
/// they gate their dialogue on GetIsVoiceType, so a follower inherits every line the moment she
/// uses a covered voice. What the user actually needs is to be TOLD which voices inherit what,
/// and that generalises: any installed mod that keys dialogue to a voice type is found the same
/// way, without knowing anything about it in advance.
///
/// A FormList in the condition is followed, because that is how mods cover many voices at once.
/// </summary>
public sealed class VoiceCoverageScanner(ILogger log)
{
    /// <summary>Scans the given plugins and returns coverage per voice type.</summary>
    public VoiceCoverageLibrary Scan(string gameDataPath, IEnumerable<string> pluginFileNames,
        CancellationToken cancel = default)
    {
        // voice FormKey -> plugin -> line count
        var byVoice = new Dictionary<FormKey, Dictionary<string, int>>();
        // voice FormKey -> plugins whose marriage dialogue covers it
        var marriage = new Dictionary<FormKey, SortedSet<string>>();
        // Voices in the base game's own marriage list: these need no mod at all.
        var vanillaMarriage = new HashSet<FormKey>();
        var scanned = 0;

        foreach (var fileName in pluginFileNames)
        {
            if (cancel.IsCancellationRequested) break;
            var path = Path.Combine(gameDataPath, fileName);
            if (!File.Exists(path)) continue;

            try
            {
                using var mod = SkyrimMod.CreateFromBinaryOverlay(new ModPath(path), VanillaForms.Release);
                scanned++;

                // FormLists local to this plugin, so a condition naming one can be expanded.
                var lists = mod.FormLists.ToDictionary(f => f.FormKey, f => f.Items.Select(i => i.FormKey).ToList());

                // Marriage first: a plugin can add marriage dialogue without having any
                // voice-gated quests, and the early exit below would skip it.
                CollectMarriage(mod, fileName, lists, marriage, vanillaMarriage);

                // Which quests are gated to which voices, and how many lines each holds.
                var questVoices = new Dictionary<FormKey, HashSet<FormKey>>();
                foreach (var quest in mod.Quests)
                {
                    var voices = VoicesGating(quest, lists);
                    if (voices.Count > 0) questVoices[quest.FormKey] = voices;
                }
                if (questVoices.Count == 0) continue;

                var lineCounts = new Dictionary<FormKey, int>();
                foreach (var topic in mod.DialogTopics)
                {
                    var q = topic.Quest.FormKey;
                    if (!questVoices.ContainsKey(q)) continue;
                    lineCounts[q] = lineCounts.GetValueOrDefault(q) + topic.Responses.Count;
                }

                foreach (var (quest, voices) in questVoices)
                {
                    if (!lineCounts.TryGetValue(quest, out var lines) || lines == 0) continue;
                    foreach (var voice in voices)
                    {
                        var perPlugin = byVoice.TryGetValue(voice, out var m)
                            ? m
                            : byVoice[voice] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        perPlugin[fileName] = perPlugin.GetValueOrDefault(fileName) + lines;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug("Skipping {Plugin} during voice scan: {Message}", fileName, ex.Message);
            }
        }

        var voicesOut = byVoice
            .Select(kv => new VoiceCoverage(
                kv.Key.ToString(),
                "",   // filled in by the caller, which has the record index
                kv.Value.Values.Sum(),
                kv.Value.OrderByDescending(p => p.Value)
                    .Select(p => new VoiceContribution(p.Key, p.Key, p.Value))
                    .ToList()))
            .OrderByDescending(v => v.TotalLines)
            .ToList();

        var marriageOut = marriage
            .Select(kv => new MarriageSupport(
                kv.Key.ToString(), vanillaMarriage.Contains(kv.Key), kv.Value.ToList()))
            .ToList();

        log.Information("Voice coverage: {Voices} voice types across {Plugins} plugins; "
            + "{Marriage} can marry ({Vanilla} without any mod)",
            voicesOut.Count, scanned, marriageOut.Count, marriageOut.Count(x => x.Vanilla));
        return new VoiceCoverageLibrary(DateTimeOffset.UtcNow, scanned, voicesOut, marriageOut);
    }

    /// <summary>
    /// The voice types a quest's dialogue is restricted to, following FormLists. An empty result
    /// means the quest is not voice-gated, so it says nothing about any particular follower.
    /// </summary>
    private static HashSet<FormKey> VoicesGating(IQuestGetter quest,
        Dictionary<FormKey, List<FormKey>> formLists)
    {
        var found = new HashSet<FormKey>();
        foreach (var condition in quest.DialogConditions)
        {
            if (condition.Data is not IGetIsVoiceTypeConditionDataGetter data) continue;
            // A negative test ("is NOT this voice") tells us nothing about who DOES get the line.
            if (condition is IConditionFloatGetter { ComparisonValue: 0 }) continue;

            var key = data.VoiceTypeOrList.Link.FormKey;
            if (key.IsNull) continue;
            if (formLists.TryGetValue(key, out var members)) found.UnionWith(members);
            else found.Add(key);
        }
        return found;
    }


    /// <summary>Vanilla's list of voice types that can be married (VoicesMarriageNeutral).</summary>
    private static readonly FormKey MarriageVoiceList =
        new(ModKey.FromFileName("Skyrim.esm"), 0x0CCD78);

    /// <summary>Membership of this faction is what marriage dialogue tests for.</summary>
    private static readonly FormKey PotentialMarriageFaction =
        new(ModKey.FromFileName("Skyrim.esm"), 0x019809);

    /// <summary>
    /// Records which voice types this plugin lets marry.
    ///
    /// Two routes, both real. Vanilla keeps a FormList of eight voices and gates the proposal on
    /// it. Relationship Dialogue Overhaul instead ships its own marriage dialogue, 51 INFOs
    /// covering about 28 voice types — FemaleElfHaughty, FemaleNord, FemaleSultry and the rest —
    /// so with RDO installed far more followers can marry than vanilla allows. Reading only the
    /// FormList would report those as unmarriageable, which is wrong on a modded setup.
    /// </summary>
    private static void CollectMarriage(ISkyrimModGetter mod, string fileName,
        Dictionary<FormKey, List<FormKey>> formLists, Dictionary<FormKey, SortedSet<string>> marriage,
        HashSet<FormKey> vanillaMarriage)
    {
        void Record(FormKey voice)
        {
            if (voice.IsNull) return;
            if (!marriage.TryGetValue(voice, out var plugins))
                marriage[voice] = plugins = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            plugins.Add(fileName);
        }

        // Route 1: the base game's list (also picks up any mod that overrides it). These need
        // nothing installed, which is the distinction that matters to whoever downloads her.
        foreach (var list in mod.FormLists)
            if (list.FormKey == MarriageVoiceList)
                foreach (var item in list.Items)
                {
                    vanillaMarriage.Add(item.FormKey);
                    Record(item.FormKey);
                }

        // Route 2: a plugin's own marriage dialogue, identified by conditions that reference the
        // marriage faction or the vanilla voice list.
        foreach (var topic in mod.DialogTopics)
        foreach (var info in topic.Responses)
        {
            var aboutMarriage = info.Conditions.Any(c =>
                ConditionTargets(c).Any(k => k == PotentialMarriageFaction || k == MarriageVoiceList));
            if (!aboutMarriage) continue;

            foreach (var condition in info.Conditions)
            {
                if (condition.Data is not IGetIsVoiceTypeConditionDataGetter data) continue;
                var key = data.VoiceTypeOrList.Link.FormKey;
                if (key == MarriageVoiceList) continue;   // already handled by route 1
                if (formLists.TryGetValue(key, out var members)) members.ForEach(Record);
                else Record(key);
            }
        }
    }

    /// <summary>Every record a condition points at, whatever the function.</summary>
    private static IEnumerable<FormKey> ConditionTargets(IConditionGetter condition)
    {
        var data = condition.Data;
        foreach (var property in data.GetType().GetProperties())
        {
            object? value;
            try { value = property.GetValue(data); } catch { continue; }
            var link = (value as IFormLinkGetter)?.FormKey
                       ?? (value?.GetType().GetProperty("Link")?.GetValue(value) as IFormLinkGetter)?.FormKey;
            if (link is not null) yield return link.Value;
        }
    }

    // ---------- cache ----------

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FollowerForge", "voice-coverage.json");

    public static void Save(VoiceCoverageLibrary library, string? path = null)
    {
        var target = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, JsonSerializer.Serialize(library, Json));
    }

    public static VoiceCoverageLibrary? Load(string? path = null)
    {
        var target = path ?? DefaultPath;
        if (!File.Exists(target)) return null;
        try
        {
            return JsonSerializer.Deserialize<VoiceCoverageLibrary>(File.ReadAllText(target), Json);
        }
        catch (JsonException)
        {
            return null;   // a corrupt cache is rebuilt, never crashes the wizard
        }
    }
}
