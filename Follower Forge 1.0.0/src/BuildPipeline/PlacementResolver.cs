using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Resolves the worldspace getter used for exterior placement. For the default Whiterun
/// placement this opens Skyrim.esm read-only; modded worldspaces come from the full load order.
/// </summary>
public sealed class PlacementResolver(ILogger log) : IDisposable
{
    private ISkyrimModDisposableGetter? _skyrimEsm;

    /// <summary>Returns WhiterunWorld from Skyrim.esm, or null if the profile targets a custom cell.</summary>
    public IWorldspaceGetter? ResolveDefaultWorldspace(EnvironmentSnapshot env, FollowerProfile profile)
    {
        var cell = profile.Placement.Cell.FormKey;
        var wantsWhiterun = cell == VanillaForms.WhiterunWorldPersistentCell.ToString()
                            || cell == VanillaForms.WhiterunWorld.ToString();
        if (!wantsWhiterun) return null;

        var esmPath = Path.Combine(env.GameDataPath, "Skyrim.esm");
        if (!File.Exists(esmPath))
        {
            log.Warning("Skyrim.esm not found for placement; using stub worldspace override.");
            return null;
        }
        _skyrimEsm = SkyrimMod.CreateFromBinaryOverlay(esmPath, VanillaForms.Release);
        var ws = _skyrimEsm.Worldspaces.FirstOrDefault(w => w.EditorID == "WhiterunWorld");
        if (ws is null) log.Warning("WhiterunWorld not found in Skyrim.esm; using stub override.");
        return ws;
    }

    public void Dispose() => _skyrimEsm?.Dispose();
}
