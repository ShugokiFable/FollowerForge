using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Enumerates every winning record of the indexed types from an opened load order.
/// Winner = first occurrence of a FormKey when walking mods in priority order
/// (last loaded first), which is exactly the game's conflict rule.
/// </summary>
public sealed class RecordIndexer(ILogger log)
{
    /// <summary>Getter interface per indexed type; enumeration is deep (cells included).</summary>
    private static readonly (IndexedRecordType Type, Func<ISkyrimModGetter, IEnumerable<IMajorRecordGetter>> Enumerate)[]
        Extractors =
        [
            (IndexedRecordType.Npc, m => m.EnumerateMajorRecords<INpcGetter>()),
            (IndexedRecordType.Race, m => m.EnumerateMajorRecords<IRaceGetter>()),
            (IndexedRecordType.CombatStyle, m => m.EnumerateMajorRecords<ICombatStyleGetter>()),
            (IndexedRecordType.Class, m => m.EnumerateMajorRecords<IClassGetter>()),
            (IndexedRecordType.VoiceType, m => m.EnumerateMajorRecords<IVoiceTypeGetter>()),
            (IndexedRecordType.HeadPart, m => m.EnumerateMajorRecords<IHeadPartGetter>()),
            (IndexedRecordType.Outfit, m => m.EnumerateMajorRecords<IOutfitGetter>()),
            (IndexedRecordType.Armor, m => m.EnumerateMajorRecords<IArmorGetter>()),
            (IndexedRecordType.ArmorAddon, m => m.EnumerateMajorRecords<IArmorAddonGetter>()),
            (IndexedRecordType.Weapon, m => m.EnumerateMajorRecords<IWeaponGetter>()),
            (IndexedRecordType.Spell, m => m.EnumerateMajorRecords<ISpellGetter>()),
            (IndexedRecordType.Perk, m => m.EnumerateMajorRecords<IPerkGetter>()),
            (IndexedRecordType.Package, m => m.EnumerateMajorRecords<IPackageGetter>()),
            (IndexedRecordType.Faction, m => m.EnumerateMajorRecords<IFactionGetter>()),
            (IndexedRecordType.Relationship, m => m.EnumerateMajorRecords<IRelationshipGetter>()),
            (IndexedRecordType.Cell, m => m.EnumerateMajorRecords<ICellGetter>()),
            (IndexedRecordType.Location, m => m.EnumerateMajorRecords<ILocationGetter>()),
            (IndexedRecordType.Keyword, m => m.EnumerateMajorRecords<IKeywordGetter>()),
            (IndexedRecordType.TextureSet, m => m.EnumerateMajorRecords<ITextureSetGetter>()),
            (IndexedRecordType.FormList, m => m.EnumerateMajorRecords<IFormListGetter>()),
        ];

    /// <param name="loadOrder">Opened load order (see <see cref="LoadOrderBuilder"/>).</param>
    /// <param name="pluginSourceMods">Plugin file name → Vortex staging mod folder.</param>
    public IEnumerable<IndexedRecord> EnumerateWinningRecords(
        LoadOrderBuilder.BuiltLoadOrder loadOrder,
        IReadOnlyDictionary<string, string>? pluginSourceMods = null)
    {
        // Materialize priority-order mods once (mmap overlays stay lazy).
        var mods = new List<ISkyrimModGetter>();
        foreach (var listing in loadOrder.LoadOrder.PriorityOrder)
        {
            if (listing.Mod is not null) mods.Add(listing.Mod);
        }
        log.Information("Indexing winning records across {Count} plugins…", mods.Count);

        var masterCache = new Dictionary<ModKey, string[]>();

        foreach (var (type, enumerate) in Extractors)
        {
            var seen = new HashSet<FormKey>();
            long emitted = 0;
            foreach (var mod in mods)
            {
                IEnumerable<IMajorRecordGetter> records;
                try
                {
                    records = enumerate(mod);
                }
                catch (Exception ex)
                {
                    log.Warning("Skipping {Type} enumeration in {Mod}: {Error}", type, mod.ModKey, ex.Message);
                    continue;
                }

                foreach (var rec in records)
                {
                    if (!seen.Add(rec.FormKey)) continue;

                    if (!masterCache.TryGetValue(mod.ModKey, out var masters))
                    {
                        masters = mod.ModHeader.MasterReferences
                            .Select(r => r.Master.FileName.String)
                            .ToArray();
                        masterCache[mod.ModKey] = masters;
                    }

                    var winningPlugin = mod.ModKey.FileName.String;
                    yield return new IndexedRecord
                    {
                        FormKey = rec.FormKey.ToString(),
                        Type = type,
                        EditorId = rec.EditorID,
                        DisplayName = TryGetName(rec),
                        SourcePlugin = rec.FormKey.ModKey.FileName.String,
                        WinningPlugin = winningPlugin,
                        RequiredMasters = masters,
                        MajorFlags = (uint)rec.MajorRecordFlagsRaw,
                        SourceMod = pluginSourceMods is not null
                            && pluginSourceMods.TryGetValue(winningPlugin, out var src) ? src : null,
                        DetailJson = ComputeDetail(rec),
                    };
                    emitted++;
                }
            }
            log.Information("  {Type}: {Count} winning records", type, emitted);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions DetailJson = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Type-specific analysis stored alongside the record for later inspection.</summary>
    private static string? ComputeDetail(IMajorRecordGetter rec)
    {
        try
        {
            object? detail = rec switch
            {
                ICombatStyleGetter cs => CombatStyleAnalyzer.Analyze(cs),
                IRaceGetter race => RaceAnalyzer.Analyze(race),
                // Voice files are verified on demand (the asset index is built after records).
                IVoiceTypeGetter vt => VoiceClassifier.Classify(vt),
                _ => null,
            };
            return detail is null ? null : System.Text.Json.JsonSerializer.Serialize(detail, DetailJson);
        }
        catch
        {
            // Analysis is best-effort; a malformed record must never abort the index.
            return null;
        }
    }

    private static string? TryGetName(IMajorRecordGetter rec)
    {
        try
        {
            return rec switch
            {
                ITranslatedNamedGetter tn => tn.Name?.String,
                INamedGetter n => n.Name,
                _ => null,
            };
        }
        catch
        {
            // Localized plugins without loaded string tables can throw on resolution;
            // a missing display name must never abort the index.
            return null;
        }
    }
}
