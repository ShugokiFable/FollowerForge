namespace FollowerForge.Domain;

/// <summary>Asset/dependency strategy for a generated follower.</summary>
public enum OutputStrategy
{
    /// <summary>Reference installed records/files; add source plugins as masters; copy nothing shared.</summary>
    PackLocalReference = 0,
    /// <summary>Depend on a generated shared hub plugin + asset package.</summary>
    SharedHub = 1,
    /// <summary>Copy assets after explicit user permission declaration.</summary>
    PortableStandalone = 2,
}

/// <summary>How the follower's level scales.</summary>
public sealed record LevelScaling
{
    /// <summary>True = level scales with the player (PC Level Mult).</summary>
    public bool ScaleWithPlayer { get; init; } = true;
    /// <summary>Multiplier ×1000 as stored by the game (1.0 = match player). Used when scaling.</summary>
    public double PlayerLevelMult { get; init; } = 1.0;
    public short MinLevel { get; init; } = 10;
    public short MaxLevel { get; init; } = 0; // 0 = no cap
    /// <summary>Fixed level when not scaling with the player.</summary>
    public short FixedLevel { get; init; } = 20;
}

/// <summary>AI stats (vanilla value ranges).</summary>
public sealed record AiValues
{
    public byte Aggression { get; init; } = 0;   // 0 unaggressive .. 3 frenzied
    public byte Confidence { get; init; } = 3;   // 0 cowardly .. 4 foolhardy
    public byte Assistance { get; init; } = 2;   // 0 helps nobody .. 2 helps friends+allies
    public byte Morality { get; init; } = 0;     // 0 any crime .. 3 no crime
    public byte Energy { get; init; } = 50;      // 0..100
}

/// <summary>Reference to an existing record in the modpack: "123ABC:Plugin.esp" (Mutagen FormKey string).</summary>
public sealed record RecordRef(string FormKey)
{
    public override string ToString() => FormKey;
}

/// <summary>Where the follower is placed in the world.</summary>
public sealed record PlacementSpec
{
    /// <summary>
    /// Preferred: an id from the spawn-location library ("whiterun-the-bannered-mare-016069").
    /// The user picks a place by name and the proven coordinates come with it.
    /// </summary>
    public string? LocationId { get; init; }

    /// <summary>FormKey of the CELL (interior) or worldspace-cell to place the ACHR in.</summary>
    public RecordRef? Cell { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float AngleZDeg { get; init; }

    /// <summary>
    /// Extra location ids. With any of these she starts at one of the listed places at random
    /// rather than always the same one — the Enemy-to-Ally idea, using our own script. Up to
    /// four are used.
    /// </summary>
    public IReadOnlyList<string> AlternateLocationIds { get; init; } = [];
}

/// <summary>How the follower's appearance is produced.</summary>
public sealed record AppearanceSpec
{
    /// <summary>Name of a CharGen export (preferred pipeline). Null = plain record-defined appearance.</summary>
    public string? CharGenExportName { get; init; }
    /// <summary>Explicit CharGen NIF path override (else resolved by export name).</summary>
    public string? CharGenNifPath { get; init; }
    public string? CharGenTintPath { get; init; }
    public string? JslotPath { get; init; }
    /// <summary>Head part FormKeys used when building without CharGen (vanilla-style face).</summary>
    public IReadOnlyList<RecordRef> HeadParts { get; init; } = [];
    public float Weight { get; init; } = 50f;
    /// <summary>RaceMenu's 19 NAM9 face-morph values. FLT_MAX means unused.</summary>
    public IReadOnlyList<float> FaceMorphs { get; init; } = [];
    /// <summary>RaceMenu's four NAMA face preset indices. uint.MaxValue means unused.</summary>
    public IReadOnlyList<uint> FacePresets { get; init; } = [];
    /// <summary>RaceMenu tint-layer metadata paired with the exported FaceTint DDS.</summary>
    public IReadOnlyList<TintLayerSpec> TintLayers { get; init; } = [];
    /// <summary>RaceMenu hair colour as packed ARGB.</summary>
    public uint? HairColorArgb { get; init; }
    /// <summary>RGBA text like "D0A575FF" — face tint average for the NPC record (QNAM).</summary>
    public string? SkinToneRgba { get; init; }
}

/// <summary>One RaceMenu tint layer stored on the NPC record.</summary>
public sealed record TintLayerSpec(ushort Index, uint ArgbColor);

/// <summary>Combat style choice: reference an existing CSTY or clone it into the output plugin.</summary>
public sealed record CombatStyleChoice
{
    public required RecordRef Style { get; init; }
    /// <summary>True = copy the CSTY into the follower plugin (user may tweak); false = reference it.</summary>
    public bool CloneIntoPlugin { get; init; }
}

/// <summary>
/// The complete, serializable input of the deterministic follower compiler.
/// Same profile + same modpack ⇒ byte-identical output.
/// </summary>
public sealed record FollowerProfile
{
    public required string Name { get; init; }
    /// <summary>Output plugin file name, e.g. "FF_Natalie.esp".</summary>
    public required string PluginName { get; init; }
    /// <summary>EditorID prefix; default derived from Name.</summary>
    public string? EditorIdPrefix { get; init; }
    public required RecordRef Race { get; init; }
    public bool Female { get; init; } = true;
    public required RecordRef VoiceType { get; init; }
    public required RecordRef Class { get; init; }
    public CombatStyleChoice? CombatStyle { get; init; }
    /// <summary>
    /// Optional legacy OTFT compatibility reference. The wizard's primary workflow uses actual
    /// ARMO records in <see cref="InventoryItems"/> so the follower owns and can trade her gear.
    /// </summary>
    public RecordRef? Outfit { get; init; }

