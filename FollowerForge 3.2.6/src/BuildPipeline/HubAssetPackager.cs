using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using FollowerForge.FaceGen;
using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Handles the appearance assets a follower leans on.
///
/// <para><b>Free hubs</b> swap the slots a free modder's resource genuinely covers (Naz's skin
/// support maps) so those stop being a dependency on someone's private mod.</para>
///
/// <para><b>Own hub</b> copies the assets she actually uses into the author's own prefixed
/// folders and repoints the face at them. That only ever happens after the author states, in
/// writing, that they hold the right to redistribute those files — FollowerForge never infers
/// permission and never signs that statement on anyone's behalf.</para>
/// </summary>
public sealed class HubAssetPackager(ILogger log)
{
    public sealed record Result(
        IReadOnlyList<FaceAsset> Assets,
        int Retargeted,
        int Copied,
        IReadOnlyList<string> HubsUsed);

    /// <param name="faceGeomPath">The generated FaceGeom NIF inside the staging package.</param>
    /// <param name="stagingRoot">Package root; copies land under it as Data-relative paths.</param>
    public Result Apply(
        HubMode mode,
        string faceGeomPath,
        string stagingRoot,
        string? raceEditorId,
        string? ownHubPrefix,
        string? redistributionPermission,
        EnvironmentSnapshot env,
        CatalogDb? catalog,
        ValidationReport report)
    {
        var assets = new List<FaceAsset>();
        var hubsUsed = new List<string>();
        int retargeted = 0, copied = 0;

        if (!File.Exists(faceGeomPath))
            return new Result(assets, 0, 0, hubsUsed);

        // Check the author's declaration once, before touching anything, so a missing one is a
        // single clear refusal rather than the same complaint repeated per texture.
        if (mode == HubMode.OwnHub)
        {
            if (string.IsNullOrWhiteSpace(redistributionPermission))
            {
                report.Add(ValidationSeverity.Error, "HUB_NO_DECLARATION",
                    "Building your own asset hub copies other people's files. Set " +
                    "RedistributionPermission in the profile — stating that you have checked each " +
                    "source mod's terms and may redistribute them — before this can run.");
                return new Result(assets, 0, 0, hubsUsed);
            }
            if (string.IsNullOrWhiteSpace(ownHubPrefix))
            {
                report.Add(ValidationSeverity.Error, "HUB_NO_PREFIX",
                    "Your asset hub needs a name (OwnHubPrefix), e.g. \"KarloAssets\".");
                return new Result(assets, 0, 0, hubsUsed);
            }
        }

        var hubs = HubCatalog.Detect(catalog);
        var guard = EnvironmentDiscovery.CreateGuard(env);

        using var nif = new NifHeadFile();
        nif.Load(faceGeomPath);
        var changed = false;

        foreach (var (shape, slot, path) in nif.AllSlots().ToList())
        {
            // The follower's own generated tint lives in her package already.
            if (path.Contains(@"facegendata\facetint", StringComparison.OrdinalIgnoreCase))
                continue;

            var kind = HubCatalog.Classify(path);
            var asset = catalog?.GetAsset(path);
            var entry = new FaceAsset
            {
                RelPath = path,
                Kind = kind,
                Resolved = asset is not null,
                SourceMod = asset?.SourceMod,
                Container = asset?.ContainerName,
            };

            if (mode == HubMode.FreeHubs && kind == FaceAssetKind.SkinSupportMap)
            {
                var naz = hubs.FirstOrDefault(h => h.Id == HubCatalog.NazId && h.Installed);
                var replacement = naz is null
                    ? null
                    : HubCatalog.MapToNaz(path, HubCatalog.NazRaceFolder(raceEditorId));
                if (replacement is not null && catalog?.GetAsset(replacement) is not null)
                {
                    // A head-only normal/specular substitution can visibly disagree with the
                    // body's installed skin at the neck. Record the available free resource, but
                    // do not retarget until a complete matched head+body skin set is packaged.
                    entry = entry with { CoverableByHub = HubCatalog.NazId };
                }
            }
            else if (mode == HubMode.OwnHub)
            {
                var (newPath, didCopy) = CopyIntoOwnHub(
                    path, kind, asset, ownHubPrefix, stagingRoot, env, guard, redistributionPermission, report);
                if (newPath is not null)
                {
                    nif.SetSlot(shape, slot, newPath);
                    changed = true;
                    retargeted++;
                    entry = entry with { RetargetedTo = newPath };
                }
                if (didCopy) copied++;
            }

            assets.Add(entry);
        }

        if (changed)
        {
            nif.Save(faceGeomPath);
            log.Information("Face assets: {Retargeted} paths repointed, {Copied} files copied", retargeted, copied);
        }

        return new Result(assets, retargeted, copied, hubsUsed);
    }

