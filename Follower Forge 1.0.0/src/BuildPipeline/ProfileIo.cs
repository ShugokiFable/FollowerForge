using System.Text.Json;
using System.Text.Json.Serialization;
using FollowerForge.Domain;

namespace FollowerForge.BuildPipeline;

/// <summary>Load/save <see cref="FollowerProfile"/> JSON with readable enum names.</summary>
public static class ProfileIo
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static FollowerProfile Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FollowerProfile>(json, Options)
               ?? throw new InvalidDataException($"Empty or invalid profile: {path}");
    }

    public static void Save(string path, FollowerProfile profile) =>
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));
}
