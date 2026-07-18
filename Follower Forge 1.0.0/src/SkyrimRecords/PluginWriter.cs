using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>Writes a SkyrimMod to disk and reopens it for verification (never touches game files).</summary>
public sealed class PluginWriter(ILogger log)
{
    /// <param name="masterOrder">
    /// Full load order used to sort the master list correctly. When null, Mutagen orders masters
    /// by its own heuristic (fine for Skyrim.esm-only plugins).
    /// </param>
    public void Write(SkyrimMod mod, string path, IReadOnlyList<ModKey>? masterOrder = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Default parameters iterate content to build the master list; IsSmallMaster keeps FormIDs
        // in the light range. Provide the load order so masters are sorted consistently.
        var parameters = new BinaryWriteParameters
        {
            MastersListContent = MastersListContentOption.Iterate,
        };
        if (masterOrder is not null)
            parameters = parameters with { MastersListOrdering = new MastersListOrderingByLoadOrder(masterOrder) };

        mod.WriteToBinary(path, parameters);
        // Mutagen's HEDR count differs from the CK convention (records + GRUPs); patch it so the
        // ship-gate and Creation Kit agree on the form count.
        var fixedCount = HeaderFixer.FixRecordCount(path);
        log.Information("Wrote plugin {Path} ({Size} bytes, HEDR numRecords={Count})",
            path, new FileInfo(path).Length, fixedCount);
    }

    /// <summary>Reopens a written plugin and returns basic facts (proves it parses).</summary>
    public ReopenResult Reopen(string path)
    {
        // Dispose the overlay: it memory-maps the file and would keep a handle open,
        // blocking the atomic publish move.
        using var mod = SkyrimMod.CreateFromBinaryOverlay(path, VanillaForms.Release);
        var npcCount = mod.Npcs.Count;
        var masters = mod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String).ToList();
        var isLight = mod.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Small);
        return new ReopenResult(npcCount, masters, isLight, mod.ModHeader.Stats.Version);
    }

    public sealed record ReopenResult(int NpcCount, IReadOnlyList<string> Masters, bool IsLight, float HeaderVersion);
}
