using System.Security.Cryptography;
using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Proves a profile rebuilds deterministically: builds it twice into separate temp workspaces
/// and byte-compares the generated plugin. Used by tests and the CLI --verify-deterministic flag.
/// </summary>
public sealed class DeterminismVerifier(ILogger log)
{
    public sealed record Result(bool Identical, string HashA, string HashB, string PluginName);

    public Result Verify(FollowerProfile profile, EnvironmentSnapshot env,
        IWorldspaceGetter? placementWorldspace, CatalogDb? catalog = null)
    {
        var builder = new FollowerBuilder(log);
        var root = Path.Combine(Path.GetTempPath(), "ff_determinism_" + Guid.NewGuid().ToString("N"));
        var a = Path.Combine(root, "a");
        var b = Path.Combine(root, "b");
        try
        {
            var ra = builder.Build(profile, env, a, placementWorldspace, catalog);
            var rb = builder.Build(profile, env, b, placementWorldspace, catalog);
            var ha = Sha256(ra.PluginPath);
            var hb = Sha256(rb.PluginPath);
            var identical = ha == hb;
            log.Information("Determinism for {Name}: {Result} ({Hash})",
                profile.Name, identical ? "IDENTICAL" : "DIFFERENT", ha[..12]);
            return new Result(identical, ha, hb, profile.PluginName);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }
    }

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
