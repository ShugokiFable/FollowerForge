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
    private const string CharGenDir =
        @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data\SKSE\Plugins\CharGen";

    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "ff_fg_" + Guid.NewGuid().ToString("N"));

    private static (string Nif, string? Dds)? FirstRealExport()
    {
        if (!Directory.Exists(CharGenDir)) return null;
        var nif = Directory.EnumerateFiles(CharGenDir, "*.nif").FirstOrDefault();
        if (nif is null) return null;
        var dds = Path.ChangeExtension(nif, ".dds");
        return (nif, File.Exists(dds) ? dds : null);
    }

    [Fact]
    public void Swap_RealExport_ProducesReopenableFaceGeomWithTintPath()
    {
        var export = FirstRealExport();
        if (export is null) return; // no game/exports on this machine — skip cleanly

        var req = new FaceGenSwapper.Request(
            SourceNifPath: export.Value.Nif,
            SourceDdsPath: export.Value.Dds,
            PluginName: "FF_FaceGenTest.esp",
            NpcFormId: 0x800,
            DataRoot: _dataRoot,
            ActorEditorId: "FF_FaceGenTest_NPC",
            NpcFormKey: "000800:FF_FaceGenTest.esp");

        var result = new FaceGenSwapper(Log).Swap(req, resolver: _ => (true, "test"));

        // A real head export should convert (not need CK).
        Assert.False(result.NeedsCreationKit,
            "Real export unexpectedly needed CK: " + string.Join("; ", result.Findings.Select(f => f.Message)));

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
        Assert.Contains(check.AllSlots(), s =>
            s.Path.Equals(expectedTint, StringComparison.OrdinalIgnoreCase));
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