    /// <summary>
    /// Copies one texture into the author's hub folders. Refuses without a written declaration,
    /// and never unpacks BSAs — extracting another author's archive is their call, not ours.
    /// </summary>
    private (string? NewPath, bool Copied) CopyIntoOwnHub(
        string relPath, FaceAssetKind kind, AssetFile? asset, string? prefix, string stagingRoot,
        EnvironmentSnapshot env, WriteGuard guard, string? permission, ValidationReport report)
    {
        // The declaration and prefix were already validated once by the caller.
        if (string.IsNullOrWhiteSpace(permission) || string.IsNullOrWhiteSpace(prefix)) return (null, false);
        if (asset is null) return (null, false);

        if (asset.Container == AssetContainerKind.Bsa)
        {
            report.Add(ValidationSeverity.Warning, "HUB_ASSET_IN_BSA",
                $"Left inside {asset.ContainerName}; unpack it yourself if its licence allows: {relPath}", relPath);
            return (null, false);
        }

        var source = Path.Combine(env.GameDataPath, relPath);
        if (!File.Exists(source)) return (null, false);

        // textures\<Prefix>\<kind>\<file>  — a tidy, collision-free layout the hub owns.
        var folder = kind switch
        {
            FaceAssetKind.Hair => "hair",
            FaceAssetKind.Eyes => "eyes",
            FaceAssetKind.Brows => "brows",
            FaceAssetKind.Mouth => "mouth",
            FaceAssetKind.SkinDiffuse or FaceAssetKind.SkinSupportMap => "skin",
            _ => "misc",
        };
        var newRel = Path.Combine("textures", prefix, folder, Path.GetFileName(relPath));
        var dest = Path.Combine(stagingRoot, newRel);
        guard.EnsureWritable(dest);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(source, dest, overwrite: true);
        return (newRel, true);
    }

    /// <summary>The declaration the author must be able to stand behind, written next to the build.</summary>
    public static string BuildPermissionsDocument(
        string followerName, string? prefix, string? declaration, IReadOnlyList<FaceAsset> assets)
    {
        var copied = assets.Where(a => a.RetargetedTo is not null && a.SourceMod is not null).ToList();
        var byMod = copied.GroupBy(a => a.SourceMod!).OrderBy(g => g.Key);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Redistribution declaration - {followerName}");
        sb.AppendLine();
        sb.AppendLine($"Asset hub prefix: `{prefix}`");
        sb.AppendLine();
        sb.AppendLine("## What you are stating");
        sb.AppendLine();
        sb.AppendLine("> " + (string.IsNullOrWhiteSpace(declaration) ? "(no declaration recorded)" : declaration));
        sb.AppendLine();
        sb.AppendLine("FollowerForge copied the files below because you said you may redistribute them.");
        sb.AppendLine("It did not verify that, and it cannot. Permission comes from each asset's author -");
        sb.AppendLine("check every mod page's permissions section before you upload this anywhere.");
        sb.AppendLine("Credit alone is not permission.");
        sb.AppendLine();
        sb.AppendLine("## Files copied, by source mod");
        if (copied.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("- (none)");
        }
        foreach (var group in byMod)
        {
            sb.AppendLine();
            sb.AppendLine($"### {group.Key}");
            foreach (var a in group.OrderBy(a => a.RelPath))
                sb.AppendLine($"- `{a.RelPath}`  ->  `{a.RetargetedTo}`");
        }
        return sb.ToString();
    }
}
