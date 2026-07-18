using System.IO.Compression;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Zips a published follower folder into a single Vortex-installable archive. The plugin and any
/// FaceGen meshes/textures sit at the archive root (Data-relative), so Vortex deploys them
/// straight into the game's Data folder. No MO2 meta.ini is produced.
/// </summary>
public sealed class VortexPackager(ILogger log)
{
    /// <param name="publishedDir">A completed build folder (contains the ESP + manifests).</param>
    /// <returns>Path to the created .zip.</returns>
    public string Package(string publishedDir, string modName, string version = "1.0.0")
    {
        if (!Directory.Exists(publishedDir))
            throw new DirectoryNotFoundException(publishedDir);

        var safe = new string(modName.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        var zipPath = Path.Combine(
            Path.GetDirectoryName(publishedDir.TrimEnd(Path.DirectorySeparatorChar))!,
            $"{safe}-{version}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        // Deterministic archive: fixed entry order, fixed timestamps.
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var files = Directory.EnumerateFiles(publishedDir, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(publishedDir, file).Replace('\\', '/');
            var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var src = File.OpenRead(file);
            using var dst = entry.Open();
            src.CopyTo(dst);
        }
        log.Information("Packaged {Mod} → {Zip} ({Count} files)", modName, zipPath, files.Count);
        return zipPath;
    }
}
