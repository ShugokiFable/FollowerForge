using FollowerForge.Domain;
using FollowerForge.FaceGen;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// The dirty swap rewrites one texture path and saves; everything the sculpt and the extended
/// (EFM) sliders produced lives in the vertices. If the save round-trip moved them at all, a
/// high-poly head would come out subtly softer than the face in RaceMenu — which is precisely
/// the kind of "looks a bit worse" nobody can point at. Run against real exports on this machine.
/// </summary>
public sealed class FaceGenRoundTripTests
{
    private static readonly string CharGenDir = Path.Combine(
        @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data",
        "SKSE", "Plugins", "CharGen");

    [Fact]
    public void SwappedHead_KeepsEveryVertexExactly()
    {
        var exports = Directory.Exists(CharGenDir)
            ? Directory.GetFiles(CharGenDir, "*.nif", SearchOption.TopDirectoryOnly)
            : [];
        if (exports.Length == 0) return;   // no RaceMenu exports on this machine

        var outDir = Path.Combine(Path.GetTempPath(), "ff_facegen_" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var source in exports)
            {
                var result = new FaceGenSwapper(new LoggerConfiguration().CreateLogger()).Swap(
                    new FaceGenSwapper.Request(
                        source, Path.ChangeExtension(source, ".dds"), "FF_RoundTrip.esp", 0x800,
                        outDir, "FF_RoundTrip", "000800:FF_RoundTrip.esp"));

                Assert.False(result.NeedsCreationKit, $"{Path.GetFileName(source)}: {
                    string.Join("; ", result.Findings.Select(f => f.Message))}");

                using var before = new NifHeadFile();
                using var after = new NifHeadFile();
                before.Load(source);
                after.Load(Path.Combine(outDir, result.FaceGeomPath!));

                var beforeShapes = before.Shapes();
                var afterShapes = after.Shapes();
                Assert.Equal(beforeShapes.Count, afterShapes.Count);

                for (var i = 0; i < beforeShapes.Count; i++)
                {
                    var original = before.Vertices(beforeShapes[i]);
                    var written = after.Vertices(afterShapes[i]);
                    Assert.Equal(original.Count, written.Count);
                    Assert.Equal(original, written);
                }
            }
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }
}
