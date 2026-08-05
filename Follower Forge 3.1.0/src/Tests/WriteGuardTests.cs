using FollowerForge.ModManagers;

namespace FollowerForge.Tests;

public sealed class WriteGuardTests
{
    [Fact]
    public void EnsureWritable_ThrowsInsideProtectedRoot()
    {
        var guard = new WriteGuard();
        var root = Path.Combine(Path.GetTempPath(), "ff_protected_root");
        guard.Protect(root);

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureWritable(Path.Combine(root, "Data", "Mod.esp")));
        Assert.Throws<UnauthorizedAccessException>(() => guard.EnsureWritable(root));
    }

    [Fact]
    public void EnsureWritable_AllowsOutsideProtectedRoots()
    {
        var guard = new WriteGuard();
        guard.Protect(Path.Combine(Path.GetTempPath(), "ff_game"));
        // Sibling that shares a name prefix must NOT be treated as inside.
        guard.EnsureWritable(Path.Combine(Path.GetTempPath(), "ff_game_output", "Mod.esp"));
        guard.EnsureWritable(Path.Combine(Path.GetTempPath(), "elsewhere", "Mod.esp"));
    }

    [Fact]
    public void IsProtected_IsCaseInsensitiveOnWindowsPaths()
    {
        var guard = new WriteGuard();
        guard.Protect(@"C:\Games\SkyrimSE");
        Assert.True(guard.IsProtected(@"c:\games\skyrimse\Data\x.esp"));
    }
}
