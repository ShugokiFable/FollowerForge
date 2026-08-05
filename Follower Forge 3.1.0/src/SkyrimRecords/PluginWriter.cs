using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
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
    public void Write(
        SkyrimMod mod,
        string path,
        IReadOnlyList<ModKey>? masterOrder = null,
        string? masterRoot = null)
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

        if (masterRoot is null)
        {
            mod.WriteToBinary(path, parameters);
        }
        else
        {
            // Mutagen discovers direct references by walking the new plugin, but Skyrim plugins
            // also need the complete transitive master chain. Discover the direct list in a
            // private scratch file, expand every master's real installed TES4 header, then write
            // the final file while preserving that complete ordered list.
            var scratchDir = path + ".master-scan";
            var scratch = Path.Combine(scratchDir, Path.GetFileName(path));
            try
            {
                Directory.CreateDirectory(scratchDir);
                mod.WriteToBinary(scratch, parameters);
                using var directMod = SkyrimMod.CreateFromBinaryOverlay(scratch, VanillaForms.Release);
                var expanded = ExpandMasterChain(
                    directMod.ModHeader.MasterReferences.Select(m => m.Master),
                    masterRoot);
                var ordered = OrderMasters(expanded, masterOrder);

                mod.ModHeader.MasterReferences.Clear();
                foreach (var master in ordered)
                    mod.ModHeader.MasterReferences.Add(new MasterReference { Master = master });

                var finalParameters = parameters with
                {
                    MastersListContent = MastersListContentOption.NoCheck,
                };
                mod.WriteToBinary(path, finalParameters);
            }
            finally
            {
                if (Directory.Exists(scratchDir)) Directory.Delete(scratchDir, recursive: true);
            }
        }
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

    private static IReadOnlyCollection<ModKey> ExpandMasterChain(
        IEnumerable<ModKey> directMasters,
        string masterRoot)
    {
        var found = new HashSet<ModKey>();
        var pending = new Queue<ModKey>();
        foreach (var master in directMasters)
            if (found.Add(master)) pending.Enqueue(master);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var sourcePath = Path.Combine(masterRoot, current.FileName.String);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    $"Required master '{current.FileName}' is not installed in '{masterRoot}'.",
                    sourcePath);

            using var source = SkyrimMod.CreateFromBinaryOverlay(sourcePath, VanillaForms.Release);
            foreach (var parent in source.ModHeader.MasterReferences.Select(m => m.Master))
                if (found.Add(parent)) pending.Enqueue(parent);
        }
        return found;
    }

    private static IReadOnlyList<ModKey> OrderMasters(
        IReadOnlyCollection<ModKey> masters,
        IReadOnlyList<ModKey>? masterOrder)
    {
        var remaining = masters.ToHashSet();
        var ordered = new List<ModKey>(masters.Count);
        if (masterOrder is not null)
        {
            foreach (var candidate in masterOrder)
                if (remaining.Remove(candidate)) ordered.Add(candidate);
        }
        ordered.AddRange(remaining.OrderBy(m => m.FileName.String, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }
}
