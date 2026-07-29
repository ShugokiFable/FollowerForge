using FollowerForge.AssetIndex;
using Serilog;

namespace FollowerForge.Tests;

public sealed class CharGenDiscoveryTests
{
    [Fact]
    public void Jslot_ParsesCompleteRecordAppearanceAndIdentifierOnlyDependency()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff_jslot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Silvia.jslot");
        try
        {
            File.WriteAllText(path, """
                {
                  "actor": { "hairColor": 5395287, "weight": 59.0 },
                  "mods": [
                    { "index": 0, "name": "Skyrim.esm" },
                    { "index": 18, "name": "High Poly Head.esm" }
                  ],
                  "headParts": [
                    { "type": 0, "formId": 333071, "formIdentifier": "Skyrim.esm|05150F" },
                    { "type": 6, "formId": 4268042430, "formIdentifier": "Even More Eyes.esp|6528BE" }
                  ],
                  "morphs": {
                    "default": {
                      "morphs": [0.0, 0.2, -1.0],
                      "presets": [13, 4294967295, 16, 3]
                    }
                  },
                  "tintInfo": [
                    { "index": 0, "color": 4294966267,
                      "texture": "Actors\\Character\\Character Assets\\TintMasks\\SkinTone.dds" }
                  ]
                }
                """);

            var discovery = new CharGenDiscovery(new LoggerConfiguration().CreateLogger());
            var appearance = discovery.ReadJslotAppearance(path);
            var plugins = discovery.ReadJslotPlugins(path);

            Assert.NotNull(appearance);
            Assert.Equal(59f, appearance!.Weight);
            Assert.Equal(2, appearance.HeadParts.Count);
            Assert.Contains(appearance.HeadParts, p => p.FormKey == "0008BE:Even More Eyes.esp");
            Assert.Equal(0.2f, appearance.FaceMorphs[1]);
            Assert.Equal(uint.MaxValue, appearance.FacePresets[1]);
            Assert.Equal((uint)5395287, appearance.HairColorArgb);
            Assert.Equal("FFFBFBFF", appearance.SkinToneRgba);
            Assert.Contains("Even More Eyes.esp", plugins);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
