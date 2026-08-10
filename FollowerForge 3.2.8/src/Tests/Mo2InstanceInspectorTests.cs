using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Mo2InstanceInspectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_mo2_inspect_" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    public Mo2InstanceInspectorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        (_log as IDisposable)?.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Inspect_ExpandsBaseDirCaseInsensitivelyAndEnumeratesProfiles()
    {
        var instance = Path.Combine(_root, "portable");
        var game = Path.Combine(_root, "game");
        var baseDir = Path.Combine(instance, "storage");
        Directory.CreateDirectory(Path.Combine(game, "Data"));
        Directory.CreateDirectory(Path.Combine(baseDir, "mods"));
        Directory.CreateDirectory(Path.Combine(baseDir, "profiles", "Main"));
        Directory.CreateDirectory(Path.Combine(baseDir, "profiles", "Testing"));

        var ini = WriteIni(instance, $$"""
            [General]
            gamePath={{game}}
            selected_profile=Main
            [Settings]
            base_directory=storage
            mod_directory=%base_dir%/mods
            profiles_directory=%BASE_DIR%/profiles
            overwrite_directory=%BaSe_DiR%/overwrite
            """);

        var result = new Mo2InstanceInspector(_log).Inspect(ini);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(Path.GetFullPath(instance), result.InstanceRoot);
        Assert.Equal(Path.GetFullPath(baseDir), result.BaseDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "mods")), result.ModsPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "profiles")), result.ProfilesPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "overwrite")), result.OverwritePath);
        Assert.Equal("Main", result.SelectedProfile);
        Assert.Equal(new[] { "Main", "Testing" }, result.Profiles);
        Assert.Contains(result.Warnings, warning => warning.Contains("overwrite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_AnchorsRelativeConfiguredPathsToResolvedBaseDirectory()
    {
        var instance = Path.Combine(_root, "instance");
        var game = Path.Combine(_root, "game-relative");
        var baseDir = Path.Combine(instance, "base");
        Directory.CreateDirectory(Path.Combine(game, "Data"));
        Directory.CreateDirectory(Path.Combine(baseDir, "custom-mods"));
        Directory.CreateDirectory(Path.Combine(baseDir, "custom-profiles", "Default"));

        var ini = WriteIni(instance, $$"""
            [General]
            gamePath={{game}}
            selected_profile=Default
            [Settings]
            base_directory=base
            mod_directory=custom-mods
            profiles_directory=custom-profiles
            overwrite_directory=custom-overwrite
            """);

        var result = new Mo2InstanceInspector(_log).Inspect(ini);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "custom-mods")), result.ModsPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "custom-profiles")), result.ProfilesPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "custom-overwrite")), result.OverwritePath);
    }

    [Fact]
    public void Inspect_ExpandsEnvironmentVariablesBeforeCanonicalizing()
    {
        const string variable = "FFORGE_TEST_MO2_BASE";
        var old = Environment.GetEnvironmentVariable(variable);
        var externalBase = Path.Combine(_root, "environment-base");
        var instance = Path.Combine(_root, "environment-instance");
        var game = Path.Combine(_root, "environment-game");
        try
        {
            Environment.SetEnvironmentVariable(variable, externalBase);
            Directory.CreateDirectory(Path.Combine(game, "Data"));
            Directory.CreateDirectory(Path.Combine(externalBase, "mods"));
            Directory.CreateDirectory(Path.Combine(externalBase, "profiles", "EnvProfile"));
            var ini = WriteIni(instance, $$"""
                [General]
                gamePath={{game}}
                selected_profile=EnvProfile
                [Settings]
                base_directory=%{{variable}}%
                mod_directory=%BASE_DIR%/mods
                profiles_directory=%BASE_DIR%/profiles
                overwrite_directory=%BASE_DIR%/overwrite
                """);

            var result = new Mo2InstanceInspector(_log).Inspect(ini);

            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(Path.GetFullPath(externalBase), result.BaseDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, old);
        }
    }

    [Fact]
    public void Inspect_ReportsExactMissingRequiredPaths()
    {
        var instance = Path.Combine(_root, "invalid");
        var ini = WriteIni(instance, """
            [General]
            gamePath=missing-game
            [Settings]
            mod_directory=missing-mods
            profiles_directory=missing-profiles
            """);

        var result = new Mo2InstanceInspector(_log).Inspect(ini);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("game root", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("mods directory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("profiles directory", StringComparison.OrdinalIgnoreCase));
    }

    private static string WriteIni(string instance, string contents)
    {
        Directory.CreateDirectory(instance);
        var ini = Path.Combine(instance, "ModOrganizer.ini");
        File.WriteAllText(ini, contents.Replace("/", Path.DirectorySeparatorChar.ToString()));
        return ini;
    }
}
