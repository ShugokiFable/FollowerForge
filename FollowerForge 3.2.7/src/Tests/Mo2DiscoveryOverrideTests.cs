using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Mo2DiscoveryOverrideTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_mo2_override_" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    public Mo2DiscoveryOverrideTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        (_log as IDisposable)?.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void TryDiscover_ExplicitProfileBeatsIniSelectedProfile()
    {
        var fixture = CreateFixture(selectedProfile: "Wrong", profiles: ["Main", "Wrong"]);

        var result = new Mo2Discovery(_log).TryDiscover(
            fixture.Instance,
            fixture.Game,
            profileOverride: "Main",
            strictOverride: true);

        Assert.NotNull(result);
        Assert.Equal(ModManagerKind.Mo2, result.Manager);
        Assert.Equal("Main", result.ActiveProfileId);
        Assert.Equal("explicit MO2 profile override", result.ActiveProfileReason);
        Assert.Equal(Path.GetFullPath(fixture.Instance), result.InstancePath);
    }

    [Fact]
    public void TryDiscover_ExplicitMissingProfileThrowsWithoutFallback()
    {
        var fixture = CreateFixture(selectedProfile: "Main", profiles: ["Main", "Other"]);

        var error = Assert.Throws<DirectoryNotFoundException>(() =>
            new Mo2Discovery(_log).TryDiscover(
                fixture.Instance,
                fixture.Game,
                profileOverride: "DoesNotExist",
                strictOverride: true));

        Assert.Contains("DoesNotExist", error.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(fixture.Profiles, "DoesNotExist"), error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDiscover_AutomaticModeWarnsAndUsesFirstProfileWhenIniSelectionIsMissing()
    {
        var fixture = CreateFixture(selectedProfile: "Gone", profiles: ["Alpha", "Beta"]);

        var result = new Mo2Discovery(_log).TryDiscover(fixture.Instance, fixture.Game);

        Assert.NotNull(result);
        Assert.Equal("Alpha", result.ActiveProfileId);
        Assert.Equal("MO2 selected_profile fallback", result.ActiveProfileReason);
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("Gone", StringComparison.Ordinal)
            && warning.Contains("Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void TryDiscover_StrictProfileRequiresModlistAndLoadOrderFile()
    {
        var fixture = CreateFixture(selectedProfile: "Main", profiles: ["Main"]);
        var profile = Path.Combine(fixture.Profiles, "Main");
        File.Delete(Path.Combine(profile, "modlist.txt"));
        File.Delete(Path.Combine(profile, "plugins.txt"));
        File.Delete(Path.Combine(profile, "loadorder.txt"));

        var error = Assert.Throws<InvalidDataException>(() =>
            new Mo2Discovery(_log).TryDiscover(
                fixture.Instance,
                fixture.Game,
                profileOverride: "Main",
                strictOverride: true));

        Assert.Contains("modlist.txt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plugins.txt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loadorder.txt", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnvironmentDiscovery_StrictMo2FailureDoesNotFallBackToVortex()
    {
        var fixture = CreateFixture(selectedProfile: "Main", profiles: ["Main"]);

        var error = Assert.Throws<DirectoryNotFoundException>(() =>
            new EnvironmentDiscovery(_log).Discover(
                gameRootOverride: fixture.Game,
                mo2InstanceOverride: fixture.Instance,
                preferMo2: true,
                mo2ProfileOverride: "Missing",
                strictMo2Override: true));

        Assert.Contains("Missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDiscover_StrictInvalidInstanceDoesNotUseEnvironmentInstance()
    {
        var valid = CreateFixture(selectedProfile: "Main", profiles: ["Main"]);
        var invalid = Path.Combine(_root, "chosen-but-invalid");
        Directory.CreateDirectory(invalid);
        var old = Environment.GetEnvironmentVariable("FFORGE_MO2_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("FFORGE_MO2_INSTANCE", valid.Instance);

            var error = Assert.Throws<DirectoryNotFoundException>(() =>
                new Mo2Discovery(_log).TryDiscover(
                    invalid,
                    valid.Game,
                    profileOverride: "Main",
                    strictOverride: true));

            Assert.Contains(invalid, error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FFORGE_MO2_INSTANCE", old);
        }
    }

    [Fact]
    public void Cli_ExposesAndPassesMo2ProfileOverride()
    {
        var program = ReadSource("Cli", "Program.cs");

        Assert.Contains("--mo2-profile NAME", program, StringComparison.Ordinal);
        Assert.Contains(
            "mo2ProfileOverride: opts.GetValueOrDefault(\"mo2-profile\")",
            program,
            StringComparison.Ordinal);
    }

    private Fixture CreateFixture(string selectedProfile, IReadOnlyList<string> profiles)
    {
        var id = Guid.NewGuid().ToString("N");
        var instance = Path.Combine(_root, "instance-" + id);
        var game = Path.Combine(_root, "game-" + id);
        var baseDir = Path.Combine(instance, "base");
        var mods = Path.Combine(baseDir, "mods");
        var profilesRoot = Path.Combine(baseDir, "profiles");
        Directory.CreateDirectory(Path.Combine(game, "Data"));
        Directory.CreateDirectory(mods);
        Directory.CreateDirectory(Path.Combine(mods, "Example Mod"));
        foreach (var profileName in profiles)
        {
            var profile = Path.Combine(profilesRoot, profileName);
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile, "plugins.txt"), "*Skyrim.esm\n");
            File.WriteAllText(Path.Combine(profile, "loadorder.txt"), "Skyrim.esm\n");
            File.WriteAllText(Path.Combine(profile, "modlist.txt"), "+Example Mod\n");
        }

        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "ModOrganizer.ini"), $$"""
            [General]
            gamePath={{game}}
            selected_profile={{selectedProfile}}
            [Settings]
            base_directory=base
            mod_directory=%BASE_DIR%/mods
            profiles_directory=%BASE_DIR%/profiles
            overwrite_directory=%BASE_DIR%/overwrite
            """.Replace("/", Path.DirectorySeparatorChar.ToString()));

        return new Fixture(instance, game, profilesRoot);
    }

    private sealed record Fixture(string Instance, string Game, string Profiles);

    private static string ReadSource(string project, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var direct = Path.Combine(directory.FullName, project, fileName);
            if (File.Exists(direct)) return File.ReadAllText(direct);
            var underSrc = Path.Combine(directory.FullName, "src", project, fileName);
            if (File.Exists(underSrc)) return File.ReadAllText(underSrc);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {project}/{fileName} from {AppContext.BaseDirectory}");
    }
}
