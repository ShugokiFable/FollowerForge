using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Builds many followers in one pass, sharing the placement worldspace and catalogue so a
/// batch is not paying per-follower open costs. Each follower is still an independent atomic
/// build; one failure does not abort the rest.
/// </summary>
public sealed class BatchBuilder(ILogger log)
{
    public sealed record BatchItemResult(string Name, bool Success, string OutputDirectory, int Errors, int Warnings);
    public sealed record BatchResult(IReadOnlyList<BatchItemResult> Items)
    {
        public int Succeeded => Items.Count(i => i.Success);
        public int Failed => Items.Count(i => !i.Success);
    }

    public BatchResult Build(
        IReadOnlyList<FollowerProfile> profiles, EnvironmentSnapshot env, string workspaceRoot,
        CatalogDb? catalog = null)
    {
        var builder = new FollowerBuilder(log);
        using var placement = new PlacementResolver(log);

        // Resolve the default Whiterun worldspace once for the whole batch.
        IWorldspaceGetter? sharedWorldspace = null;
        var anyDefault = profiles.FirstOrDefault(p => WantsDefaultWhiterun(p));
        if (anyDefault is not null)
            sharedWorldspace = placement.ResolveDefaultWorldspace(env, anyDefault);

        var items = new List<BatchItemResult>();
        foreach (var profile in profiles)
        {
            try
            {
                var ws = WantsDefaultWhiterun(profile) ? sharedWorldspace : null;
                var result = builder.Build(profile, env, workspaceRoot, ws, catalog);
                items.Add(new BatchItemResult(
                    profile.Name, result.Success, result.OutputDirectory,
                    result.Validation.Findings.Count(f => f.Severity == ValidationSeverity.Error),
                    result.Validation.Findings.Count(f => f.Severity == ValidationSeverity.Warning)));
                log.Information("Batch [{Name}]: {Status}", profile.Name, result.Success ? "OK" : "FAILED");
            }
            catch (Exception ex)
            {
                log.Error(ex, "Batch item {Name} threw", profile.Name);
                items.Add(new BatchItemResult(profile.Name, false, "", 1, 0));
            }
        }
        log.Information("Batch complete: {Ok}/{Total} succeeded", items.Count(i => i.Success), items.Count);
        return new BatchResult(items);
    }

    /// <summary>Loads every *.json profile in a file or directory.</summary>
    public static IReadOnlyList<FollowerProfile> LoadProfiles(string pathOrDir)
    {
        if (File.Exists(pathOrDir)) return [ProfileIo.Load(pathOrDir)];
        if (Directory.Exists(pathOrDir))
            return Directory.EnumerateFiles(pathOrDir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(ProfileIo.Load)
                .ToList();
        throw new FileNotFoundException($"No profile file or directory at {pathOrDir}");
    }

    private static bool WantsDefaultWhiterun(FollowerProfile p) =>
        p.Placement.Cell.FormKey == SkyrimRecords.VanillaForms.WhiterunWorldPersistentCell.ToString()
        || p.Placement.Cell.FormKey == SkyrimRecords.VanillaForms.WhiterunWorld.ToString();
}
