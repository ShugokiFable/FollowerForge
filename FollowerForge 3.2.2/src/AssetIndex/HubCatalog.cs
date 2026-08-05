using FollowerForge.Domain;

namespace FollowerForge.AssetIndex;

/// <summary>
/// The free-to-use modder's resources FollowerForge knows about, and what each one can
/// honestly stand in for. Both entries were verified against the installed copies — the
/// coverage claims below are deliberately narrow because neither hub supplies a skin diffuse,
/// hair or eye textures, so a follower using those still depends on their source mods.
/// </summary>
public static class HubCatalog
{
    public const string MawId = "maw";
    public const string NazId = "naz";

    /// <summary>Races Naz's collection ships support maps for (folder names as shipped).</summary>
    private static readonly string[] NazRaces =
        ["Altmer", "Bosmer", "Breton", "Dunmer", "Imperial", "Nord", "Orc", "Redguard"];

    /// <summary>Support-map suffixes Naz provides for head/body/hands.</summary>
    private static readonly string[] NazHeadFiles = ["femalehead_msn", "femalehead_s", "femalehead_sk"];

    public static IReadOnlyList<AssetHub> Known { get; } =
    [
        new AssetHub
        {
            Id = MawId,
            Name = "MAW Assets",
            Author = "MAW",
            Url = "https://www.nexusmods.com/skyrimspecialedition/mods/110534",
            PluginName = "MawAssets.esp",
            MarkerPath = @"meshes\maw\mawfemale\femalehead.nif",
            Covers = [FaceAssetKind.HeadMesh, FaceAssetKind.Brows],
            Covering = "head and brow meshes with their .tri morphs, plus hairline meshes and textures",
            NotCovering = "skin colour, hair textures and eye textures",
        },
        new AssetHub
        {
            Id = NazId,
            Name = "Naz's Asset Collection",
            Author = "Nazarethblood",
            Url = "https://www.nexusmods.com/skyrimspecialedition/mods/91718",
            PluginName = null,
            MarkerPath = @"textures\nazarethblood\body\nord\femalehead_msn.dds",
            Covers = [FaceAssetKind.SkinSupportMap],
            Covering = "skin normal / specular / subsurface maps for the eight human and mer races",
            NotCovering = "the skin diffuse itself — that still comes from your own skin mod",
        },
    ];

    /// <summary>Marks which known hubs are actually installed, using the asset catalogue.</summary>
    public static IReadOnlyList<AssetHub> Detect(CatalogDb? catalog)
    {
        return Known.Select(h =>
        {
            if (catalog is null) return h;
            var asset = catalog.GetAsset(h.MarkerPath);
            return h with
            {
                Installed = asset is not null,
                SourceMod = asset?.SourceMod,
            };
        }).ToList();
    }

    /// <summary>Classifies a texture path by the part of the face it dresses.</summary>
    public static FaceAssetKind Classify(string relPath)
    {
        var p = relPath.Replace('/', '\\').ToLowerInvariant();
        var file = Path.GetFileNameWithoutExtension(p);

        // Brows are tested before eyes because "eyebrows03.dds" contains both words — but a bare
        // "brow" match would also swallow eye colours like "...\Eyes\brown\Amber Dark.dds".
        var isBrow = p.Contains("eyebrow")
                     || p.Contains(@"brows\")
                     || (file.Contains("brow") && !file.Contains("brown"));
        if (isBrow) return FaceAssetKind.Brows;
        if (p.Contains(@"\hair") || p.Contains("hairline") || p.Contains("hairdo")) return FaceAssetKind.Hair;
        if (p.Contains(@"\eyes") || file.StartsWith("eye")) return FaceAssetKind.Eyes;
        if (p.Contains(@"\mouth") || file.Contains("mouth") || file.Contains("teeth")) return FaceAssetKind.Mouth;

        var isSupport = file.EndsWith("_msn") || file.EndsWith("_n") || file.EndsWith("_s")
                        || file.EndsWith("_sk") || file.Contains("detailmap");
        if (file.Contains("head") || file.Contains("body") || file.Contains("hands"))
            return isSupport ? FaceAssetKind.SkinSupportMap : FaceAssetKind.SkinDiffuse;

        return isSupport ? FaceAssetKind.SkinSupportMap : FaceAssetKind.Other;
    }

    /// <summary>
    /// Naz equivalent for a head support map, when one exists. Race is needed because the
    /// collection is organised per race; an unknown race gets no substitute rather than a guess.
    /// </summary>
    public static string? MapToNaz(string relPath, string? raceFolder)
    {
        if (raceFolder is null) return null;
        var race = NazRaces.FirstOrDefault(r => r.Equals(raceFolder, StringComparison.OrdinalIgnoreCase));
        if (race is null) return null;

        var file = Path.GetFileNameWithoutExtension(relPath).ToLowerInvariant();
        if (file.Contains("detailmap"))
            return $@"textures\Nazarethblood\body\{race}\blankdetailmap.dds";

        var match = NazHeadFiles.FirstOrDefault(n => file.EndsWith(n[("femalehead".Length)..]) && file.Contains("head"));
        return match is null ? null : $@"textures\Nazarethblood\body\{race}\{match}.dds";
    }

    /// <summary>Maps a Skyrim race EditorID onto the folder name Naz uses.</summary>
    public static string? NazRaceFolder(string? raceEditorId) => raceEditorId?.ToLowerInvariant() switch
    {
        "nordrace" => "Nord",
        "imperialrace" => "Imperial",
        "bretonrace" => "Breton",
        "redguardrace" => "Redguard",
        "darkelfrace" => "Dunmer",
        "highelfrace" => "Altmer",
        "woodelfrace" => "Bosmer",
        "orcrace" => "Orc",
        _ => null,   // Khajiit/Argonian and custom races are not covered
    };
}
