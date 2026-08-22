using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using Microsoft.Data.Sqlite;
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

    [Fact]
    public void CatalogueFreshness_RequiresSameMo2ProfileInstanceAndStagingPath()
    {
        var instance = Path.Combine(Path.GetTempPath(), "MO2-A");
        var staging = Path.Combine(instance, "mods");
        using (var db = new CatalogDb(_dbPath, _log))
        {
            db.SetMeta("manager", ModManagerKind.Mo2.ToString());
            db.SetMeta("deployment_time", "12345");
            db.SetMeta("profile_id", "Main");
            db.SetMeta("instance_path", instance);
            db.SetMeta("staging_path", staging);
            db.SetMeta("index_version", CatalogBuilder.IndexVersion);
        }

        var matching = EnvironmentFor("Main", instance, staging);
        Assert.True(CatalogBuilder.IsFresh(matching, _dbPath));
        Assert.False(CatalogBuilder.IsFresh(
            EnvironmentFor("Testing", instance, staging), _dbPath));
        Assert.False(CatalogBuilder.IsFresh(
            EnvironmentFor("Main", Path.Combine(Path.GetTempPath(), "MO2-B"), staging), _dbPath));
        Assert.False(CatalogBuilder.IsFresh(
            EnvironmentFor("Main", instance, Path.Combine(instance, "custom-mods")), _dbPath));
    }

    /// <summary>
    /// 3.3.0 started indexing Ammo. A catalogue written before that is stale even though the
    /// deployment has not changed — otherwise the arrows list comes up empty for every existing
    /// user and looks like a broken feature.
    /// </summary>
    [Fact]
    public void CatalogueBuiltByAnOlderIndexer_IsStale_EvenWhenNothingElseChanged()
    {
        var instance = Path.Combine(Path.GetTempPath(), "MO2-A");
        var staging = Path.Combine(instance, "mods");
        using (var db = new CatalogDb(_dbPath, _log))
        {
            db.SetMeta("manager", ModManagerKind.Mo2.ToString());
            db.SetMeta("deployment_time", "12345");
            db.SetMeta("profile_id", "Main");
            db.SetMeta("instance_path", instance);
            db.SetMeta("staging_path", staging);
            db.SetMeta("index_version", "1");   // pre-Ammo
        }

        Assert.False(CatalogBuilder.IsFresh(EnvironmentFor("Main", instance, staging), _dbPath));
    }

    [Fact]
    public void LegacySchema_IsInvalidatedInsteadOfCrashingOnFormKeyQuery()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using (var legacy = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            legacy.Open();
            using var cmd = legacy.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE records(
                    record_type TEXT NOT NULL,
                    form_id TEXT NOT NULL,
                    editor_id TEXT,
                    name TEXT);
                INSERT INTO records(record_type,form_id,editor_id,name)
                VALUES('Race','00013746','NordRace','Nord');
                """;
            cmd.ExecuteNonQuery();
        }

        using var db = new CatalogDb(_dbPath, _log);
        Assert.Equal(0, db.CountRecords());
        Assert.Equal(CatalogDb.SchemaVersion, db.GetMeta("schema_version"));
        Assert.Empty(db.SearchRecords(IndexedRecordType.Race, null));
    }

    [Fact]
    public void PriorVersionWithCurrentColumns_IsInvalidatedSoArmorDetailsAreReindexed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using (var legacy = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            legacy.Open();
            using var cmd = legacy.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                INSERT INTO meta(key,value) VALUES('schema_version','2');
                INSERT INTO meta(key,value) VALUES('deployment_time','12345');
                CREATE TABLE records(
                    form_key TEXT NOT NULL,
                    type TEXT NOT NULL,
                    editor_id TEXT,
                    display_name TEXT,
                    source_plugin TEXT NOT NULL,
                    winning_plugin TEXT NOT NULL,
                    masters TEXT NOT NULL,
                    flags INTEGER NOT NULL,
                    source_mod TEXT,
                    detail TEXT,
                    PRIMARY KEY(form_key, type));
                INSERT INTO records(
                    form_key,type,editor_id,display_name,source_plugin,winning_plugin,
                    masters,flags,source_mod,detail)
                VALUES(
                    '012E49:Skyrim.esm','Armor','ArmorIronCuirass','Iron Armor',
                    'Skyrim.esm','Skyrim.esm','',0,NULL,NULL);
                """;
            cmd.ExecuteNonQuery();
        }

        using var db = new CatalogDb(_dbPath, _log);
        Assert.Equal(0, db.CountRecords());
        Assert.Null(db.GetMeta("deployment_time"));
        Assert.Equal(CatalogDb.SchemaVersion, db.GetMeta("schema_version"));
    }

    [Fact]
    public void CorruptSqliteFile_CanBeQuarantinedAndRecreated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        File.WriteAllText(_dbPath, "this is not a sqlite database");

        var failure = Assert.Throws<SqliteException>(() =>
        {
            using var ignored = new CatalogDb(_dbPath, _log);
        });
        Assert.True(CatalogDb.IsCacheFailure(failure));

        var quarantined = CatalogDb.QuarantineBrokenCache(_dbPath, _log);
        Assert.Single(quarantined);
        Assert.True(File.Exists(quarantined[0]));
        Assert.False(File.Exists(_dbPath));

        using var rebuilt = new CatalogDb(_dbPath, _log);
        Assert.Equal(CatalogDb.SchemaVersion, rebuilt.GetMeta("schema_version"));
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

    private static EnvironmentSnapshot EnvironmentFor(string profile, string instance, string staging) =>
        new()
        {
            Manager = ModManagerKind.Mo2,
            ManagerLabel = "Mod Organizer 2",
            GameRootPath = Path.Combine(Path.GetTempPath(), "game"),
            GameDataPath = Path.Combine(Path.GetTempPath(), "game", "Data"),
            PluginDataPath = Path.Combine(Path.GetTempPath(), "view"),
            InstancePath = instance,
            StagingPath = staging,
            ProfilesPath = Path.Combine(instance, "profiles"),
            RuntimePluginsTxtPath = Path.Combine(Path.GetTempPath(), "plugins.txt"),
            ActiveProfileId = profile,
            DeploymentTimeUtcMs = 12345,
        };
}
