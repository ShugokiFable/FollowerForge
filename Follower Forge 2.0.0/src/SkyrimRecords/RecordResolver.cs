using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Opens individual source plugins read-only to fetch a specific record getter (e.g. a combat
/// style to clone). Disposable: keeps the overlays alive until the caller is done reading.
/// </summary>
public sealed class RecordResolver(string gameDataPath, ILogger log) : IDisposable
{
    private readonly Dictionary<ModKey, ISkyrimModDisposableGetter> _open = new();

    /// <summary>Resolves a combat style by its FormKey string ("123ABC:Plugin.esp").</summary>
    public ICombatStyleGetter? ResolveCombatStyle(string formKeyString)
    {
        var formKey = FormKey.Factory(formKeyString);
        var mod = Open(formKey.ModKey);
        if (mod is null) return null;
        var match = mod.CombatStyles.FirstOrDefault(c => c.FormKey == formKey);
        if (match is null)
            log.Warning("Combat style {Key} not found in {Plugin}", formKeyString, formKey.ModKey.FileName);
        return match;
    }

    private ISkyrimModDisposableGetter? Open(ModKey modKey)
    {
        if (_open.TryGetValue(modKey, out var existing)) return existing;
        var path = Path.Combine(gameDataPath, modKey.FileName);
        if (!File.Exists(path))
        {
            log.Warning("Source plugin not found for resolve: {Path}", path);
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
