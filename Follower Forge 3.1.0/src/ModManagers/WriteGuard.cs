namespace FollowerForge.ModManagers;

/// <summary>
/// Central enforcement of the read-only rule: game Data, Vortex staging, Vortex profiles
/// and anything else registered here must never be written by Follower Forge.
/// Every file write in the app must pass through <see cref="EnsureWritable"/>.
/// </summary>
public sealed class WriteGuard
{
    private readonly List<string> _protectedRoots = [];

    public void Protect(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!_protectedRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
            _protectedRoots.Add(full);
    }

    public IReadOnlyList<string> ProtectedRoots => _protectedRoots;

    public bool IsProtected(string path)
    {
        var full = Path.GetFullPath(path);
        foreach (var root in _protectedRoots)
        {
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Throws when <paramref name="path"/> is under any protected root.</summary>
    public void EnsureWritable(string path)
    {
        if (IsProtected(path))
            throw new UnauthorizedAccessException(
                $"Refusing to write inside a protected (read-only) location: {path}");
    }
}
