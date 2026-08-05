using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Single entry point for read-only environment discovery. Prefers Vortex when a deployed
/// Skyrim SE Vortex game folder exists; otherwise tries Mod Organizer 2.
/// </summary>
public sealed class EnvironmentDiscovery(ILogger log)
{
    /// <param name="gameRootOverride">CLI --game-path.</param>
    /// <param name="mo2InstanceOverride">CLI --mo2-instance.</param>
    /// <param name="preferMo2">When true, try MO2 before Vortex (testing / dual installs).</param>
    public EnvironmentSnapshot Discover(
        string? gameRootOverride = null,
        string? mo2InstanceOverride = null,
        bool preferMo2 = false)
    {
        Exception? vortexError = null;
        Exception? mo2Error = null;

        if (preferMo2 || !string.IsNullOrWhiteSpace(mo2InstanceOverride)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SKYRIM_MO2_INSTANCE")))
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

        // Vortex failed — last chance MO2 without prefer flag.
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
            + "Install/deploy Vortex, or set FFORGE_MO2_INSTANCE / SKYRIM_MO2_INSTANCE to your MO2 "
            + "instance folder (the one with ModOrganizer.ini)."
            + (vortexError is null ? "" : $" Vortex: {vortexError.Message}.")
            + (mo2Error is null ? "" : $" MO2: {mo2Error.Message}."));
    }

    /// <summary>Write guard covering game Data, staging/mods, and the manager instance tree.</summary>
    public static WriteGuard CreateGuard(EnvironmentSnapshot env) =>
        env.Manager == ModManagerKind.Mo2
            ? Mo2Discovery.CreateGuard(env)
            : VortexDiscovery.CreateGuard(env);
}
