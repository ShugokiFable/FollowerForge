using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Harvests spawn points from the installed mod library: every plugin that places its OWN NPC
/// somewhere is evidence that the spot works (accessible, on navmesh, not inside geometry).
/// Base-game masters are skipped — they would flood the library with thousands of vanilla NPCs.
/// </summary>
public sealed class LocationScanner(ILogger log)
{
    private sealed record RawPlacement(
        FormKey Cell, string? CellName, string? CellEditorId, FormKey? Worldspace,
        float X, float Y, float Z, float RotZ, string Plugin, LocationKind Kind, FormKey? CellLocation,
        string? WorldspaceName = null, (short X, short Y)? Grid = null);

    /// <param name="cellNameLookup">FormKey → winning display name (from the catalogue), for cells whose override drops the name.</param>
    /// <param name="pluginSourceMods">Plugin file name → Vortex staging mod folder.</param>
    public IReadOnlyList<SpawnLocation> Scan(
        LoadOrderBuilder.BuiltLoadOrder loadOrder,
        Func<string, string?>? cellNameLookup = null,
        IReadOnlyDictionary<string, string>? pluginSourceMods = null)
    {
        var raw = new List<RawPlacement>();
        var scanned = 0;

        foreach (var listing in loadOrder.LoadOrder.PriorityOrder)
        {
            var mod = listing.Mod;
            if (mod is null) continue;
            var pluginName = mod.ModKey.FileName.String;
            if (IsBaseGame(pluginName)) continue;
            scanned++;

            try
            {
                CollectInteriors(mod, pluginName, raw);
                CollectExteriors(mod, pluginName, raw);
            }
            catch (Exception ex)
            {
                // A malformed plugin must never abort the whole scan.
                log.Warning("Location scan skipped {Plugin}: {Error}", pluginName, ex.Message);
            }
        }

        log.Information("Scanned {Count} plugins, found {Raw} custom-NPC placements", scanned, raw.Count);
        return Aggregate(raw, cellNameLookup, pluginSourceMods);
    }

    private static void CollectInteriors(ISkyrimModGetter mod, string pluginName, List<RawPlacement> raw)
    {
        foreach (var block in mod.Cells.Records)
            foreach (var sub in block.SubBlocks)
                foreach (var cell in sub.Cells)
                    foreach (var placed in cell.Persistent.Concat(cell.Temporary).OfType<IPlacedNpcGetter>())
                    {
                        if (!IsOwnNpc(placed, mod.ModKey)) continue;
                        var p = placed.Placement;
                        raw.Add(new RawPlacement(
                            cell.FormKey, cell.Name?.String, cell.EditorID, null,
                            p?.Position.X ?? 0, p?.Position.Y ?? 0, p?.Position.Z ?? 0, p?.Rotation.Z ?? 0,
                            pluginName, LocationKind.Interior,
                            cell.Location.IsNull ? null : cell.Location.FormKey));
                    }
    }

    private static void CollectExteriors(ISkyrimModGetter mod, string pluginName, List<RawPlacement> raw)
    {
        foreach (var ws in mod.Worldspaces)
        {
            // The worldspace override often keeps its name; fall back to a readable EditorID.
            var wsName = FirstNonEmpty(ws.Name?.String, Humanize(ws.EditorID));

            // Persistent refs live in the worldspace's persistent (top) cell...
            if (ws.TopCell is { } top)
                AddFrom(top, ws.FormKey, wsName);
            // ...but many follower mods place into an ordinary exterior subcell instead.
            foreach (var block in ws.SubCells)
                foreach (var sub in block.Items)
                    foreach (var cell in sub.Items)
                        AddFrom(cell, ws.FormKey, wsName);

            void AddFrom(ICellGetter cell, FormKey worldspace, string? worldspaceName)
            {
                foreach (var placed in cell.Persistent.Concat(cell.Temporary).OfType<IPlacedNpcGetter>())
                {
                    if (!IsOwnNpc(placed, mod.ModKey)) continue;
                    var p = placed.Placement;
                    var grid = cell.Grid?.Point is { } pt ? ((short)pt.X, (short)pt.Y) : ((short, short)?)null;
                    raw.Add(new RawPlacement(
                        cell.FormKey, cell.Name?.String, cell.EditorID, worldspace,
                        p?.Position.X ?? 0, p?.Position.Y ?? 0, p?.Position.Z ?? 0, p?.Rotation.Z ?? 0,
                        pluginName, LocationKind.Exterior,
                        cell.Location.IsNull ? null : cell.Location.FormKey,
                        worldspaceName, grid));
                }
            }
        }
    }

    /// <summary>True when the placed actor's base NPC is defined by this same plugin (a custom NPC).</summary>
    private static bool IsOwnNpc(IPlacedNpcGetter placed, ModKey modKey) =>
        !placed.Base.IsNull && placed.Base.FormKey.ModKey == modKey;

