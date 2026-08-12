namespace FollowerForge.Tests;

/// <summary>
/// Guards the 3.2.0 hang: SKYRIM_MO2_INSTANCE pointing at houseCARL's Vortex shim must not
/// steal discovery from a working Vortex install.
/// </summary>
public sealed class DiscoveryPriorityTests
{
    [Fact]
    public void EnvironmentDiscovery_DoesNotTreatSkyrimMo2InstanceAsPreferMo2()
    {
        var src = ReadModManagersSource("EnvironmentDiscovery.cs");
        Assert.Contains("Do NOT treat SKYRIM_MO2_INSTANCE", src, StringComparison.Ordinal);
        Assert.Contains("FFORGE_PREFER_MO2", src, StringComparison.Ordinal);
        // Prefer-MO2 gate must not list SKYRIM_MO2_INSTANCE.
        var preferBlockStart = src.IndexOf("var explicitMo2", StringComparison.Ordinal);
        Assert.True(preferBlockStart >= 0);
        var preferBlock = src.Substring(preferBlockStart, Math.Min(500, src.Length - preferBlockStart));
        Assert.DoesNotContain("SKYRIM_MO2_INSTANCE", preferBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Mo2Discovery_SkipsHouseCarlShimByDefault()
    {
        var src = ReadModManagersSource("Mo2Discovery.cs");
        Assert.Contains("HOUSECARL-SHIM.txt", src, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsHouseCarlShim", src, StringComparison.Ordinal);
        Assert.Contains("Do not auto-add houseCARL-Shim", src, StringComparison.Ordinal);
    }

    private static string ReadModManagersSource(string fileName)
    {
        // Tests run from src/Tests/bin/Release/net10.0 → walk up to src/ModManagers.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ModManagers", fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            candidate = Path.Combine(dir.FullName, "src", "ModManagers", fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Could not locate {fileName} from {AppContext.BaseDirectory}");
    }
}
