using Newtonsoft.Json;

namespace FollowerForge.ModManagers;

public sealed record DeploymentManifestHeader(
    string? DeploymentMethod,
    string? GameId,
    string? StagingPath,
    string? TargetPath,
    long DeploymentTimeUtcMs);

public sealed record DeploymentFileEntry(string RelPath, string SourceMod, long TimeUtcMs);

/// <summary>
/// Streaming reader for Vortex's <c>vortex.deployment.json</c>. The live file is ~88 MB,
/// so it is never loaded whole; header fields are read up front and file entries are
/// yielded one at a time.
/// </summary>
public static class DeploymentManifest
{
    public const string FileName = "vortex.deployment.json";

    public static string? Locate(string gameDataPath)
    {
        // Ignore vortex.deployment.json.XXXX.tmp leftovers — only the exact name counts.
        var path = Path.Combine(gameDataPath, FileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Reads only the scalar header fields (stops at the "files" array).</summary>
    public static DeploymentManifestHeader ReadHeader(string manifestPath)
    {
        using var reader = OpenReader(manifestPath);
        string? method = null, gameId = null, staging = null, target = null;
        long time = 0;

        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.PropertyName) continue;
            var name = (string)reader.Value!;
            switch (name)
            {
                case "deploymentMethod": method = reader.ReadAsString(); break;
                case "gameId": gameId = reader.ReadAsString(); break;
                case "stagingPath": staging = reader.ReadAsString(); break;
                case "targetPath": target = reader.ReadAsString(); break;
                case "deploymentTime": time = (long)(reader.ReadAsDouble() ?? 0); break;
                case "files": return new DeploymentManifestHeader(method, gameId, staging, target, time);
                default: reader.Skip(); break;
            }
        }
        return new DeploymentManifestHeader(method, gameId, staging, target, time);
    }

    /// <summary>Streams every deployed-file entry (relPath + source mod folder).</summary>
    public static IEnumerable<DeploymentFileEntry> ReadFiles(string manifestPath)
    {
        using var reader = OpenReader(manifestPath);

        // Advance to the "files" array.
        var inFiles = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value! == "files")
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartArray)
                    throw new InvalidDataException($"'files' is not an array in {manifestPath}");
                inFiles = true;
                break;
            }
        }
        if (!inFiles) yield break;

        while (reader.Read() && reader.TokenType == JsonToken.StartObject)
        {
            string? relPath = null, source = null;
            long time = 0;
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName) continue;
                switch ((string)reader.Value!)
                {
                    case "relPath": relPath = reader.ReadAsString(); break;
                    case "source": source = reader.ReadAsString(); break;
                    case "time": time = (long)(reader.ReadAsDouble() ?? 0); break;
                    default: reader.Skip(); break;
                }
            }
            if (!string.IsNullOrEmpty(relPath) && source is not null)
                yield return new DeploymentFileEntry(relPath, source, time);
        }
    }

    private static JsonTextReader OpenReader(string manifestPath)
    {
        var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 1 << 16, FileOptions.SequentialScan);
        return new JsonTextReader(new StreamReader(stream)) { CloseInput = true };
    }
}
