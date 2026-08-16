using FollowerForge.AssetIndex;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Only two things reach a follower's face: the vanilla 19 morphs, which go on the NPC record,
/// and geometry, which bakes into the exported head. RaceMenu's extra sliders (CME_/EFM_/SPG_)
/// are neither — they move the head's nodes on a live actor. Measured on a real install: not one
/// of those names exists as a vertex morph in any chargen .tri, vanilla or High Poly Head.
///
/// So a preset built entirely from sliders looks right on your own character and flat on the
/// follower made from it, and nothing downstream can recover the difference. Reading the two
/// counts apart is what lets the build say so instead of shipping a flat face quietly.
/// </summary>
public sealed class SliderOnlyPresetTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    private static (int Sliders, int Sculpted) Read(string jslot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_slider_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "p.jslot");
        try
        {
            File.WriteAllText(path, jslot);
            var a = new CharGenDiscovery(Log).ReadJslotAppearance(path);
            Assert.NotNull(a);
            return (a!.SliderCount, a.SculptedVertices);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact]
    public void SliderOnlyPreset_IsCountedAsSlidersWithNoSculpt()
    {
        // Shape of a FaceForge-generated preset: sculpt hosts are listed but carry no deltas.
        var (sliders, sculpted) = Read("""
            {
              "actor": { "weight": 50 },
              "morphs": {
                "custom": [
                  { "name": "EFM_Nose_Tip_Width", "value": -0.58 },
                  { "name": "CME_EyesSize", "value": 0.42 },
                  { "name": "SPG_ECEUpperEyeLidShape", "value": 0.93 },
                  { "name": "CME_JawWidth", "value": 0.0 }
                ],
                "sculpt": [
                  { "host": "KL\\High Poly Head\\FemaleHeadCharGen.tri", "vertices": 3832 }
                ],
                "default": { "morphs": [0.1], "presets": [1] }
              }
            }
            """);

        Assert.Equal(3, sliders);          // the zero-valued slider does not count
        Assert.Equal(0, sculpted);
    }

    [Fact]
    public void SculptedPreset_ReportsItsVertexDeltas()
    {
        // Shape of a preset actually sculpted in RaceMenu: hosts carry delta rows.
        var (sliders, sculpted) = Read("""
            {
              "actor": { "weight": 50 },
              "morphs": {
                "custom": [ { "name": "CME_JawWidth", "value": 0.4 } ],
                "sculpt": [
                  { "host": "KL\\High Poly Head\\FemaleHeadCharGen.tri", "vertices": 3832,
                    "data": [[2255,468,-541,293],[2256,12,-3,7]] },
                  { "host": "Actors\\Character\\Character Assets\\EyesFemaleChargen.tri",
                    "vertices": 176, "data": [[65,-1,0,0]] }
                ],
                "default": { "morphs": [0.1], "presets": [1] }
              }
            }
            """);

        Assert.Equal(1, sliders);
        Assert.Equal(3, sculpted);
    }

    [Fact]
    public void APresetWithNeitherIsNotFlaggedAsAnything()
    {
        var (sliders, sculpted) = Read("""
            { "actor": { "weight": 50 }, "morphs": { "default": { "morphs": [0.1] } } }
            """);

        Assert.Equal(0, sliders);
        Assert.Equal(0, sculpted);
    }
}
