using FollowerForge.Domain;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Places a reference into any cell using the pattern shipped follower mods actually use:
/// a cell override that reproduces the cell's own data and adds ONE persistent reference.
/// Verified against Z_Iseult.esp (adds itself to Frostfruit Inn): its CELL overrides are
/// zlib-compressed and carry the full record — EDID, FULL, DATA, XCLL, LTMP, XCLW, XLCN…
/// Copying that data keeps the room's lighting intact; excluding the child groups keeps the
/// room's existing contents from being duplicated.
/// </summary>
public static class CellPlacer
{
    /// <summary>
    /// Interior cells are grouped by their own FormID: block = id % 10, sub-block = (id / 10) % 10.
    /// Verified against Skyrim.esm cells 0x013870 (4/8), 0x015239 (5/8) and 0x0152AA (8/9).
    /// </summary>
    public static (sbyte Block, sbyte SubBlock) InteriorBlocks(uint formId) =>
        ((sbyte)(formId % 10), (sbyte)(formId / 10 % 10));

    /// <summary>Adds <paramref name="placed"/> to an interior cell, creating the block structure.</summary>
    public static void PlaceInInterior(SkyrimMod mod, FormKey cellKey, IPlaced placed, ICellGetter? source = null)
    {
        var (blockNo, subBlockNo) = InteriorBlocks(cellKey.ID);

        var block = mod.Cells.Records.FirstOrDefault(b => b.BlockNumber == blockNo);
        if (block is null)
        {
            block = new CellBlock { BlockNumber = blockNo, GroupType = GroupTypeEnum.InteriorCellBlock };
            mod.Cells.Records.Add(block);
        }

        var subBlock = block.SubBlocks.FirstOrDefault(s => s.BlockNumber == subBlockNo);
        if (subBlock is null)
        {
            subBlock = new CellSubBlock { BlockNumber = subBlockNo, GroupType = GroupTypeEnum.InteriorCellSubBlock };
            block.SubBlocks.Add(subBlock);
        }

        var cell = subBlock.Cells.FirstOrDefault(c => c.FormKey == cellKey);
        if (cell is null)
        {
            cell = CopyOfCell(cellKey, source);
            subBlock.Cells.Add(cell);
        }
        cell.Persistent.Add(placed);
    }

    /// <summary>
    /// Reproduces the cell's own data (flags, lighting, water, location…) but NOT its contents.
    /// An override that omitted this data would blank the room's lighting; an override that
    /// copied the children would duplicate every object already in it.
    /// </summary>
    private static Cell CopyOfCell(FormKey cellKey, ICellGetter? source)
    {
        if (source is null)
        {
            var blank = new Cell(cellKey, VanillaForms.Release) { FormVersion = 44 };
            return blank;
        }
        // DeepCopy preserves the source cell's FormVersion (often 40 from Skyrim.esm). The
        // ship-gate requires every record in a new SE/AE plugin to be formVersion 44.
        var copy = source.DeepCopy(new Cell.TranslationMask(defaultOn: true)
        {
            Persistent = false,
            Temporary = false,
            Landscape = false,
            NavigationMeshes = false,
        });
        copy.FormVersion = 44;
        return copy;
    }

    /// <summary>
    /// Adds <paramref name="placed"/> to a worldspace's persistent cell — where a follower's
    /// reference must live so it exists before the exterior cell is loaded.
    /// </summary>
    public static void PlaceInWorldspace(SkyrimMod mod, FormKey worldspaceKey, FormKey persistentCellKey, IPlaced placed, ICellGetter? source = null)
    {
        var worldspace = mod.Worldspaces.FirstOrDefault(w => w.FormKey == worldspaceKey);
        if (worldspace is null)
        {
            worldspace = new Worldspace(worldspaceKey, VanillaForms.Release);
            mod.Worldspaces.Add(worldspace);
        }
        worldspace.TopCell ??= CopyOfCell(persistentCellKey, source);
        worldspace.TopCell.Persistent.Add(placed);
    }

    /// <summary>Routes a library location to the correct placement strategy.</summary>
    public static void Place(SkyrimMod mod, SpawnLocation location, IPlaced placed, ICellGetter? source = null)
    {
        var cellKey = FormKey.Factory(location.CellFormKey);
        if (location.Kind == LocationKind.Interior)
        {
            PlaceInInterior(mod, cellKey, placed, source);
            return;
        }
        if (location.WorldspaceFormKey is not { } wsText)
            throw new InvalidOperationException($"Exterior location '{location.Id}' has no worldspace.");
        PlaceInWorldspace(mod, FormKey.Factory(wsText), cellKey, placed, source);
    }
}
