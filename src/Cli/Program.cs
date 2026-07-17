using System.Text.Json;
using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.Cli;

/// <summary>
/// fforge — the Follower Forge command line. Exposes the same engine the UI uses,
/// so every build is reproducible and scriptable.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    public static int Main(string[] args)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FollowerForge", "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(logDir, "fforge-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Command failed");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        var options = ParseOptions(args.Skip(1));
        return args[0].ToLowerInvariant() switch
        {
            "env" => CmdEnv(options),
            "index" => CmdIndex(options),
            "search" => CmdSearch(options),
            _ => Unknown(args[0]),
        };
    }

    private static int CmdEnv(Dictionary<string, string> opts)
    {
        var discovery = new VortexDiscovery(Log.Logger);
        var env = discovery.Discover(opts.GetValueOrDefault("game-path"));

        Console.WriteLine();
        Console.WriteLine("=== Follower Forge — Environment ===");
        Console.WriteLine($"Game root        : {env.GameRootPath}");
        Console.WriteLine($"Vortex (game)    : {env.VortexGamePath}");
        Console.WriteLine($"Staging mods     : {env.StagingPath}  ({env.StagingModCount} mods)");
        Console.WriteLine($"Active profile   : {env.ActiveProfileId}  ({env.ActiveProfileReason})");
        Console.WriteLine($"Deployment       : {env.DeploymentMethod}, {DateTimeOffset.FromUnixTimeMilliseconds(env.DeploymentTimeUtcMs):yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine($"Enabled plugins  : {env.EnabledPluginCount} (load order {env.LoadOrderCount})");

        var chargen = new CharGenDiscovery(Log.Logger).Discover(env.GameDataPath);
        Console.WriteLine($"CharGen exports  : {chargen.Count} ({chargen.Count(c => c.JslotPath != null)} with preset, {chargen.Count(c => c.TintDdsPath != null)} with tint)");

        foreach (var w in env.Warnings)
            Console.WriteLine($"WARNING: {w}");

        if (opts.TryGetValue("json", out var jsonPath) && jsonPath.Length > 0)
        {
            var guard = VortexDiscovery.CreateGuard(env);
            guard.EnsureWritable(jsonPath);
            var payload = new { Environment = env, CharGenExports = chargen };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, JsonOpts));
            Console.WriteLine($"Report written: {jsonPath}");
        }
        return env.Warnings.Count == 0 ? 0 : 3;
    }

    private static int CmdIndex(Dictionary<string, string> opts)
    {
        var discovery = new VortexDiscovery(Log.Logger);
        var env = discovery.Discover(opts.GetValueOrDefault("game-path"));
        var dbPath = opts.GetValueOrDefault("db") ?? CatalogBuilder.DefaultDbPath;

        if (opts.ContainsKey("if-stale") && CatalogBuilder.IsFresh(env, dbPath))
        {
            Console.WriteLine("Catalogue is up to date with the current deployment; nothing to do.");
            return 0;
        }

        var summary = new CatalogBuilder(Log.Logger).Build(env, dbPath);
        Console.WriteLine($"Indexed {summary.Records} records and {summary.Assets} assets " +
                          $"from {summary.Plugins} plugins in {summary.Elapsed:mm\\:ss}.");
        Console.WriteLine($"Catalogue: {dbPath}");
        return 0;
    }

    private static int CmdSearch(Dictionary<string, string> opts)
    {
        var dbPath = opts.GetValueOrDefault("db") ?? CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"No catalogue at {dbPath} — run 'fforge index' first.");
            return 4;
        }

        IndexedRecordType? type = null;
        if (opts.TryGetValue("type", out var typeText))
        {
            if (!TryParseType(typeText, out var parsed))
            {
                Console.Error.WriteLine(
                    $"Unknown --type '{typeText}'. Valid: {string.Join(", ", Enum.GetNames<IndexedRecordType>())}");
                return 2;
            }
            type = parsed;
        }

        var limit = opts.TryGetValue("limit", out var limText) && int.TryParse(limText, out var lim) ? lim : 50;
        using var db = new CatalogDb(dbPath, Log.Logger);
        var results = db.SearchRecords(type, opts.GetValueOrDefault("text"), limit);
        foreach (var r in results)
        {
            Console.WriteLine($"{r.Type,-12} {r.FormKey,-40} {r.EditorId ?? "-",-40} " +
                              $"{r.DisplayName ?? "-",-30} win={r.WinningPlugin}");
        }
        Console.WriteLine($"{results.Count} result(s).");
        return 0;
    }

    /// <summary>Accepts enum names and common signatures (csty, npc_, vtyp…).</summary>
    private static bool TryParseType(string text, out IndexedRecordType type)
    {
        if (Enum.TryParse(text, ignoreCase: true, out type)) return true;
        (bool ok, IndexedRecordType t) = text.ToLowerInvariant() switch
        {
            "npc_" or "npc" => (true, IndexedRecordType.Npc),
            "csty" => (true, IndexedRecordType.CombatStyle),
            "clas" => (true, IndexedRecordType.Class),
            "vtyp" or "voice" => (true, IndexedRecordType.VoiceType),
            "hdpt" => (true, IndexedRecordType.HeadPart),
            "otft" => (true, IndexedRecordType.Outfit),
            "armo" => (true, IndexedRecordType.Armor),
            "arma" => (true, IndexedRecordType.ArmorAddon),
            "weap" => (true, IndexedRecordType.Weapon),
            "spel" => (true, IndexedRecordType.Spell),
            "kywd" => (true, IndexedRecordType.Keyword),
            "fact" => (true, IndexedRecordType.Faction),
            "rela" => (true, IndexedRecordType.Relationship),
            "lctn" => (true, IndexedRecordType.Location),
            "txst" => (true, IndexedRecordType.TextureSet),
            "flst" => (true, IndexedRecordType.FormList),
            "pack" => (true, IndexedRecordType.Package),
            "race" => (true, IndexedRecordType.Race),
            "cell" => (true, IndexedRecordType.Cell),
            "perk" => (true, IndexedRecordType.Perk),
            _ => (false, default),
        };
        type = t;
        return ok;
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                if (key is not null) result[key] = "";
                key = arg[2..];
            }
            else if (key is not null)
            {
                result[key] = arg;
                key = null;
            }
        }
        if (key is not null) result[key] = "";
        return result;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command '{cmd}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            fforge — Follower Forge CLI

            Commands:
              env     [--game-path DIR] [--json FILE]   Environment discovery + diagnostic report
              index   [--game-path DIR] [--db FILE] [--if-stale]
                                                        Build the modpack catalogue (records + assets)
              search  [--type TYPE] [--text QUERY] [--limit N] [--db FILE]
                                                        Search the catalogue
            """);
    }
}
