using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.ModManagers;

/// <summary>
/// Builds a FollowerForge-owned directory of hardlinks (copy fallback) to the enabled plugins
/// resolved from an MO2 instance, so Mutagen can open a single Data-like folder without writing
/// into the game or the MO2 instance.
/// </summary>
public sealed class Mo2DataView(ILogger log)
{
    public static string ViewRootFor(string instancePath, string profileId)
    {
        var safeInstance = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(instancePath))))[..12];
        var safeProfile = new string(profileId.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (safeProfile.Length == 0) safeProfile = "profile";
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FollowerForge", "mo2-view", $"{safeInstance}_{safeProfile}");
    }

    /// <summary>
    /// Ensures <see cref="EnvironmentSnapshot.PluginDataPath"/> contains hardlinks for every
    /// enabled load-order plugin that can be resolved. Returns how many plugins were linked.
    /// </summary>
    public int Ensure(EnvironmentSnapshot env, IReadOnlyList<LoadOrderEntry> entries)
    {
        if (env.Manager != ModManagerKind.Mo2) return 0;

        var view = env.PluginDataPath;
        Directory.CreateDirectory(view);

        // Drop stale plugin links so a disabled mod does not keep winning.
        foreach (var existing in Directory.EnumerateFiles(view, "*.*", SearchOption.TopDirectoryOnly))
        {
            var ext = Path.GetExtension(existing);
            if (ext is ".esp" or ".esm" or ".esl" or ".bsa")
            {
                try { File.Delete(existing); } catch (IOException) { /* locked; overwrite below */ }
            }
        }

        var linked = 0;
        var missing = new List<string>();
        foreach (var entry in entries.Where(e => e.Enabled))
        {
            var src = Mo2PathResolver.ResolvePlugin(env, entry.PluginFileName);
            if (src is null)
            {
                missing.Add(entry.PluginFileName);
                continue;
            }
            var dest = Path.Combine(view, entry.PluginFileName);
            if (LinkOrCopy(src, dest))
            {
                linked++;
                // Companion BSA next to the plugin (Foo.esp → Foo.bsa / Foo - Textures.bsa patterns
                // are not exhaustively mirrored; Mutagen record indexing only needs the ESP/ESM/ESL).
            }
        }

        // Base masters sometimes live only in real game Data and not in any mod folder.
        foreach (var master in PluginLists.ImplicitBaseMasters)
        {
            var dest = Path.Combine(view, master);
            if (File.Exists(dest)) continue;
            var src = Path.Combine(env.GameDataPath, master);
            if (File.Exists(src) && LinkOrCopy(src, dest)) linked++;
        }

        if (missing.Count > 0)
            log.Warning("MO2 view: {Count} enabled plugins not found in mods or Data (e.g. {First})",
                missing.Count, missing[0]);
        log.Information("MO2 plugin view ready at {View} ({Linked} plugins)", view, linked);
        return linked;
    }

    private static bool LinkOrCopy(string src, string dest)
    {
        try
        {
            if (File.Exists(dest)) File.Delete(dest);
            // Hardlink keeps a single copy on disk; falls back to copy on other volumes.
            if (CreateHardLink(dest, src, IntPtr.Zero)) return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        try
        {
            File.Copy(src, dest, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("Kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
}
