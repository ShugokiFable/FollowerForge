using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Builds a shared follower hub plugin: a light master (.esm) carrying a single marker keyword
/// that every follower in the hub also carries, creating a genuine master dependency and a way
/// to identify hub members. The hub also ships shared assets (body/skin/hair) so its followers
/// do not each duplicate them.
/// </summary>
public sealed class HubCompiler(ILogger log)
{
    public const uint MarkerKeywordId = 0x800;

    public sealed record HubResult(SkyrimMod Mod, FormKey MarkerKeyword, string MarkerEditorId);

    public HubResult Compile(string hubPluginName)
    {
        var modKey = ModKey.FromFileName(hubPluginName);
        // Light master: followers can reliably master it (Small = ESL slot, Master = load-first).
        var mod = new SkyrimMod(modKey, VanillaForms.Release) { IsSmallMaster = true };
        mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Master;

        var baseName = new string(Path.GetFileNameWithoutExtension(hubPluginName)
            .Where(char.IsLetterOrDigit).ToArray());
        var kwd = mod.Keywords.AddNew($"FF_{baseName}_Member");
        // Keywords carry no colour for gameplay; the record just needs to exist and be masterable.

        log.Information("Compiled hub {Hub}: marker keyword {Kwd} ({Id})",
            hubPluginName, kwd.EditorID, kwd.FormKey.ID.ToString("X6"));
        return new HubResult(mod, kwd.FormKey, kwd.EditorID!);
    }
}
