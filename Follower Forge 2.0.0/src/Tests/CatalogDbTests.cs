using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.Tests;

public sealed class CatalogDbTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "ff_cat_" + Guid.NewGuid().ToString("N"), "c.db");
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    [Fact]
    public void RecordsRoundTrip_AndSearchByTypeTextPlugin()
    {
        using var db = new CatalogDb(_dbPath, _log);
        db.ReplaceRecords(
        [
            new IndexedRecord
            {
                FormKey = "000D66:ForHonorBFCO.esp", Type = IndexedRecordType.Spell,
                EditorId = "FHBFCO_ParryAbility", DisplayName = "For Honor - Parry System",
                SourcePlugin = "ForHonorBFCO.esp", WinningPlugin = "ForHonorBFCO.esp",
                RequiredMasters = ["Skyrim.esm"], SourceMod = "For Honor in Skyrim",
            },
            new IndexedRecord
            {
                FormKey = "000800:NPCWeaponVariance.esp", Type = IndexedRecordType.CombatStyle,
                EditorId = "WV_csBandit_Berserker", SourcePlugin = "NPCWeaponVariance.esp",
                WinningPlugin = "NPCWeaponVariance.esp",
            },
        ]);

        Assert.Equal(2, db.CountRecords());
        Assert.Equal(1, db.CountRecords(IndexedRecordType.CombatStyle));

        var byType = db.SearchRecords(IndexedRecordType.CombatStyle, null);
        Assert.Single(byType);
        Assert.Equal("WV_csBandit_Berserker", byType[0].EditorId);

        var byText = db.SearchRecords(null, "Parry");
        Assert.Single(byText);

        var byPlugin = db.SearchRecords(IndexedRecordType.Spell, null, plugin: "ForHonorBFCO");
        Assert.Single(byPlugin);
        Assert.Equal("Skyrim.esm", byPlugin[0].RequiredMasters.Single());
    }

    [Fact]
    public void Assets_ExistenceIsPathNormalized()
    {
        using var db = new CatalogDb(_dbPath, _log);
        db.ReplaceAssets(
        [
            new AssetFile
            {
                RelPath = @"meshes\actors\character\facegendata\facegeom\x.nif",
                Container = AssetContainerKind.Bsa, ContainerName = "X.bsa", Size = 10,
            },
        ]);

        Assert.True(db.AssetExists("Meshes/Actors/Character/FaceGenData/FaceGeom/X.nif"));
        Assert.False(db.AssetExists("meshes/missing.nif"));
    }

    [Fact]
    public void Meta_RoundTrips()
    {
        using var db = new CatalogDb(_dbPath, _log);
        db.SetMeta("deployment_time", "12345");
        Assert.Equal("12345", db.GetMeta("deployment_time"));
        db.SetMeta("deployment_time", "999");
        Assert.Equal("999", db.GetMeta("deployment_time"));
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_dbPath)!;
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort; a locked WAL file must never fail the test.
        }
    }
}
