using FollowerForge.Domain;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FollowerForge.AssetIndex;

/// <summary>
/// SQLite-backed catalogue of winning records and winning asset files.
/// One database per modpack state; staleness is detected via the Vortex deployment time.
/// </summary>
public sealed class CatalogDb : IDisposable
{
    public const string SchemaVersion = "3";
    private readonly SqliteConnection _conn;
    private readonly ILogger _log;

    public string DbPath { get; }

    /// <summary>True only for failures produced by SQLite while reading the disposable cache.</summary>
    public static bool IsCacheFailure(Exception exception) =>
        exception is SqliteException
        || exception.InnerException is not null && IsCacheFailure(exception.InnerException);

    /// <summary>
    /// Moves a broken cache and its WAL sidecars aside so the caller can rebuild once. The old
    /// bytes remain available for diagnosis; game files and Vortex staging are never touched.
    /// </summary>
    public static IReadOnlyList<string> QuarantineBrokenCache(string dbPath, ILogger log)
    {
        SqliteConnection.ClearAllPools();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var moved = new List<string>();
        foreach (var source in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (!File.Exists(source)) continue;
            var destination = source + $".broken-{stamp}";
            File.Move(source, destination);
            moved.Add(destination);
        }
        log.Warning("Quarantined {Count} broken catalogue file(s); the cache will be rebuilt.", moved.Count);
        return moved;
    }

