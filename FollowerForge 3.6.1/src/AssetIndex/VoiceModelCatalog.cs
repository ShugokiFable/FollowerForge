using System.Text.Json;

namespace FollowerForge.AssetIndex;

/// <summary>
/// An xVASynth voice model that can speak custom lines for a follower.
/// <paramref name="BaseSpeakerEmb"/> comes from the model's own json (games[0].base_speaker_emb)
/// and must be passed on every synthesis call — v3 models produce nothing usable without it.
/// </summary>
public sealed record VoiceModel(
    string VoiceTypeEditorId,
    string ModelId,
    string CheckpointPath,
    string ModelType,
    string? BaseSpeakerEmb);

/// <summary>
/// Maps Skyrim voice types onto the xVASynth models installed on this machine, so the wizard can
/// say which voices can actually have custom lines synthesised rather than offering all of them
/// and failing later.
///
/// Naming was verified against the installed set: the model for VoiceType "FemaleEvenToned" is
/// "sk_femaleeventoned" (.pt checkpoint beside a .json describing it).
/// </summary>
public sealed class VoiceModelCatalog
{
    public const string DefaultXvaSynthRoot =
        @"C:\Program Files (x86)\Steam\steamapps\common\xVASynth";

    private readonly Dictionary<string, VoiceModel> _byVoiceType = new(StringComparer.OrdinalIgnoreCase);

    public string Root { get; }
    public bool Installed { get; }
    public IReadOnlyCollection<VoiceModel> Models => _byVoiceType.Values;

    public VoiceModelCatalog(string? xvaSynthRoot = null)
    {
        Root = FollowerForge.ModManagers.XvaSynthLocator.Resolve(xvaSynthRoot);
        var modelsDir = Path.Combine(Root, "resources", "app", "models", "skyrim");
        Installed = Directory.Exists(modelsDir);
        if (!Installed) return;

        foreach (var checkpoint in Directory.EnumerateFiles(modelsDir, "sk_*.pt"))
        {
            var modelId = Path.GetFileNameWithoutExtension(checkpoint);
            // "sk_femaleeventoned" -> voice type "femaleeventoned"
            var voiceKey = modelId["sk_".Length..];
            var (modelType, emb) = ReadModelJson(Path.ChangeExtension(checkpoint, ".json"));
            _byVoiceType[voiceKey] = new VoiceModel(voiceKey, modelId, checkpoint, modelType, emb);
        }
    }

    /// <summary>The model that can speak as this voice type, or null when none is installed.</summary>
    public VoiceModel? ForVoiceType(string? voiceTypeEditorId) =>
        voiceTypeEditorId is not null && _byVoiceType.TryGetValue(voiceTypeEditorId, out var m) ? m : null;

    public bool CanSpeak(string? voiceTypeEditorId) => ForVoiceType(voiceTypeEditorId) is not null;

    /// <summary>Paths to the lip/fuz toolchain shipped with xVASynth's lip_fuz plugin.</summary>
    public string LipFuzDir => Path.Combine(Root, "resources", "app", "plugins", "lip_fuz");
    public string FaceFxWrapper => Path.Combine(LipFuzDir, "FaceFXWrapper.exe");
    public string FonixData => Path.Combine(LipFuzDir, "FonixData.cdf");
    public string XwmaEncode => Path.Combine(LipFuzDir, "xWMAEncode.exe");
    public string FuzExtractor => Path.Combine(LipFuzDir, "fuz_extractor.exe");

    /// <summary>True when every tool needed to turn a WAV into a lipsynced .fuz is present.</summary>
    public bool CanMakeFuz =>
        File.Exists(FaceFxWrapper) && File.Exists(FonixData)
        && File.Exists(XwmaEncode) && File.Exists(FuzExtractor);

    /// <summary>
    /// Server binary. Mantella — which drives this same server for live dialogue — launches it
    /// with the working directory set to the xVASynth ROOT, so that is what we match.
    /// </summary>
    public string ServerExe => Path.Combine(Root, "resources", "app", "cpython_cpu", "server.exe");
    public string ServerWorkingDirectory => Root;

    /// <summary>
    /// Reads the model's architecture and speaker embedding from its own json. Both are handed
    /// straight to xVASynth: guessing the type makes /loadModel fail, and omitting the embedding
    /// makes v3 models synthesise nothing usable.
    /// </summary>
    private static (string ModelType, string? BaseSpeakerEmb) ReadModelJson(string jsonPath)
    {
        const string fallback = "FastPitch1.1";
        if (!File.Exists(jsonPath)) return (fallback, null);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = doc.RootElement;

            var modelType = root.TryGetProperty("modelType", out var t) && t.GetString() is { Length: > 0 } s
                ? s
                : fallback;

            // games[0].base_speaker_emb, flattened to a bare comma list exactly as xVASynth wants.
            string? emb = null;
            if (root.TryGetProperty("games", out var games)
                && games.ValueKind == JsonValueKind.Array
                && games.GetArrayLength() > 0
                && games[0].TryGetProperty("base_speaker_emb", out var e))
            {
                emb = e.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", e.EnumerateArray().Select(v => v.ToString()))
                    : e.GetString();
            }
            return (modelType, emb);
        }
        catch (JsonException)
        {
            return (fallback, null);   // malformed model json: fall back rather than crash discovery
        }
    }
}
