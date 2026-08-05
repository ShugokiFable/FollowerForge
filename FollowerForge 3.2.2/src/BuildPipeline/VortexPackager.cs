using System.IO.Compression;
using System.Text.Json;
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

        // Stable archive order, with every entry stamped to this follower's actual build time.
        // An explicitly pinned BuildTimestampUnix still makes the package fully reproducible.
        var packageTime = ReadBuildTime(publishedDir);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var files = Directory.EnumerateFiles(publishedDir, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(publishedDir, file).Replace('\\', '/');
            var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
            entry.LastWriteTime = packageTime;
            using var src = File.OpenRead(file);
            using var dst = entry.Open();
            src.CopyTo(dst);
        }
        log.Information("Packaged {Mod} → {Zip} ({Count} files)", modName, zipPath, files.Count);
        return zipPath;
    }

    private static DateTimeOffset ReadBuildTime(string publishedDir)
    {
        try
        {
            var manifestPath = Path.Combine(publishedDir, "manifest.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("generatedAtUtc", out var camel)
                && DateTimeOffset.TryParse(camel.GetString(), out var parsedCamel))
                return ClampZipTime(parsedCamel);
            if (doc.RootElement.TryGetProperty("GeneratedAtUtc", out var pascal)
                && DateTimeOffset.TryParse(pascal.GetString(), out var parsedPascal))
                return ClampZipTime(parsedPascal);
        }
        catch (IOException) { }
        catch (JsonException) { }
        return ClampZipTime(DateTimeOffset.UtcNow);
    }

    private static DateTimeOffset ClampZipTime(DateTimeOffset value)
    {
        // ZIP stores a timezone-less DOS timestamp. Give it local wall-clock time so reading or
        // extracting in Explorer maps back to the same instant instead of shifting by UTC offset.
        var local = value.ToLocalTime();
        return local.Year < 1980
            ? new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : local;
    }
}