    public CatalogDb(string dbPath, ILogger log)
    {
        _log = log;
        DbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // One long-lived connection per catalogue: pooling gives no benefit and would keep the
            // file handle alive after Dispose, blocking deletion/rebuild of the db file.
            Pooling = false,
        }.ToString());
        try
        {
            _conn.Open();
            Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");
            InvalidateIncompatibleSchema();
            CreateSchema();
            SetMeta("schema_version", SchemaVersion);
        }
        catch
        {
            // A failed constructor is not disposable by the caller. Release SQLite's handle here
            // so automatic cache quarantine can move the broken file immediately.
            _conn.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The catalogue is a disposable cache. Older FollowerForge builds used a different
    /// records table; CREATE TABLE IF NOT EXISTS cannot upgrade it and caused the wizard to
    /// fail later with "no such column: form_key". Invalidate that cache atomically so the
    /// normal freshness check rebuilds it from the current Vortex deployment.
    /// </summary>
    private void InvalidateIncompatibleSchema()
    {
        using var exists = _conn.CreateCommand();
        exists.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='records'";
        if (exists.ExecuteScalar() is null) return;

        using var info = _conn.CreateCommand();
        info.CommandText = "PRAGMA table_info(records)";
        using var reader = info.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) columns.Add(reader.GetString(1));

        string[] required =
        [
            "form_key", "type", "editor_id", "display_name", "source_plugin",
            "winning_plugin", "masters", "flags", "source_mod", "detail",
        ];
        string? existingVersion = null;
        using (var metaExists = _conn.CreateCommand())
        {
            metaExists.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='meta'";
            if (metaExists.ExecuteScalar() is not null)
            {
                using var version = _conn.CreateCommand();
                version.CommandText = "SELECT value FROM meta WHERE key='schema_version'";
                existingVersion = version.ExecuteScalar() as string;
            }
        }
        if (required.All(columns.Contains)
            && string.Equals(existingVersion, SchemaVersion, StringComparison.Ordinal))
            return;

        reader.Close();
        _log.Warning(
            "FollowerForge catalogue schema {OldVersion} is obsolete; invalidating the disposable cache for schema {NewVersion}.",
            existingVersion ?? "(unversioned)", SchemaVersion);
        Exec("""
            DROP TABLE IF EXISTS records;
            DROP TABLE IF EXISTS assets;
            DROP TABLE IF EXISTS plugins;
            DROP TABLE IF EXISTS meta;
            """);
    }

    private void CreateSchema()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS meta(
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS plugins(
                name TEXT PRIMARY KEY COLLATE NOCASE,
                load_index INTEGER NOT NULL,
                enabled INTEGER NOT NULL,
                source_mod TEXT);
            CREATE TABLE IF NOT EXISTS records(
                form_key TEXT NOT NULL,
                type TEXT NOT NULL,
                editor_id TEXT COLLATE NOCASE,
                display_name TEXT COLLATE NOCASE,
                source_plugin TEXT NOT NULL,
                winning_plugin TEXT NOT NULL,
                masters TEXT NOT NULL,
                flags INTEGER NOT NULL,
                source_mod TEXT,
                detail TEXT,
                PRIMARY KEY(form_key, type));
            CREATE INDEX IF NOT EXISTS idx_records_type ON records(type);
            CREATE INDEX IF NOT EXISTS idx_records_edid ON records(editor_id);
            CREATE INDEX IF NOT EXISTS idx_records_name ON records(display_name);
            CREATE TABLE IF NOT EXISTS assets(
                rel_path TEXT PRIMARY KEY,
                container INTEGER NOT NULL,
                container_name TEXT NOT NULL,
                source_mod TEXT,
                size INTEGER NOT NULL);
            """);
    }

    public void SetMeta(string key, string value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO meta(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public string? GetMeta(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void ReplacePlugins(IEnumerable<(LoadOrderEntry Entry, string? SourceMod)> plugins)
    {
        using var tx = _conn.BeginTransaction();
        Exec("DELETE FROM plugins", tx);
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO plugins(name,load_index,enabled,source_mod) VALUES($n,$i,$e,$s)";
        var pn = cmd.CreateParameter(); pn.ParameterName = "$n"; cmd.Parameters.Add(pn);
        var pi = cmd.CreateParameter(); pi.ParameterName = "$i"; cmd.Parameters.Add(pi);
        var pe = cmd.CreateParameter(); pe.ParameterName = "$e"; cmd.Parameters.Add(pe);
        var ps = cmd.CreateParameter(); ps.ParameterName = "$s"; cmd.Parameters.Add(ps);
        foreach (var (entry, sourceMod) in plugins)
        {
            pn.Value = entry.PluginFileName;
            pi.Value = entry.Index;
            pe.Value = entry.Enabled ? 1 : 0;
            ps.Value = (object?)sourceMod ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public long ReplaceRecords(IEnumerable<IndexedRecord> records)
    {
        using var tx = _conn.BeginTransaction();
        Exec("DELETE FROM records", tx);
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO records(form_key,type,editor_id,display_name,source_plugin,
                winning_plugin,masters,flags,source_mod,detail)
            VALUES($fk,$t,$e,$dn,$sp,$wp,$m,$f,$sm,$d)
            """;
        var p = new Dictionary<string, SqliteParameter>();
        foreach (var name in new[] { "$fk", "$t", "$e", "$dn", "$sp", "$wp", "$m", "$f", "$sm", "$d" })
        {
            var par = cmd.CreateParameter(); par.ParameterName = name; cmd.Parameters.Add(par);
            p[name] = par;
        }
        long count = 0;
        foreach (var r in records)
        {
            p["$fk"].Value = r.FormKey;
            p["$t"].Value = r.Type.ToString();
            p["$e"].Value = (object?)r.EditorId ?? DBNull.Value;
            p["$dn"].Value = (object?)r.DisplayName ?? DBNull.Value;
            p["$sp"].Value = r.SourcePlugin;
            p["$wp"].Value = r.WinningPlugin;
            p["$m"].Value = string.Join(";", r.RequiredMasters);
            p["$f"].Value = r.MajorFlags;
            p["$sm"].Value = (object?)r.SourceMod ?? DBNull.Value;
            p["$d"].Value = (object?)r.DetailJson ?? DBNull.Value;
            cmd.ExecuteNonQuery();
            count++;
        }
        tx.Commit();
        _log.Information("Stored {Count} records in catalogue", count);
        return count;
    }

    public long ReplaceAssets(IEnumerable<AssetFile> assets)
    {
        using var tx = _conn.BeginTransaction();
        Exec("DELETE FROM assets", tx);
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO assets(rel_path,container,container_name,source_mod,size)
            VALUES($p,$c,$cn,$sm,$s)
            """;
        var pp = cmd.CreateParameter(); pp.ParameterName = "$p"; cmd.Parameters.Add(pp);
        var pc = cmd.CreateParameter(); pc.ParameterName = "$c"; cmd.Parameters.Add(pc);
        var pcn = cmd.CreateParameter(); pcn.ParameterName = "$cn"; cmd.Parameters.Add(pcn);
        var psm = cmd.CreateParameter(); psm.ParameterName = "$sm"; cmd.Parameters.Add(psm);
        var psz = cmd.CreateParameter(); psz.ParameterName = "$s"; cmd.Parameters.Add(psz);
        long count = 0;
        foreach (var a in assets)
        {
            pp.Value = a.RelPath;
            pc.Value = (int)a.Container;
            pcn.Value = a.ContainerName;
            psm.Value = (object?)a.SourceMod ?? DBNull.Value;
            psz.Value = a.Size;
            cmd.ExecuteNonQuery();
            count++;
        }
        tx.Commit();
        _log.Information("Stored {Count} asset entries in catalogue", count);
        return count;
    }

    /// <summary>True when an asset exists at the Data-relative path (loose wins over BSA is irrelevant for existence).</summary>
    public bool AssetExists(string relPath)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM assets WHERE rel_path=$p LIMIT 1";
        cmd.Parameters.AddWithValue("$p", NormalizeRelPath(relPath));
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>True when any asset path starts with the given Data-relative prefix (folder check).</summary>
    public bool AssetPathPrefixExists(string prefix)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM assets WHERE rel_path LIKE $p LIMIT 1";
        cmd.Parameters.AddWithValue("$p", NormalizeRelPath(prefix) + "%");
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>Full asset record (loose preferred over BSA), or null if absent.</summary>
    public AssetFile? GetAsset(string relPath)
    {
        using var cmd = _conn.CreateCommand();
        // Prefer loose (container 0) over BSA (container 1) when both exist.
        cmd.CommandText =
            "SELECT rel_path,container,container_name,source_mod,size FROM assets WHERE rel_path=$p ORDER BY container LIMIT 1";
        cmd.Parameters.AddWithValue("$p", NormalizeRelPath(relPath));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new AssetFile
        {
            RelPath = reader.GetString(0),
            Container = (AssetContainerKind)reader.GetInt32(1),
            ContainerName = reader.GetString(2),
            SourceMod = reader.IsDBNull(3) ? null : reader.GetString(3),
            Size = reader.GetInt64(4),
        };
    }

    public IReadOnlyList<IndexedRecord> SearchRecords(
        IndexedRecordType? type, string? text, int limit = 50, string? plugin = null)
    {
        using var cmd = _conn.CreateCommand();
        var where = new List<string>();
        if (type is not null)
        {
            where.Add("type=$t");
            cmd.Parameters.AddWithValue("$t", type.ToString());
        }
        if (!string.IsNullOrWhiteSpace(text))
        {
            where.Add("(editor_id LIKE $q OR display_name LIKE $q OR form_key LIKE $q OR winning_plugin LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{text}%");
        }
        if (!string.IsNullOrWhiteSpace(plugin))
        {
            // Match by either the winning plugin or the defining (source) plugin.
            where.Add("(winning_plugin LIKE $pl OR source_plugin LIKE $pl)");
            cmd.Parameters.AddWithValue("$pl", $"%{plugin}%");
        }
        cmd.CommandText =
            $"SELECT form_key,type,editor_id,display_name,source_plugin,winning_plugin,masters,flags,source_mod,detail FROM records" +
            (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
            " ORDER BY editor_id LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);

        var result = new List<IndexedRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new IndexedRecord
            {
                FormKey = reader.GetString(0),
                Type = Enum.Parse<IndexedRecordType>(reader.GetString(1)),
                EditorId = reader.IsDBNull(2) ? null : reader.GetString(2),
                DisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                SourcePlugin = reader.GetString(4),
                WinningPlugin = reader.GetString(5),
                RequiredMasters = reader.GetString(6).Split(';', StringSplitOptions.RemoveEmptyEntries),
                MajorFlags = (uint)reader.GetInt64(7),
                SourceMod = reader.IsDBNull(8) ? null : reader.GetString(8),
                DetailJson = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }
        return result;
    }

    /// <summary>Vortex staging mod that supplied a plugin, or null if unknown/base game.</summary>
    public string? GetPluginSourceMod(string pluginName)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT source_mod FROM plugins WHERE name=$n LIMIT 1";
        cmd.Parameters.AddWithValue("$n", pluginName);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>Looks up a single record by FormKey (any type) for provenance/appearance evaluation.</summary>
    public IndexedRecord? GetRecord(string formKey)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT form_key,type,editor_id,display_name,source_plugin,winning_plugin,masters,flags,source_mod,detail " +
            "FROM records WHERE form_key=$fk LIMIT 1";
        cmd.Parameters.AddWithValue("$fk", formKey);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new IndexedRecord
        {
            FormKey = reader.GetString(0),
            Type = Enum.Parse<IndexedRecordType>(reader.GetString(1)),
            EditorId = reader.IsDBNull(2) ? null : reader.GetString(2),
            DisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
            SourcePlugin = reader.GetString(4),
            WinningPlugin = reader.GetString(5),
            RequiredMasters = reader.GetString(6).Split(';', StringSplitOptions.RemoveEmptyEntries),
            MajorFlags = (uint)reader.GetInt64(7),
            SourceMod = reader.IsDBNull(8) ? null : reader.GetString(8),
            DetailJson = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }

    public long CountRecords(IndexedRecordType? type = null)
    {
        using var cmd = _conn.CreateCommand();
        if (type is null)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM records";
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM records WHERE type=$t";
            cmd.Parameters.AddWithValue("$t", type.ToString());
        }
        return (long)cmd.ExecuteScalar()!;
    }

    public static string NormalizeRelPath(string relPath) =>
        relPath.Replace('/', '\\').TrimStart('\\').ToLowerInvariant();

    private void Exec(string sql, SqliteTransaction? tx = null)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
