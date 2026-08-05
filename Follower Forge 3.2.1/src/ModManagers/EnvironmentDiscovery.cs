using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Single entry point for read-only environment discovery.
/// Prefers Vortex when it is available. MO2 is only tried first when the user explicitly asks
/// (CLI --mo2-instance, FFORGE_PREFER_MO2=1, or FFORGE_MO2_INSTANCE).
/// SKYRIM_MO2_INSTANCE alone is not enough — houseCARL sets that to a Vortex shim that is not
/// a real MO2 install and can contain thousands of junctioned mods.
/// </summary>
public sealed class EnvironmentDiscovery(ILogger log)
{
    /// <param name="gameRootOverride">CLI --game-path.</param>
    /// <param name="mo2InstanceOverride">CLI --mo2-instance (explicit MO2 root).</param>
    /// <param name="preferMo2">When true, try MO2 before Vortex.</param>
    public EnvironmentSnapshot Discover(
        string? gameRootOverride = null,
        string? mo2InstanceOverride = null,
        bool preferMo2 = false)
    {
        Exception? vortexError = null;
        Exception? mo2Error = null;

        // Explicit MO2-only intent. Do NOT treat SKYRIM_MO2_INSTANCE as prefer-MO2 — that env var
        // is shared with houseCARL and often points at a Vortex shim, not the user's MO2.
        var explicitMo2 = preferMo2
            || !string.IsNullOrWhiteSpace(mo2InstanceOverride)
            || IsTruthy(Environment.GetEnvironmentVariable("FFORGE_PREFER_MO2"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE"));

        if (explicitMo2)
        {
            try
            {
                var mo2 = new Mo2Discovery(log).TryDiscover(mo2InstanceOverride, gameRootOverride);
                if (mo2 is not null) return mo2;
            }
            catch (Exception ex)
            {
                mo2Error = ex;
                log.Debug(ex, "MO2 discovery failed");
            }
        }

        try
        {
            return new VortexDiscovery(log).Discover(gameRootOverride);
        }
        catch (Exception ex)
        {
            vortexError = ex;
            log.Debug(ex, "Vortex discovery failed");
        }

        // Vortex unavailable — fall back to MO2 (including SKYRIM_MO2_INSTANCE for real MO2 users).
        try
        {
            var mo2 = new Mo2Discovery(log).TryDiscover(mo2InstanceOverride, gameRootOverride);
            if (mo2 is not null) return mo2;
        }
        catch (Exception ex)
        {
            mo2Error = ex;
        }

        throw new DirectoryNotFoundException(
            "Could not find a Vortex Skyrim SE deployment or a Mod Organizer 2 instance. "
            + "Install/deploy Vortex, or set FFORGE_MO2_INSTANCE to your MO2 instance folder "
            + "(the one with ModOrganizer.ini). Do not point Follower Forge at a houseCARL shim."
            + (vortexError is null ? "" : $" Vortex: {vortexError.Message}.")
            + (mo2Error is null ? "" : $" MO2: {mo2Error.Message}."));
    }

    private static bool IsTruthy(string? value) =>
        value is not null
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    /// <summary>Write guard covering game Data, staging/mods, and the manager instance tree.</summary>
    public static WriteGuard CreateGuard(EnvironmentSnapshot env) =>
        env.Manager == ModManagerKind.Mo2
            ? Mo2Discovery.CreateGuard(env)
            : VortexDiscovery.CreateGuard(env);
}
