using System.IO.Compression;
using FollowerForge.BuildPipeline;
using Serilog;

namespace FollowerForge.Tests;

public sealed class TimestampTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_time_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void VortexArchiveUsesTheFollowerBuildTimeInsteadOfA2020Placeholder()
    {
        var published = Path.Combine(_root, "builds", "Timestamp Follower");
        Directory.CreateDirectory(published);
        File.WriteAllText(Path.Combine(published, "FF_Timestamp.esp"), "fixture");
        File.WriteAllText(Path.Combine(published, "manifest.json"),
            """{"GeneratedAtUtc":"2026-07-26T12:34:56.0000000Z"}""");

        var zipPath = new VortexPackager(new LoggerConfiguration().CreateLogger())
            .Package(published, "FF_Timestamp", "2.1.1");

        using var zip = ZipFile.OpenRead(zipPath);
        Assert.All(zip.Entries, entry =>
            Assert.Equal(new DateTimeOffset(2026, 7, 26, 12, 34, 56, TimeSpan.Zero),
                entry.LastWriteTime.ToUniversalTime()));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException) { }
    }
}
