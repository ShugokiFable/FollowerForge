namespace FollowerForge.Domain;

/// <summary>Where an asset file physically lives.</summary>
public enum AssetContainerKind
{
    /// <summary>Loose file deployed by Vortex (hardlink into game Data).</summary>
    Loose = 0,
    /// <summary>Inside a .bsa archive.</summary>
    Bsa = 1,
}

/// <summary>
/// One winning game asset (mesh/texture/sound/…), keyed by its Data-relative path
/// (lower-cased, backslash-separated — the game's own convention).
/// </summary>
public sealed record AssetFile
{
    public required string RelPath { get; init; }
    public required AssetContainerKind Container { get; init; }
    /// <summary>Staging mod folder (loose) or BSA file name (archive).</summary>
    public required string ContainerName { get; init; }
    /// <summary>Vortex staging mod folder that supplied the file, when known.</summary>
    public string? SourceMod { get; init; }
    public long Size { get; init; }
}

/// <summary>A discovered RaceMenu CharGen head export (NIF + tint DDS + optional preset).</summary>
public sealed record CharGenExport
{
    public required string Name { get; init; }
    public required string NifPath { get; init; }
    public string? TintDdsPath { get; init; }
    public string? JslotPath { get; init; }
    /// <summary>
    /// Optional RaceMenu SavePCFace companion (same stem or stem.npc). Not required to build —
    /// listed so authors who use tools that expect all four export files can see it.
    /// </summary>
    public string? SavePcFacePath { get; init; }
    /// <summary>Plugins the jslot says the face needs (mods array), when a jslot matched.</summary>
    public IReadOnlyList<string> RequiredPlugins { get; init; } = [];
    /// <summary>Record-side appearance parsed from the matching preset.</summary>
    public AppearanceSpec? PresetAppearance { get; init; }
    /// <summary>Number of NiShape blocks in the head NIF (0 = unreadable or empty).</summary>
    public int HeadShapeCount { get; init; } = -1;

    /// <summary>
    /// Why a build would reject this face, or null when it is usable. Single source of truth so
    /// the picker cannot say "ready" for something the builder then refuses.
    /// </summary>
    public string? Blocker
    {
        get
        {
            if (!File.Exists(NifPath))
                return "head export (.nif) is gone from disk — re-export Head from RaceMenu Sculpt";
            if (PresetAppearance is null)
                return "no matching RaceMenu preset (.jslot) — save the preset under the same name as the head export";
            if (HeadShapeCount == 0)
                return "head mesh has no shapes — the Export Head file is empty or corrupt; re-export in RaceMenu with every part showing";
            return null;
        }
    }

    public bool IsUsable => Blocker is null;

    /// <summary>
    /// Soft problems that do not block the build but commonly produce a flat or black face in game.
    /// Distinct from <see cref="Blocker"/> so the UI can show READY vs NO SCULPT vs CANNOT BUILD.
    /// </summary>
    public IReadOnlyList<string> QualityNotes
    {
        get
        {
            var notes = new List<string>();
            if (TintDdsPath is null || !File.Exists(TintDdsPath))
                notes.Add("no FaceTint DDS — her face can look grey or wrong until you re-export with tint");
            if (PresetAppearance is { SliderCount: >= 10, SculptedVertices: 0 })
                notes.Add(
                    $"shape is {PresetAppearance.SliderCount} RaceMenu sliders with no sculpt — "
                    + "sliders do not become NPC geometry; sculpt anything in RaceMenu then Export Head again "
                    + "(this is not the same as a missing head-part / black-face export)");
            // Only when nifly could not open the mesh — not the old SavePCFace noise on every export.
            if (HeadShapeCount < 0)
                notes.Add("could not verify head mesh shapes (file may be unreadable) — re-export Head if the face looks wrong");
            return notes;
        }
    }
}
