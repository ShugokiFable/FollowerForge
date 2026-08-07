using FollowerForge.BuildPipeline;

namespace FollowerForge.Tests;

public sealed class FaceTextureResolutionTests
{
    [Fact]
    public void DeployedDataFallback_ResolvesLooseTextureMissingFromCatalogue()
    {
        var data = Path.Combine(Path.GetTempPath(), "ff_tex_" + Guid.NewGuid().ToString("N"), "Data");
        var relative = @"textures\actors\character\test\face.dds";
        var absolute = Path.Combine(data, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, [0x44, 0x44, 0x53, 0x20]);
        try
        {
            var result = FollowerBuilder.ResolveFaceTexture(@"Data\" + relative, data, catalog: null);
            Assert.True(result.Resolved);
            Assert.Equal("deployed Data file", result.Container);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(data)!, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void DeployedDataFallback_RejectsTraversalOutsideData()
    {
        var data = Path.Combine(Path.GetTempPath(), "ff_tex_" + Guid.NewGuid().ToString("N"), "Data");
        Directory.CreateDirectory(data);
        try
        {
            var result = FollowerBuilder.ResolveFaceTexture(@"..\outside.dds", data, catalog: null);
            Assert.False(result.Resolved);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(data)!, recursive: true); } catch (IOException) { }
        }
    }
}
