using System.Globalization;
using System.Text.Json;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.AssetIndex;

/// <summary>
/// Finds RaceMenu CharGen head exports and their matching presets. A face export is two halves:
/// the NIF/DDS assets and the NPC-record data in the jslot. Both are required for a faithful NPC.
/// Under MO2, CharGen folders inside enabled mods are scanned as well as game Data.
/// </summary>
public sealed class CharGenDiscovery(ILogger log)
{
    public const string CharGenRelDir = @"SKSE\Plugins\CharGen";

    public IReadOnlyList<CharGenExport> Discover(EnvironmentSnapshot env)
    {
        var roots = new List<string> { env.GameDataPath };
        if (env.Manager == ModManagerKind.Mo2)
        {
            foreach (var modDir in Mo2PathResolver.EnumerateModDirsHighestFirst(env))
                roots.Add(modDir);
        }

        var byName = new Dictionary<string, CharGenExport>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var export in DiscoverInDataRoot(root))
            {
                // Higher-priority roots are enumerated first for MO2; keep the first win.
                byName.TryAdd(export.Name, export);
            }
        }

        var exports = byName.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        log.Information("CharGen discovery: {Count} head exports ({WithPreset} with matching jslot)",
            exports.Count, exports.Count(e => e.JslotPath is not null));
        return exports;
    }

    /// <summary>Legacy single-folder scan (game Data or a test root).</summary>
    public IReadOnlyList<CharGenExport> Discover(string gameDataPath) => DiscoverInDataRoot(gameDataPath);

    private IReadOnlyList<CharGenExport> DiscoverInDataRoot(string gameDataPath)
    {
        var dir = Path.Combine(gameDataPath, CharGenRelDir);
        if (!Directory.Exists(dir))
            return [];

        var presets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var presetDir in new[] { Path.Combine(dir, "Presets"), Path.Combine(dir, "Exported") })
        {
            if (!Directory.Exists(presetDir)) continue;
            foreach (var jslot in Directory.EnumerateFiles(presetDir, "*.jslot"))
                presets.TryAdd(NormalizeKey(Path.GetFileNameWithoutExtension(jslot)), jslot);
        }

        // Optional SavePCFace companions: same stem .npc, or *SavePCFace* near the export.
        var savePc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in Directory.EnumerateFiles(dir, "*.npc", SearchOption.TopDirectoryOnly))
            savePc.TryAdd(NormalizeKey(Path.GetFileNameWithoutExtension(npc)), npc);
        foreach (var extra in Directory.EnumerateFiles(dir, "*SavePCFace*", SearchOption.TopDirectoryOnly))
            savePc.TryAdd(NormalizeKey(Path.GetFileNameWithoutExtension(extra)), extra);

        var exports = new List<CharGenExport>();
        foreach (var nif in Directory.EnumerateFiles(dir, "*.nif", SearchOption.TopDirectoryOnly))
        {
            var stem = Path.GetFileNameWithoutExtension(nif);
            var dds = Path.ChangeExtension(nif, ".dds");
            var jslotPath = MatchPreset(stem, presets);
            var preset = jslotPath is null ? null : ReadJslot(jslotPath);
            savePc.TryGetValue(NormalizeKey(stem), out var savePath);

            exports.Add(new CharGenExport
            {
                Name = stem,
                NifPath = nif,
                TintDdsPath = File.Exists(dds) ? dds : null,
                JslotPath = jslotPath,
                SavePcFacePath = savePath is not null && File.Exists(savePath) ? savePath : null,
                RequiredPlugins = preset?.RequiredPlugins ?? [],
                PresetAppearance = preset?.Appearance,
                // Shape count is validated during FaceGen; -1 = not probed at discovery (keeps scan fast).
                HeadShapeCount = -1,
            });
        }

        return exports;
    }

    /// <summary>
    /// Decorations people leave on one side of the pair but not the other. RaceMenu writes
    /// "&lt;name&gt;_head.nif" while the preset is often saved as "&lt;name&gt;_Replacer.jslot" or
    /// "&lt;name&gt;Sculpt", so a plain equality check rejects faces whose preset is sitting right
    /// there. Verified against this machine: Matilda/Eva/Samantha use _Replacer, Morya uses Sculpt.
    /// </summary>
    private static readonly string[] IgnorableSuffixes =
        ["head", "replacer", "sculpt", "preset", "chargen", "export", "final", "face"];

    private static string NormalizeKey(string name)
    {
        var normalized = new string(name.Trim()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray())
            .ToLowerInvariant();

        // Peel decorations off the end, repeatedly: "matildareplacer" -> "matilda".
        bool trimmed;
        do
        {
            trimmed = false;
            foreach (var suffix in IgnorableSuffixes)
            {
                if (normalized.Length > suffix.Length && normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    normalized = normalized[..^suffix.Length];
                    trimmed = true;
                }
            }
        } while (trimmed);

        return normalized;
    }

    /// <summary>
    /// Finds the preset belonging to a head export: exact match on the normalised name, then a
    /// single unambiguous prefix match. Ambiguity is left unmatched rather than guessed — pairing
    /// a face with the wrong preset would silently build the wrong follower.
    /// </summary>
    private static string? MatchPreset(string exportStem, Dictionary<string, string> presets)
    {
        var key = NormalizeKey(exportStem);
        if (key.Length == 0) return null;
        if (presets.TryGetValue(key, out var exact)) return exact;

        var candidates = presets
            .Where(p => p.Key.Length > 2
                        && (key.StartsWith(p.Key, StringComparison.Ordinal)
                            || p.Key.StartsWith(key, StringComparison.Ordinal)))
            .OrderByDescending(p => p.Key.Length)
            .ToList();

        if (candidates.Count == 0) return null;
        // Only accept a clear winner: two presets of equal length both matching is a coin flip.
        if (candidates.Count > 1 && candidates[0].Key.Length == candidates[1].Key.Length) return null;
        return candidates[0].Value;
    }

    /// <summary>
    /// Reads every plugin named by the preset. Head-part formIdentifier values are included
    /// because RaceMenu's mods table can omit light/overlay plugins.
    /// </summary>
    public IReadOnlyList<string> ReadJslotPlugins(string jslotPath)
        => ReadJslot(jslotPath)?.RequiredPlugins ?? [];

    /// <summary>Reads the NPC-record appearance that must accompany the exported NIF/DDS.</summary>
    public AppearanceSpec? ReadJslotAppearance(string jslotPath)
        => ReadJslot(jslotPath)?.Appearance;

    private sealed record ParsedJslot(
        IReadOnlyList<string> RequiredPlugins,
        AppearanceSpec Appearance);

    /// <summary>
    /// Turns a RaceMenu "Plugin.esp|001234" identifier into a Mutagen FormKey and records the
    /// plugin as required. Light-plugin ids are ambiguous here — the caller resolves those
    /// against the catalogue rather than guessing a mask.
    /// </summary>
    private static RecordRef? ParseIdentifier(string? identifier, HashSet<string> required)
    {
        var pipe = identifier?.IndexOf('|') ?? -1;
        if (pipe <= 0 || pipe >= identifier!.Length - 1) return null;
        var plugin = identifier[..pipe];
        if (!uint.TryParse(identifier[(pipe + 1)..], NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var localId))
            return null;
        required.Add(plugin);
        return new RecordRef($"{localId:X6}:{plugin}");
    }

    private ParsedJslot? ReadJslot(string jslotPath)
    {
        try
        {
            using var stream = File.OpenRead(jslotPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var modsByIndex = new Dictionary<int, string>();
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
            {
                foreach (var mod in mods.EnumerateArray())
                {
                    if (!mod.TryGetProperty("name", out var nameElement)
                        || string.IsNullOrWhiteSpace(nameElement.GetString()))
                        continue;
                    var name = nameElement.GetString()!;
                    required.Add(name);
                    if (mod.TryGetProperty("index", out var indexElement)
                        && indexElement.TryGetInt32(out var index))
                        modsByIndex[index] = name;
                }
            }

            var headParts = new List<RecordRef>();
            if (root.TryGetProperty("headParts", out var headPartArray)
                && headPartArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in headPartArray.EnumerateArray())
                {
                    string? formKey = null;
                    uint rawFormId = 0;
                    var hasRawFormId = part.TryGetProperty("formId", out var rawFormIdElement)
                        && rawFormIdElement.TryGetUInt32(out rawFormId);
                    var rawIsLight = hasRawFormId && (rawFormId >> 24) == 0xFE;
                    if (part.TryGetProperty("formIdentifier", out var identifierElement))
                    {
                        var identifier = identifierElement.GetString();
                        var pipe = identifier?.IndexOf('|') ?? -1;
                        if (pipe > 0 && pipe < identifier!.Length - 1)
                        {
                            var plugin = identifier[..pipe];
                            var hex = identifier[(pipe + 1)..];
                            if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                                    out var localId))
                            {
                                if (rawIsLight) localId &= 0xFFF;
                                required.Add(plugin);
                                formKey = $"{localId:X6}:{plugin}";
                            }
                        }
                    }

                    if (formKey is null
                        && hasRawFormId)
                    {
                        var index = (int)(rawFormId >> 24);
                        if (modsByIndex.TryGetValue(index, out var plugin))
                        {
                            var localId = index == 0xFE
                                ? rawFormId & 0xFFF
                                : rawFormId & 0x00FFFFFF;
                            formKey = $"{localId:X6}:{plugin}";
                        }
                    }

                    if (formKey is not null)
                        headParts.Add(new RecordRef(formKey));
                }
            }

            float weight = 50f;
            uint? hairColor = null;
            RecordRef? headTexture = null;
            if (root.TryGetProperty("actor", out var actor))
            {
                if (actor.TryGetProperty("weight", out var weightElement)
                    && weightElement.TryGetSingle(out var parsedWeight))
                    weight = parsedWeight;
                if (actor.TryGetProperty("hairColor", out var hairElement)
                    && hairElement.TryGetUInt32(out var parsedHair))
                    hairColor = parsedHair;
                // The face texture set the preset was built on. Without it the follower falls
                // back to her race's default complexion while the exported head keeps the
                // preset's — which shows up as a skin-tone seam at the neck. Older presets
                // (and creature-race ones) omit it, so it stays optional.
                if (actor.TryGetProperty("headTexture", out var headTextureElement)
                    && ParseIdentifier(headTextureElement.GetString(), required) is { } textureRef)
                    headTexture = textureRef;
            }

            // How the preset's shape is stored decides whether it can become an NPC at all.
            // RaceMenu slider values (CME_/EFM_/SPG_) are applied to a live actor's head nodes at
            // runtime; they are not vertex data and there is nowhere on an NPC record to put them.
            // Only the vanilla 19 morphs and sculpt vertex deltas survive into a follower.
            var sliderCount = 0;
            var sculptedVertices = 0;
            var morphsElement = root.TryGetProperty("morphs", out var allMorphs) ? allMorphs : default;
            if (morphsElement.ValueKind == JsonValueKind.Object)
            {
                if (morphsElement.TryGetProperty("custom", out var custom)
                    && custom.ValueKind == JsonValueKind.Array)
                    sliderCount = custom.EnumerateArray().Count(entry =>
                        entry.TryGetProperty("value", out var v)
                        && v.TryGetSingle(out var f) && Math.Abs(f) > 0.001f);

                if (morphsElement.TryGetProperty("sculpt", out var sculpt)
                    && sculpt.ValueKind == JsonValueKind.Array)
                    foreach (var host in sculpt.EnumerateArray())
                        if (host.TryGetProperty("data", out var deltas)
                            && deltas.ValueKind == JsonValueKind.Array)
                            sculptedVertices += deltas.GetArrayLength();
            }

            var faceMorphs = new List<float>();
            var facePresets = new List<uint>();
            if (root.TryGetProperty("morphs", out var morphs)
                && morphs.TryGetProperty("default", out var defaults))
            {
                if (defaults.TryGetProperty("morphs", out var morphArray)
                    && morphArray.ValueKind == JsonValueKind.Array)
                    faceMorphs.AddRange(morphArray.EnumerateArray().Take(19).Select(v => v.GetSingle()));
                if (defaults.TryGetProperty("presets", out var presetArray)
                    && presetArray.ValueKind == JsonValueKind.Array)
                    facePresets.AddRange(presetArray.EnumerateArray().Take(4).Select(v => v.GetUInt32()));
            }

            var tintLayers = new List<TintLayerSpec>();
            string? skinTone = null;
            if (root.TryGetProperty("tintInfo", out var tintArray)
                && tintArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var tint in tintArray.EnumerateArray())
                {
                    if (!tint.TryGetProperty("index", out var indexElement)
                        || !indexElement.TryGetUInt16(out var index)
                        || !tint.TryGetProperty("color", out var colorElement)
                        || !colorElement.TryGetUInt32(out var color))
                        continue;
                    tintLayers.Add(new TintLayerSpec(index, color));
                    if (index == 0) skinTone = ArgbToRgba(color);
                }
            }

            return new ParsedJslot(
                required.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList(),
                new AppearanceSpec
                {
                    HeadParts = headParts,
                    Weight = weight,
                    FaceMorphs = faceMorphs,
                    FacePresets = facePresets,
                    TintLayers = tintLayers,
                    HairColorArgb = hairColor,
                    SkinToneRgba = skinTone,
                    HeadTextureSet = headTexture,
                    SliderCount = sliderCount,
                    SculptedVertices = sculptedVertices,
                });
        }
        catch (Exception ex)
        {
            log.Warning("Unreadable jslot {Path}: {Error}", jslotPath, ex.Message);
            return null;
        }
    }

    private static string ArgbToRgba(uint argb) =>
        $"{(argb >> 16) & 0xFF:X2}{(argb >> 8) & 0xFF:X2}{argb & 0xFF:X2}{(argb >> 24) & 0xFF:X2}";
}