    /// <summary>
    /// Her body: a skin ARMO assigned to the NPC (WornArmor). Leave null and she uses her race's
    /// default skin — i.e. whatever body replacer the player has installed, which is usually
    /// what you want. Set it only to pin her to a specific body/skin record.
    /// </summary>
    public RecordRef? SkinArmor { get; init; }
    /// <summary>
    /// Selected ARMO records that should be equipped when the follower first loads. The compiler
    /// turns these actual equipment choices into a private engine OTFT and also keeps them in
    /// inventory, so the user never has to pick a pre-authored outfit.
    /// </summary>
    public IReadOnlyList<RecordRef> EquippedArmor { get; init; } = [];
    public IReadOnlyList<RecordRef> InventoryItems { get; init; } = [];
    public IReadOnlyList<RecordRef> Spells { get; init; } = [];
    public IReadOnlyList<RecordRef> Perks { get; init; } = [];
    public AiValues Ai { get; init; } = new();
    public LevelScaling Level { get; init; } = new();
    /// <summary>
    /// Automatic by default. Custom mode writes every Skyrim skill plus Health, Magicka, and
    /// Stamina into the NPC's DNAM block and disables the AutoCalcStats flag.
    /// </summary>
    public FollowerStats Stats { get; init; } = new();
    /// <summary>
    /// Makes her a vampire: her race is swapped for its vampire form and she gains the Vampire
    /// and ActorTypeUndead keywords. Needs no script — that is all a vanilla vampire NPC has.
    /// The build fails rather than quietly producing a mortal if her race has no vampire form.
    /// </summary>
    public bool IsVampire { get; init; }
    public bool Protected { get; init; } = true;
    public bool Essential { get; init; }
    public bool Marriageable { get; init; }
    public required PlacementSpec Placement { get; init; }
    public AppearanceSpec Appearance { get; init; } = new();
    public OutputStrategy Strategy { get; init; } = OutputStrategy.PackLocalReference;
    /// <summary>Hub plugin name this follower depends on (SharedHub strategy).</summary>
    public string? HubPluginName { get; init; }

    /// <summary>How her appearance assets are handled — see <see cref="HubMode"/>.</summary>
    public HubMode Hub { get; init; } = HubMode.ReferenceInstalled;

    /// <summary>Folder prefix for your own asset hub, e.g. "Karlo" → textures\Karlo\…</summary>
    public string? OwnHubPrefix { get; init; }
    /// <summary>Explicit user permission statement required before any asset copying.</summary>
    public string? RedistributionPermission { get; init; }
    /// <summary>Optional explicit follower-framework integration (none by default).</summary>
    public string? FrameworkIntegration { get; init; }
    /// <summary>Optional custom spoken lines. Empty means she uses only her voice type's stock dialogue.</summary>
    public DialogueSpec Dialogue { get; init; } = new();
    /// <summary>Her idle routine and how she regards the player.</summary>
    public BehaviorSpec Behavior { get; init; } = new();
    /// <summary>Optional, experimental: she grows in confidence and skill as she fights beside you.</summary>
    public EvolutionSpec Evolution { get; init; } = new();
    /// <summary>Optional, experimental: she changes form in combat.</summary>
    public TransformSpec Transformation { get; init; } = new();
    /// <summary>Optional: meet her as an enemy, beat her, then summon her as an ally.</summary>
    public EnemyToAllySpec EnemyToAlly { get; init; } = new();
    /// <summary>
    /// Optional fixed timestamp for reproducible non-ESP metadata. Zero uses the actual build
    /// time so a newly generated follower does not appear to contain years-old files.
    /// </summary>
    public long BuildTimestampUnix { get; init; }
}
