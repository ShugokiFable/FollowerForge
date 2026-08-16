namespace FollowerForge.ModManagers;

/// <summary>
/// Central enforcement of the read-only rule: game Data, Vortex staging, Vortex profiles
/// and anything else registered here must never be written by FollowerForge.
/// Every file write in the app must pass through <see cref="EnsureWritable"/>.
/// </summary>
public sealed class WriteGuard
{
    private readonly List<string> _protectedRoots = [];
    private readonly List<string> _allowedRoots = [];

    public void Protect(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!_protectedRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
            _protectedRoots.Add(full);
    }

    /// <summary>
    /// Explicit user-chosen destination. A folder under Vortex/MO2 mods is still protected
    /// against accidental writes, but a Paths-dialog choice is consent to publish there.
    /// Game Data must not be passed here — the dialog rejects it first.
    /// </summary>
    public void Allow(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!_allowedRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
            _allowedRoots.Add(full);
    }

    public IReadOnlyList<string> ProtectedRoots => _protectedRoots;

    public bool IsProtected(string path)
    {
        var full = Path.GetFullPath(path);
        foreach (var allowed in _allowedRoots)
        {
            if (IsUnder(full, allowed)) return false;
        }
        foreach (var root in _protectedRoots)
        {
            if (IsUnder(full, root)) return true;
        }
        return false;
    }

    private static bool IsUnder(string full, string root) =>
        full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || string.Equals(full, root, StringComparison.OrdinalIgnoreCase);

    /// <summary>Throws when <paramref name="path"/> is under any protected root.</summary>
    public void EnsureWritable(string path)
    {
        if (IsProtected(path))
            throw new UnauthorizedAccessException(
                $"Refusing to write inside a protected (read-only) location: {path}");
    }
}
