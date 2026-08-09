using FollowerForge.ModManagers;
using FollowerForge.Ui;
using Serilog;

namespace FollowerForge.Tests;

public sealed class Mo2SetupControllerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_mo2_setup_" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    public Mo2SetupControllerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        (_log as IDisposable)?.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Inspect_ReturnsResolvedSummaryAndProfilesWithoutIndexing()
    {
        var fixture = CreateFixture(createOverwrite: true);

        var state = new Mo2SetupController(_log).Inspect(fixture.Ini);

        Assert.True(state.Inspection.IsValid, string.Join(Environment.NewLine, state.Errors));
        Assert.Equal(new[] { "Main", "Testing" }, state.Profiles);
        Assert.Contains(fixture.Instance, state.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixture.BaseDirectory, state.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.Selection);
    }

    [Fact]
    public void Validate_RejectsProfileMissingModlistAndLoadOrderFiles()
    {
        var fixture = CreateFixture(createOverwrite: true);
        var profile = Path.Combine(fixture.Profiles, "Main");
        File.Delete(Path.Combine(profile, "modlist.txt"));
        File.Delete(Path.Combine(profile, "plugins.txt"));

        var state = new Mo2SetupController(_log).Validate(fixture.Ini, "Main");

        Assert.False(state.IsValid);
        Assert.Null(state.Selection);
        Assert.Contains(state.Errors, error => error.Contains("modlist.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(state.Errors, error =>
            error.Contains("plugins.txt", StringComparison.OrdinalIgnoreCase)
            && error.Contains("loadorder.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MissingOverwriteIsWarningOnlyAndReturnsExactSelection()
    {
        var fixture = CreateFixture(createOverwrite: false);

        var state = new Mo2SetupController(_log).Validate(fixture.Ini, "Testing");

        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Errors));
        Assert.Equal(new Mo2UserSelection(fixture.Instance, "Testing"), state.Selection);
        Assert.Contains(state.Warnings, warning => warning.Contains("overwrite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WizardXaml_OffersMo2SetupBelowManagerSwitch()
    {
        var xaml = ReadSource("Ui", "WizardWindow.axaml");
        var switchIndex = xaml.IndexOf("x:Name=\"ManagerSwitchButton\"", StringComparison.Ordinal);
        var setupIndex = xaml.IndexOf("x:Name=\"Mo2SetupButton\"", StringComparison.Ordinal);

        Assert.True(switchIndex >= 0);
        Assert.True(setupIndex > switchIndex, "MO2 setup button should be directly available below the manager switch.");
        Assert.Contains("Click=\"OnMo2Setup\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WizardReload_LoadsSavedOverrideAndCancelsBeforeReindex()
    {
        var source = ReadSource("Ui", "WizardWindow.axaml.cs");

        Assert.Contains("Mo2UserSettings.Load", source, StringComparison.Ordinal);
        Assert.Contains("strictMo2Override: savedMo2 is not null", source, StringComparison.Ordinal);
        var handler = source.Substring(source.IndexOf("OnMo2Setup", StringComparison.Ordinal));
        Assert.True(
            handler.IndexOf("_loadCts?.Cancel()", StringComparison.Ordinal)
            < handler.IndexOf("LoadEverythingAsync()", StringComparison.Ordinal),
            "The old index generation must be cancelled before the manual MO2 re-index starts.");
    }

    private Fixture CreateFixture(bool createOverwrite)
    {
        var instance = Path.Combine(_root, "instance-" + Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(instance, "base");
        var game = Path.Combine(_root, "game-" + Guid.NewGuid().ToString("N"));
        var profiles = Path.Combine(baseDirectory, "profiles");
        Directory.CreateDirectory(Path.Combine(game, "Data"));
        Directory.CreateDirectory(Path.Combine(baseDirectory, "mods"));
        if (createOverwrite) Directory.CreateDirectory(Path.Combine(baseDirectory, "overwrite"));
        foreach (var name in new[] { "Main", "Testing" })
        {
            var profile = Path.Combine(profiles, name);
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile, "plugins.txt"), "*Skyrim.esm\n");
            File.WriteAllText(Path.Combine(profile, "modlist.txt"), string.Empty);
        }

        Directory.CreateDirectory(instance);
        var ini = Path.Combine(instance, "ModOrganizer.ini");
        File.WriteAllText(ini, $$"""
            [General]
            gamePath={{game}}
            selected_profile=Main
            [Settings]
            base_directory=base
            mod_directory=%BASE_DIR%/mods
            profiles_directory=%BASE_DIR%/profiles
            overwrite_directory=%BASE_DIR%/overwrite
            """.Replace("/", Path.DirectorySeparatorChar.ToString()));
        return new Fixture(ini, instance, baseDirectory, profiles);
    }

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

    private sealed record Fixture(string Ini, string Instance, string BaseDirectory, string Profiles);
}
