using FollowerForge.AssetIndex;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Head exports and their presets are frequently saved under different decorations
/// ("X_head.nif" vs "X_Replacer.jslot", "MoryaSculpt.nif" vs "Morya.jslot"). Failing to pair
/// them made usable faces look broken and told the user to re-save a preset that already
/// existed, so the pairing rules are pinned here.
/// </summary>
public sealed class CharGenMatchingTests : IDisposable
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff_cg_" + Guid.NewGuid().ToString("N"));
    private readonly string _charGen;

    public CharGenMatchingTests()
    {
        _charGen = Path.Combine(_root, "SKSE", "Plugins", "CharGen");
        Directory.CreateDirectory(Path.Combine(_charGen, "Presets"));
    }

    private void Head(string name) => File.WriteAllText(Path.Combine(_charGen, name + ".nif"), "");

    private void Preset(string name)
    {
        // Minimal but real jslot: discovery must be able to parse an appearance out of it.
        var json = """
            {"actor":{"weight":50.0,"hairColor":0},"mods":[{"index":0,"name":"Skyrim.esm"}],
             "headParts":[{"formIdentifier":"Skyrim.esm|05150F","type":0}],
             "morphs":{},"tintInfo":[]}
            """;
        File.WriteAllText(Path.Combine(_charGen, "Presets", name + ".jslot"), json);
    }

    private IReadOnlyList<Domain.CharGenExport> Discover() =>
        new CharGenDiscovery(Log).Discover(_root);

    [Theory]
    [InlineData("A1_Imperial_Matilda_head", "A1_Imperial_Matilda_Replacer")]
    [InlineData("A1_High Elf_Eva_head", "A1_High Elf_Eva_Replacer")]
    [InlineData("MoryaSculpt", "Morya")]
    [InlineData("Aoife", "Aoife")]
    [InlineData("Kitty Preset", "Kitty Preset")]
    public void RealWorldNamingVariants_StillPair(string head, string preset)
    {
        Head(head);
        Preset(preset);

        var export = Assert.Single(Discover());
        Assert.Equal(head, export.Name);
        Assert.NotNull(export.JslotPath);
        Assert.True(export.IsUsable, export.Blocker);
    }

    [Fact]
    public void GenuinelyMissingPreset_IsReportedAsUnusable()
    {
        Head("Nobody_head");

        var export = Assert.Single(Discover());
        Assert.False(export.IsUsable);
        Assert.Contains("no matching RaceMenu preset", export.Blocker);
    }

    /// <summary>
    /// Two presets that both plausibly match must not be guessed between — pairing a face with
    /// the wrong preset silently builds the wrong follower.
    /// </summary>
    [Fact]
    public void AmbiguousPrefixes_AreLeftUnmatchedRatherThanGuessed()
    {
        Head("Priscilla_Kitty_head");
        Preset("Priscilla_Kitty_Big");
        Preset("Priscilla_Kitty_Sml");   // same length: no clear winner

        var export = Assert.Single(Discover());
        Assert.Null(export.JslotPath);
        Assert.False(export.IsUsable);
    }

    [Fact]
    public void ExactMatchWins_EvenWhenALongerPrefixCandidateExists()
    {
        Head("Kitty");
        Preset("Kitty");
        Preset("KittyDeluxeEdition");

        var export = Assert.Single(Discover());
        Assert.EndsWith("Kitty.jslot", export.JslotPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }
}
