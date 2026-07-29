using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Serilog;

namespace FollowerForge.AssetIndex;

/// <summary>Indexes loose files from the Vortex deployment manifest (provenance included).</summary>
public sealed class LooseFileIndexer(ILogger log)
{
    /// <summary>File extensions that matter for follower builds; everything else is noise.</summary>
    private static readonly HashSet<string> RelevantExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nif", ".dds", ".tri", ".hkx", ".fuz", ".wav", ".xwm", ".lip", ".seq",
        ".esp", ".esm", ".esl", ".bsa", ".jslot", ".json", ".xml", ".pex",
    };

    public IEnumerable<AssetFile> Enumerate(string manifestPath)
    {
        long total = 0, kept = 0;
        foreach (var entry in DeploymentManifest.ReadFiles(manifestPath))
        {
            total++;
            var ext = Path.GetExtension(entry.RelPath);
            if (!RelevantExtensions.Contains(ext)) continue;
            kept++;
            yield return new AssetFile
            {
                RelPath = CatalogDb.NormalizeRelPath(entry.RelPath),
                Container = AssetContainerKind.Loose,
                ContainerName = entry.SourceMod,
                SourceMod = entry.SourceMod,
                Size = 0, // size not stored in the manifest; existence is what matters
            };
        }
        log.Information("Loose-file index: kept {Kept} of {Total} deployed files", kept, total);
    }
}

/// <summary>Indexes the contents of every BSA in the game Data folder.</summary>
public sealed class ArchiveIndexer(ILogger log)
{
    public IEnumerable<AssetFile> Enumerate(string gameDataPath)
    {
        var bsaFiles = Directory.EnumerateFiles(gameDataPath, "*.bsa", SearchOption.TopDirectoryOnly).ToList();
        log.Information("Archive index: reading {Count} BSAs…", bsaFiles.Count);
        var done = 0;
        foreach (var bsaPath in bsaFiles)
        {
            var bsaName = Path.GetFileName(bsaPath);
            IArchiveReader reader;
            try
            {
                reader = Archive.CreateReader(GameRelease.SkyrimSE, bsaPath);
            }
            catch (Exception ex)
            {
                log.Warning("Unreadable BSA {Bsa}: {Error}", bsaName, ex.Message);
                continue;
            }

            foreach (var file in reader.Files)
            {
                string relPath;
                long size;
                try
                {
                    relPath = file.Path;
                    size = (long)file.Size;
                }
                catch (Exception ex)
                {
                    log.Warning("Bad entry in {Bsa}: {Error}", bsaName, ex.Message);
                    continue;
                }
                yield return new AssetFile
                {
                    RelPath = CatalogDb.NormalizeRelPath(relPath),
                    Container = AssetContainerKind.Bsa,
                    ContainerName = bsaName,
                    SourceMod = null,
                    Size = size,
                };
            }
            done++;
            if (done % 100 == 0) log.Information("  …{Done}/{Total} BSAs", done, bsaFiles.Count);
        }
    }
}
