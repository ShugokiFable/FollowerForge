using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;

namespace FollowerForge.Tests;

public sealed class HubTests
{
    [Theory]
    [InlineData(@"textures\Actors\Character\Female\FemaleHead.dds", FaceAssetKind.SkinDiffuse)]
    [InlineData(@"textures\Actors\Character\Female\FemaleHead_msn.dds", FaceAssetKind.SkinSupportMap)]
    [InlineData(@"textures\Actors\Character\Female\FemaleHead_sk.dds", FaceAssetKind.SkinSupportMap)]
    [InlineData(@"textures\Actors\Character\Male\BlankDetailmap.dds", FaceAssetKind.SkinSupportMap)]
    [InlineData(@"textures\ks hairdo's\daylight.dds", FaceAssetKind.Hair)]
    [InlineData(@"textures\Koralina\Eyes\brown\Amber Dark.dds", FaceAssetKind.Eyes)]
    [InlineData(@"textures\actors\character\mouth\MouthHuman.dds", FaceAssetKind.Mouth)]
    // "eyebrows03" contains both "eye" and "brow" — brows must win.
    [InlineData(@"textures\Actors\Character\Koralina Eyebrows\eyebrows03.dds", FaceAssetKind.Brows)]
    [InlineData(@"textures\actors\character\SGbrows\femalebrow07.dds", FaceAssetKind.Brows)]
    public void Classify_SortsFaceTexturesByBodyPart(string path, FaceAssetKind expected)
        => Assert.Equal(expected, HubCatalog.Classify(path));

    [Fact]
    public void Naz_SubstitutesSupportMapsForCoveredRaces()
    {
        Assert.Equal(@"textures\Nazarethblood\body\Nord\femalehead_msn.dds",
            HubCatalog.MapToNaz(@"textures\Actors\Character\Female\FemaleHead_msn.dds", "Nord"));
        Assert.Equal(@"textures\Nazarethblood\body\Dunmer\femalehead_sk.dds",
            HubCatalog.MapToNaz(@"textures\Actors\Character\Female\FemaleHead_sk.dds", "Dunmer"));
        Assert.Equal(@"textures\Nazarethblood\body\Nord\blankdetailmap.dds",
            HubCatalog.MapToNaz(@"textures\Actors\Character\Male\BlankDetailmap.dds", "Nord"));
    }

    [Fact]
    public void Naz_DoesNotSubstituteWhatItDoesNotShip()
    {
        // No diffuse in the collection...
        Assert.Null(HubCatalog.MapToNaz(@"textures\Actors\Character\Female\FemaleHead.dds", "Nord"));
        // ...and no beast races, so guessing one would be wrong.
        Assert.Null(HubCatalog.MapToNaz(@"textures\Actors\Character\Female\FemaleHead_msn.dds", "Khajiit"));
        Assert.Null(HubCatalog.MapToNaz(@"textures\Actors\Character\Female\FemaleHead_msn.dds", null));
    }

    [Theory]
    [InlineData("NordRace", "Nord")]
    [InlineData("DarkElfRace", "Dunmer")]
    [InlineData("HighElfRace", "Altmer")]
    [InlineData("WoodElfRace", "Bosmer")]
    [InlineData("KhajiitRace", null)]     // beast races are not in the collection
    [InlineData("ArgonianRace", null)]
    [InlineData("KapotunRace", null)]     // custom races are never assumed
    public void NazRaceFolder_MapsOnlyRacesItActuallyShips(string editorId, string? expected)
        => Assert.Equal(expected, HubCatalog.NazRaceFolder(editorId));

    [Fact]
    public void KnownHubs_DoNotClaimToCoverSkinColour()
    {
        // The whole honesty of the feature rests on this: no free hub supplies a skin diffuse,
        // so none of them may be listed as covering it.
        Assert.All(HubCatalog.Known, h => Assert.DoesNotContain(FaceAssetKind.SkinDiffuse, h.Covers));
        Assert.Contains(HubCatalog.Known, h => h.Covers.Contains(FaceAssetKind.SkinSupportMap));
        Assert.Contains(HubCatalog.Known, h => h.Covers.Contains(FaceAssetKind.HeadMesh));
    }

    [Fact]
    public void PermissionsDocument_RecordsTheDeclarationAndEveryCopiedFile()
    {
        var assets = new[]
        {
            new FaceAsset
            {
                RelPath = @"textures\ks hairdo's\daylight.dds", Kind = FaceAssetKind.Hair,
                Resolved = true, SourceMod = "KS Hairdos", RetargetedTo = @"textures\Mine\hair\daylight.dds",
            },
            new FaceAsset
            {
                RelPath = @"textures\untouched.dds", Kind = FaceAssetKind.Other, Resolved = true,
                SourceMod = "Something", RetargetedTo = null,
            },
        };

        var doc = HubAssetPackager.BuildPermissionsDocument("Aria", "Mine", "I checked every mod page.", assets);

        Assert.Contains("I checked every mod page.", doc);
        Assert.Contains("KS Hairdos", doc);
        Assert.Contains(@"textures\Mine\hair\daylight.dds", doc);
        // It must never imply FollowerForge granted or verified anything.
        Assert.Contains("Credit alone is not permission", doc);
        Assert.DoesNotContain(@"textures\untouched.dds", doc);
    }

    [Fact]
    public void PermissionsDocument_IsHonestWhenNothingWasCopied()
    {
        var doc = HubAssetPackager.BuildPermissionsDocument("Aria", "Mine", null, []);
        Assert.Contains("(no declaration recorded)", doc);
        Assert.Contains("(none)", doc);
    }
}