    private IReadOnlyList<SpawnLocation> Aggregate(
        List<RawPlacement> raw, Func<string, string?>? cellNameLookup,
        IReadOnlyDictionary<string, string>? pluginSourceMods)
    {
        var result = new List<SpawnLocation>();

        foreach (var group in raw.GroupBy(r => r.Cell))
        {
            var first = group.First();
            var plugins = group.Select(g => g.Plugin).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Representative spot: the placement closest to the group's centre, so a single
            // outlier (someone hiding an NPC in a corner) never becomes the default.
            var cx = group.Average(g => g.X);
            var cy = group.Average(g => g.Y);
            var rep = group.OrderBy(g => (g.X - cx) * (g.X - cx) + (g.Y - cy) * (g.Y - cy)).First();

            var cellKey = first.Cell.ToString();
            var wsName = group.Select(g => g.WorldspaceName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            var locationName = first.CellLocation is { } lk ? cellNameLookup?.Invoke(lk.ToString()) : null;

            var name = FirstNonEmpty(
                group.Select(g => g.CellName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                cellNameLookup?.Invoke(cellKey),
                Humanize(first.CellEditorId),
                // Nameless exterior: identify it by its location/worldspace and map grid rather
                // than calling every outdoor cell "Wilderness".
                first.Kind == LocationKind.Exterior ? ExteriorName(locationName, wsName, first.Grid) : null,
                "Unnamed interior")!;

            var area = FirstNonEmpty(locationName, wsName, ResolveArea(first, cellNameLookup));

            result.Add(new SpawnLocation
            {
                Id = Slug(name, area, first.Cell),
                Name = name,
                Area = area,
                Kind = first.Kind,
                CellFormKey = cellKey,
                WorldspaceFormKey = first.Worldspace?.ToString(),
                CellEditorId = first.CellEditorId,
                X = rep.X, Y = rep.Y, Z = rep.Z, RotationZ = rep.RotZ,
                GridX = first.Grid?.X, GridY = first.Grid?.Y,
                RequiredPlugin = first.Cell.ModKey.FileName.String,
                ProvenByPlugins = plugins,
                ProvenByMods = plugins
                    .Select(p => pluginSourceMods is not null && pluginSourceMods.TryGetValue(p, out var m) ? m : null)
                    .Where(m => m is not null).Select(m => m!).Distinct().ToList(),
                Popularity = plugins.Count,
            });
        }

        var ordered = result
            .OrderByDescending(l => l.Popularity)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        log.Information("Location library: {Count} distinct spots ({Interior} interior, {Exterior} exterior)",
            ordered.Count, ordered.Count(l => l.Kind == LocationKind.Interior), ordered.Count(l => l.Kind == LocationKind.Exterior));
        return ordered;
    }

    private static string? ResolveArea(RawPlacement p, Func<string, string?>? lookup)
    {
        if (p.CellLocation is { } loc && lookup?.Invoke(loc.ToString()) is { Length: > 0 } locName)
            return locName;
        if (p.Worldspace is { } ws && lookup?.Invoke(ws.ToString()) is { Length: > 0 } wsName)
            return wsName;
        return null;
    }

    /// <summary>Readable identity for an outdoor cell that carries no name of its own.</summary>
    private static string ExteriorName(string? locationName, string? worldspaceName, (short X, short Y)? grid)
    {
        var place = FirstNonEmpty(locationName, worldspaceName) ?? "Wilderness";
        // The persistent cell of a worldspace has no grid square; everything else does.
        return grid is { } g ? $"{place} — outdoors ({g.X}, {g.Y})" : $"{place} — outdoors";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>"RoriksteadFrostfruitInn" → "Rorikstead Frostfruit Inn" (last-resort naming).</summary>
    private static string? Humanize(string? editorId)
    {
        if (string.IsNullOrWhiteSpace(editorId)) return null;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < editorId.Length; i++)
        {
            if (i > 0 && char.IsUpper(editorId[i]) && !char.IsUpper(editorId[i - 1])) sb.Append(' ');
            sb.Append(editorId[i]);
        }
        return sb.ToString();
    }

    private static string Slug(string name, string? area, FormKey cell)
    {
        // Skip the area prefix when it just repeats the place name ("The Bannered Mare" in the
        // location "The Bannered Mare") so ids stay readable.
        var redundant = string.IsNullOrWhiteSpace(area)
            || name.Contains(area, StringComparison.OrdinalIgnoreCase)
            || area.Contains(name, StringComparison.OrdinalIgnoreCase);
        var basis = redundant ? name : $"{area}-{name}";
        var chars = basis.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length == 0) slug = "location";
        // Cell id keeps ids unique when two places share a name (e.g. several "Bandit Camp").
        return $"{slug}-{cell.ID:x6}";
    }

    private static bool IsBaseGame(string plugin) =>
        PluginLists.ImplicitBaseMasters.Contains(plugin, StringComparer.OrdinalIgnoreCase)
        || plugin.StartsWith("cc", StringComparison.OrdinalIgnoreCase)
        || plugin.Equals("_ResourcePack.esl", StringComparison.OrdinalIgnoreCase);
}
