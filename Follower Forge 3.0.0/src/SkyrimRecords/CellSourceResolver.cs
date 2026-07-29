using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Fetches the original definition of a cell from the plugin that owns it (usually Skyrim.esm),
/// so a placement override can reproduce the cell's own data instead of blanking it.
/// Reading the base version — not the winning one — keeps the follower's master list minimal.
/// </summary>
public sealed class CellSourceResolver(string gameDataPath, ILogger log) : IDisposable
{
    private readonly Dictionary<ModKey, ISkyrimModDisposableGetter> _open = new();

    public ICellGetter? Resolve(FormKey cellKey)
    {
        var mod = Open(cellKey.ModKey);
        if (mod is null) return null;

        foreach (var block in mod.Cells.Records)
            foreach (var sub in block.SubBlocks)
                foreach (var cell in sub.Cells)
                    if (cell.FormKey == cellKey) return cell;

        foreach (var ws in mod.Worldspaces)
        {
            if (ws.TopCell is { } top && top.FormKey == cellKey) return top;
            foreach (var block in ws.SubCells)
                foreach (var sub in block.Items)
                    foreach (var cell in sub.Items)
                        if (cell.FormKey == cellKey) return cell;
        }

        log.Warning("Cell {Key} not found in {Plugin}", cellKey, cellKey.ModKey.FileName);
        return null;
    }

    private ISkyrimModDisposableGetter? Open(ModKey modKey)
    {
        if (_open.TryGetValue(modKey, out var existing)) return existing;
        var path = Path.Combine(gameDataPath, modKey.FileName);
        if (!File.Exists(path))
        {
            log.Warning("Plugin not found while resolving a cell: {Path}", path);
            return null;
        }
        var mod = SkyrimMod.CreateFromBinaryOverlay(path, VanillaForms.Release);
        _open[modKey] = mod;
        return mod;
    }

    public void Dispose()
    {
        foreach (var m in _open.Values) m.Dispose();
        _open.Clear();
    }
}
