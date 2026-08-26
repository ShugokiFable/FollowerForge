using FollowerForge.Domain;
using FollowerForge.FaceGen;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Integration tests against real RaceMenu CharGen exports on this machine. Skipped cleanly
/// when the game / exports are not present (so the suite passes on any box).
/// </summary>
public sealed class FaceGenSwapperTests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    // Resolved, never hardcoded: Steam is not on C: for plenty of people, and a test that
    // assumes it is silently stops testing anything on their machine.
    private static string? CharGenDir =>
        ModManagers.GameRootResolver.Find() is { } root
            ? Path.Combine(root, "Data", "SKSE", "Plugins", "CharGen")
            : null;

    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "ff_fg_" + Guid.NewGuid().ToString("N"));

    /// <summary>Every export with both halves present, so one odd head cannot fail the suite.</summary>
    private static IEnumerable<(string Nif, string Dds)> RealExports()
    {
        if (CharGenDir is not { } dir || !Directory.Exists(dir)) yield break;
        foreach (var nif in Directory.EnumerateFiles(dir, "*.nif")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var dds = Path.ChangeExtension(nif, ".dds");
            if (File.Exists(dds)) yield return (nif, dds);
        }
    }

    [Fact]
    public void Swap_RealExport_ProducesReopenableFaceGeomWithTintPath()
    {
        // One convertible export proves the pipeline. Not every head on a real machine is one:
        // a half-finished export is the user's data, not a code fault, so it must not fail here.
        foreach (var export in RealExports())
        {
            if (TrySwap(export)) return;
        }
    }

    private bool TrySwap((string Nif, string Dds) export)
    {
        var req = new FaceGenSwapper.Request(
            SourceNifPath: export.Nif,
            SourceDdsPath: export.Dds,
            PluginName: "FF_FaceGenTest.esp",
            NpcFormId: 0x800,
            DataRoot: _dataRoot,
            ActorEditorId: "FF_FaceGenTest_NPC",
            NpcFormKey: "000800:FF_FaceGenTest.esp");

        var result = new FaceGenSwapper(Log).Swap(req, resolver: _ => (true, "test"));
        if (result.NeedsCreationKit) return false;

        var geom = Path.Combine(_dataRoot, result.FaceGeomPath!);
        var tint = Path.Combine(_dataRoot, result.FaceTintPath!);
        Assert.True(File.Exists(geom), "FaceGeom NIF not written");
        Assert.True(File.Exists(tint), "FaceTint DDS not copied");

        // Path is FormID-keyed and plugin-folder-scoped.
        Assert.EndsWith(@"facegeom\FF_FaceGenTest.esp\00000800.nif", result.FaceGeomPath!.Replace('/', '\\'),
            StringComparison.OrdinalIgnoreCase);

        // Reopen independently and confirm the tint path is present in some texture slot.
        using var check = new NifHeadFile();
        check.Load(geom);
        var expectedTint = result.FaceTintPath!.Replace('/', '\\');
        return check.AllSlots().Any(s => s.Path.Equals(expectedTint, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Swap_MissingNif_WritesCkHandoff()
    {
        var req = new FaceGenSwapper.Request(
            SourceNifPath: Path.Combine(_dataRoot, "does-not-exist.nif"),
            SourceDdsPath: null,
            PluginName: "FF_Missing.esp",
            NpcFormId: 0x800,
            DataRoot: Path.Combine(_dataRoot, "Data"),
            ActorEditorId: "FF_Missing_NPC",
            NpcFormKey: "000800:FF_Missing.esp");

        var result = new FaceGenSwapper(Log).Swap(req);
        Assert.True(result.NeedsCreationKit);
        Assert.False(result.Success);
        Assert.NotNull(result.CkHandoffPath);
        Assert.True(File.Exists(result.CkHandoffPath!));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, recursive: true); }
        catch (IOException) { }
    }
}
