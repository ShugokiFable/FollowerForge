using System.Text.Json;
using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Cli;

/// <summary>
/// fforge — the FollowerForge command line. Exposes the same engine the UI uses,
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
            "build" => CmdBuild(options),
            "batch" => CmdBatch(options),
            "hub" => CmdHub(options),
            "detect" => CmdDetect(options),
            "inspect" => CmdInspect(options),
            "locations" => CmdLocations(options),
            "assets" => CmdAssets(options),
            "races" => CmdRaces(options),
            "hubs" => CmdHubs(options),
            "faces" => CmdFaces(options),
            "voices" => CmdVoices(options),
            "voice-coverage" => CmdVoiceCoverage(options),
            "sample-profile" => CmdSampleProfile(options),
            _ => Unknown(args[0]),
        };
    }

    private static int CmdBuild(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("profile", out var profilePath) || !File.Exists(profilePath))
        {
            Console.Error.WriteLine("--profile <file.json> is required (see 'FollowerForge.Cli.exe sample-profile').");
            return 2;
        }

        var profile = BuildPipeline.ProfileIo.Load(profilePath);
        // --face <ExportName> overrides the profile's CharGen appearance for a quick FaceGen build.
        if (opts.TryGetValue("face", out var faceName) && faceName.Length > 0)
            profile = profile with { Appearance = profile.Appearance with { CharGenExportName = faceName } };
        var env = DiscoverEnvironment(opts);
        var workspace = opts.GetValueOrDefault("out")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge", "workspace");

        CatalogDb? catalog = null;
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        if (File.Exists(dbPath)) catalog = new CatalogDb(dbPath, Log.Logger);

        try
        {
            var result = new BuildPipeline.FollowerBuilder(Log.Logger)
                .Build(profile, env, workspace, location: null, catalog);

            Console.WriteLine();
            Console.WriteLine($"Follower : {result.Manifest.Name}");
            Console.WriteLine($"Plugin   : {result.Manifest.PluginName}  NPC {result.Manifest.NpcFormKey}");
            Console.WriteLine($"Output   : {result.OutputDirectory}");
            Console.WriteLine($"Masters  : {string.Join(", ", result.Manifest.Masters)}");
            Console.WriteLine($"FaceGen  : {(result.Manifest.HasFaceGen ? "yes" : "no")}");
            foreach (var f in result.Validation.Findings)
                Console.WriteLine($"  [{f.Severity}] {f.Code}: {f.Message}");
            if (result.Success && opts.ContainsKey("zip"))
            {
                var zip = new BuildPipeline.VortexPackager(Log.Logger)
                    .Package(result.OutputDirectory, Path.GetFileNameWithoutExtension(profile.PluginName));
                Console.WriteLine($"Package  : {zip}");
            }
            if (result.Success && opts.ContainsKey("verify-deterministic"))
            {
                var det = new BuildPipeline.DeterminismVerifier(Log.Logger).Verify(profile, env, location: null, catalog);
                Console.WriteLine($"Rebuild  : {(det.Identical ? "DETERMINISTIC (byte-identical)" : "NON-DETERMINISTIC!")}  {det.HashA[..16]}");
                if (!det.Identical) return 6;
            }
            Console.WriteLine(result.Success ? "BUILD OK (published)" : "BUILD FAILED (not published)");
            return result.Success ? 0 : 5;
        }
        finally
        {
            catalog?.Dispose();
        }
    }

    private static int CmdBatch(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("profiles", out var profilesPath))
        {
            Console.Error.WriteLine("--profiles <file-or-dir> is required.");
            return 2;
        }
        var profiles = BuildPipeline.BatchBuilder.LoadProfiles(profilesPath);
        if (profiles.Count == 0) { Console.Error.WriteLine("No profiles found."); return 4; }

        var env = DiscoverEnvironment(opts);
        var workspace = opts.GetValueOrDefault("out")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge", "workspace");
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        CatalogDb? catalog = File.Exists(dbPath) ? new CatalogDb(dbPath, Log.Logger) : null;

        try
        {
            var result = new BuildPipeline.BatchBuilder(Log.Logger).Build(profiles, env, workspace, catalog);
            Console.WriteLine();
            Console.WriteLine($"=== Batch: {result.Succeeded}/{result.Items.Count} succeeded ===");
            foreach (var item in result.Items)
                Console.WriteLine($"  [{(item.Success ? "OK " : "FAIL")}] {item.Name}  " +
                                  $"({item.Errors} err, {item.Warnings} warn)  {item.OutputDirectory}");
            return result.Failed == 0 ? 0 : 5;
        }
        finally { catalog?.Dispose(); }
    }

    /// <summary>
    /// locations --scan            rebuild the library from the installed mods
    /// locations [--text QUERY]    browse it by name / area / source mod
    /// </summary>
    private static int CmdLocations(Dictionary<string, string> opts)
    {
        var libPath = opts.GetValueOrDefault("file") ?? BuildPipeline.LocationLibraryBuilder.DefaultPath;

        if (opts.ContainsKey("scan"))
        {
            var env = DiscoverEnvironment(opts);
            var built = new BuildPipeline.LocationLibraryBuilder(Log.Logger)
                .Build(env, libPath, opts.GetValueOrDefault("db"));
            Console.WriteLine($"Scanned {built.ScannedPlugins} plugins → {built.Locations.Count} spawn locations.");
            Console.WriteLine($"Library: {libPath}");
        }

        var library = BuildPipeline.LocationLibraryBuilder.Load(libPath);
        if (library is null)
        {
            Console.Error.WriteLine($"No location library at {libPath} — run 'FollowerForge.Cli.exe locations --scan' first.");
            return 4;
        }

        var limit = opts.TryGetValue("limit", out var lt) && int.TryParse(lt, out var l) ? l : 40;
        var results = BuildPipeline.LocationLibraryBuilder.Search(library, opts.GetValueOrDefault("text"), limit);
        Console.WriteLine();
        Console.WriteLine($"{"PLACE",-46} {"KIND",-9} {"USED BY",-8} REQUIRES");
        foreach (var loc in results)
            Console.WriteLine($"{Trim(loc.Display, 46),-46} {loc.Kind,-9} {loc.Popularity,-8} {loc.RequiredPlugin}");
        Console.WriteLine($"\n{results.Count} of {library.Locations.Count} places.");
        return 0;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>assets --path textures\...\x.dds — is this file actually in the load order?</summary>
    private static int CmdAssets(Dictionary<string, string> opts)
    {
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"No catalogue at {dbPath} — run 'FollowerForge.Cli.exe index' first.");
            return 4;
        }
        if (!opts.TryGetValue("path", out var rel) || rel.Length == 0)
        {
            Console.Error.WriteLine(@"--path ""textures\actors\character\...\file.dds"" is required.");
            return 2;
        }

        using var db = new CatalogDb(dbPath, Log.Logger);
        var asset = db.GetAsset(rel);
        if (asset is null)
        {
            Console.WriteLine($"NOT FOUND: {rel}");
            return 1;
        }
        var where = asset.Container == Domain.AssetContainerKind.Loose
            ? $"loose file from \"{asset.SourceMod ?? "?"}\""
            : $"inside archive {asset.ContainerName}";
        Console.WriteLine($"FOUND: {asset.RelPath}\n  {where}");
        return 0;
    }

    /// <summary>races — exactly what the wizard offers, in the order it offers them.</summary>
    /// <summary>voices — which follower voices can have custom lines synthesised on this PC.</summary>
    /// <summary>
    /// Shows what dialogue a follower inherits for free from her voice type — RDO and anything
    /// else installed that keys lines to a voice rather than to a specific NPC.
    /// </summary>
    private static int CmdVoiceCoverage(Dictionary<string, string> opts)
    {
        if (opts.ContainsKey("scan"))
        {
            var env = DiscoverEnvironment(opts);
            var plugins = new LoadOrderBuilder(Log.Logger).BuildEntryList(env).Entries
                .Where(e => e.Enabled).Select(e => e.PluginFileName);
            var built = new VoiceCoverageScanner(Log.Logger).Scan(env.GameDataPath, plugins);
            VoiceCoverageScanner.Save(built);
            Console.WriteLine($"Scanned {built.PluginsScanned} plugins → {built.Voices.Count} voice types with extra dialogue.");
        }

        var library = VoiceCoverageScanner.Load();
        if (library is null)
        {
            Console.Error.WriteLine("No voice-coverage data — run 'FollowerForge.Cli.exe voice-coverage --scan' first.");
            return 4;
        }

        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(dbPath))
        {
            using var db = new CatalogDb(dbPath, Log.Logger);
            foreach (var v in db.SearchRecords(Domain.IndexedRecordType.VoiceType, null, 20000))
                if (v.EditorId is { Length: > 0 }) names[v.FormKey] = v.EditorId;
        }

        var filter = opts.GetValueOrDefault("text");
        Console.WriteLine($"\nScanned {library.PluginsScanned} plugins on {library.BuiltUtc:yyyy-MM-dd}.\n");
        var shown = 0;
        foreach (var voice in library.Voices)
        {
            var name = names.GetValueOrDefault(voice.VoiceFormKey, voice.VoiceFormKey);
            if (filter is { Length: > 0 } && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            if (++shown > 25) break;
            Console.WriteLine($"{name,-32} +{voice.TotalLines,5} lines from {voice.Contributions.Count} mod(s)");
            foreach (var c in voice.Contributions.Take(4))
                Console.WriteLine($"{"",34}{c.Lines,5}  {c.Plugin}");
        }
        return 0;
    }

    private static int CmdVoices(Dictionary<string, string> opts)
    {
        var catalog = new VoiceModelCatalog(opts.GetValueOrDefault("xvasynth"));
        Console.WriteLine();
        Console.WriteLine($"xVASynth : {(catalog.Installed ? catalog.Root : "NOT FOUND — custom lines unavailable")}");
        Console.WriteLine($"models   : {catalog.Models.Count}");
        Console.WriteLine($"lip/fuz  : {(catalog.CanMakeFuz ? "ready (FaceFXWrapper + xWMAEncode + fuz_extractor)" : "MISSING — lines would be silent")}");
        if (!catalog.Installed) return 1;

        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"\nNo catalogue at {dbPath} — run 'FollowerForge.Cli.exe index' to cross-check your voice types.");
            return 4;
        }

        using var db = new CatalogDb(dbPath, Log.Logger);
        var voices = db.SearchRecords(Domain.IndexedRecordType.VoiceType, opts.GetValueOrDefault("text"), 20000)
            .Where(v => !string.IsNullOrWhiteSpace(v.EditorId))
            .ToList();

        var speakable = voices.Where(v => catalog.CanSpeak(v.EditorId)).ToList();
        Console.WriteLine($"\n{speakable.Count} of {voices.Count} voice types in your load order can speak custom lines:\n");
        foreach (var v in speakable.OrderBy(v => v.EditorId, StringComparer.OrdinalIgnoreCase)
                     .Take(opts.TryGetValue("limit", out var l) && int.TryParse(l, out var n) ? n : 40))
            Console.WriteLine($"  {Trim(v.EditorId!, 34),-34} model {catalog.ForVoiceType(v.EditorId)!.ModelId}");
        return 0;
    }

    /// <summary>faces — which RaceMenu exports the builder will actually accept, and why not.</summary>
    private static int CmdFaces(Dictionary<string, string> opts)
    {
        var env = DiscoverEnvironment(opts);
        var faces = new CharGenDiscovery(Log.Logger).Discover(env)
            .OrderBy(f => f.IsUsable ? 0 : 1)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine();
        foreach (var f in faces)
        {
            var verdict = f.Blocker is { } why ? $"CANNOT BUILD - {why}" : "ready";
            var notes = f.QualityNotes.Count > 0 ? "  |  " + string.Join("; ", f.QualityNotes.Take(2)) : "";
            Console.WriteLine($"  {(f.IsUsable ? "[ok]  " : "[fail]")} {Trim(f.Name, 34),-34} {verdict}{notes}");
        }
        Console.WriteLine($"\n{faces.Count(f => f.IsUsable)} of {faces.Count} exports are usable.");
        return 0;
    }

    private static int CmdRaces(Dictionary<string, string> opts)
    {
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"No catalogue at {dbPath} — run 'FollowerForge.Cli.exe index' first.");
            return 4;
        }
        using var db = new CatalogDb(dbPath, Log.Logger);
        var all = db.SearchRecords(Domain.IndexedRecordType.Race, null, 20000);
        var offered = SkyrimRecords.RaceSuitability.Offer(all);
        var limit = opts.TryGetValue("limit", out var lt) && int.TryParse(lt, out var l) ? l : 25;

        Console.WriteLine($"{all.Count} race records → {offered.Count} usable\n");
        foreach (var r in offered.Take(limit))
            Console.WriteLine($"  {r.Class,-15} {Trim(r.Name, 34),-34} {r.Note}");
        return 0;
    }

    /// <summary>hubs — which free modder's resources are installed, and what they really cover.</summary>
    private static int CmdHubs(Dictionary<string, string> opts)
    {
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        CatalogDb? db = File.Exists(dbPath) ? new CatalogDb(dbPath, Log.Logger) : null;
        try
        {
            Console.WriteLine();
            foreach (var hub in HubCatalog.Detect(db))
            {
                Console.WriteLine($"{(hub.Installed ? "[installed]" : "[missing]  ")} {hub.Name}  by {hub.Author}");
                Console.WriteLine($"     covers     : {hub.Covering}");
                Console.WriteLine($"     does NOT   : {hub.NotCovering}");
                if (hub.Installed && hub.SourceMod is not null)
                    Console.WriteLine($"     from       : {hub.SourceMod}");
                Console.WriteLine($"     {hub.Url}");
                Console.WriteLine();
            }
            Console.WriteLine("Free hubs cover support maps and geometry only. Skin colour, hair and eye");
            Console.WriteLine("textures still come from your own mods, or from your own hub if you have");
            Console.WriteLine("permission to redistribute them.");
            return 0;
        }
        finally { db?.Dispose(); }
    }

    private static int CmdDetect(Dictionary<string, string> opts)
    {
        var env = DiscoverEnvironment(opts);
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;

        var (entries, _) = new SkyrimRecords.LoadOrderBuilder(Log.Logger).BuildEntryList(env);
        var enabled = entries.Where(e => e.Enabled).Select(e => e.PluginFileName).ToList();

        CatalogDb? db = File.Exists(dbPath) ? new CatalogDb(dbPath, Log.Logger) : null;
        // Detection uses folder-prefix checks (e.g. BodySlide's CalienteTools\BodySlide tree).
        Func<string, bool> assetExists = db is null ? _ => false : db.AssetPathPrefixExists;
        var stagingMods = Directory.Exists(env.StagingPath)
            ? Directory.EnumerateDirectories(env.StagingPath).Select(Path.GetFileName).Where(n => n != null).Select(n => n!)
            : [];

        var report = SkyrimRecords.Detection.Detect(enabled, assetExists, stagingMods);
        db?.Dispose();

        Console.WriteLine();
        Console.WriteLine("=== Follower frameworks ===");
        if (report.Frameworks.Count == 0) Console.WriteLine("  (none detected)");
        foreach (var f in report.Frameworks)
            Console.WriteLine($"  {f.Framework}  [{f.Plugin}]  — {f.Integration}");
        Console.WriteLine("=== Body systems ===");
        if (report.BodySystems.Count == 0) Console.WriteLine("  (none detected)");
        foreach (var b in report.BodySystems)
            Console.WriteLine($"  {b.System}  ({b.Evidence})");
        Console.WriteLine(report.Guidance);
        return 0;
    }

    private static int CmdInspect(Dictionary<string, string> opts)
    {
        var dbPath = opts.GetValueOrDefault("db") ?? BuildPipeline.CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"No catalogue at {dbPath} — run 'FollowerForge.Cli.exe index' first.");
            return 4;
        }
        if (!opts.TryGetValue("formkey", out var fk))
        {
            Console.Error.WriteLine("--formkey <123ABC:Plugin.esp> is required.");
            return 2;
        }
        using var db = new CatalogDb(dbPath, Log.Logger);
        var rec = db.GetRecord(fk);
        if (rec is null) { Console.Error.WriteLine("Record not found."); return 4; }

        Console.WriteLine($"{rec.Type} {rec.FormKey}");
        Console.WriteLine($"  EditorID : {rec.EditorId}");
        Console.WriteLine($"  Name     : {rec.DisplayName}");
        Console.WriteLine($"  Source   : {rec.SourcePlugin}  (mod: {rec.SourceMod ?? "?"})");
        Console.WriteLine($"  Winning  : {rec.WinningPlugin}");
        Console.WriteLine($"  Masters  : {string.Join(", ", rec.RequiredMasters)}");
        if (rec.DetailJson is not null)
        {
            Console.WriteLine("  Detail   :");
            using var doc = System.Text.Json.JsonDocument.Parse(rec.DetailJson);
            Console.WriteLine("    " + System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\n", "\n    "));
        }

        // Voice types: verify voice files on disk now that the asset index is available.
        if (rec.Type == IndexedRecordType.VoiceType && rec.EditorId is not null)
        {
            var voiceDir = $@"sound\voice\{rec.SourcePlugin}\{rec.EditorId}";
            var present = db.AssetPathPrefixExists(voiceDir);
            Console.WriteLine($"  Voice files ({voiceDir}\\): {(present ? "PRESENT" : "not found")}");
        }
        return 0;
    }

    private static int CmdHub(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("name", out var hubName) || hubName.Length == 0)
        {
            Console.Error.WriteLine("--name <HubName> is required.");
            return 2;
        }
        var env = DiscoverEnvironment(opts);
        var workspace = opts.GetValueOrDefault("out")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge", "workspace");

        var result = new BuildPipeline.HubBuilder(Log.Logger).Build(hubName, env, workspace);
        Console.WriteLine();
        Console.WriteLine($"Hub      : {hubName}");
        Console.WriteLine($"Plugin   : {Path.GetFileName(result.PluginPath)}");
        Console.WriteLine($"Output   : {result.OutputDirectory}");
        foreach (var f in result.Validation.Findings)
            Console.WriteLine($"  [{f.Severity}] {f.Code}: {f.Message}");
        Console.WriteLine(result.Success ? "HUB OK (published)" : "HUB FAILED");
        return result.Success ? 0 : 5;
    }

    private static int CmdSampleProfile(Dictionary<string, string> opts)
    {
        var path = opts.GetValueOrDefault("out") ?? "follower-profile.sample.json";
        var sample = new FollowerProfile
        {
            Name = "Aria Forge",
            PluginName = "FF_AriaForge.esp",
            Race = new RecordRef("013746:Skyrim.esm"),        // NordRace
            Female = true,
            VoiceType = new RecordRef("013ADD:Skyrim.esm"),   // FemaleEvenToned
            Class = new RecordRef("013176:Skyrim.esm"),       // CombatWarrior1H
            Outfit = new RecordRef("01DC10:Skyrim.esm"),      // FarmClothesOutfit
            Placement = new PlacementSpec { Cell = new RecordRef("01A270:Skyrim.esm") }, // Whiterun exterior
            Ai = new AiValues { Aggression = 0, Confidence = 3, Assistance = 2, Morality = 0, Energy = 50 },
            Level = new LevelScaling { ScaleWithPlayer = true, PlayerLevelMult = 1.0, MinLevel = 6, MaxLevel = 0 },
            Protected = true,
        };
        BuildPipeline.ProfileIo.Save(path, sample);
        Console.WriteLine($"Sample profile written: {path}");
        Console.WriteLine("Edit it, then: FollowerForge.Cli.exe build --profile " + path);
        return 0;
    }

    private static int CmdEnv(Dictionary<string, string> opts)
    {
        var env = DiscoverEnvironment(opts);

        Console.WriteLine();
        Console.WriteLine("=== FollowerForge — Environment ===");
        Console.WriteLine($"Manager          : {env.ManagerLabel}");
        Console.WriteLine($"Game root        : {env.GameRootPath}");
        Console.WriteLine($"Instance         : {env.InstancePath}");
        Console.WriteLine($"Mods / staging   : {env.StagingPath}  ({env.StagingModCount} mods)");
        Console.WriteLine($"Active profile   : {env.ActiveProfileId}  ({env.ActiveProfileReason})");
        Console.WriteLine($"Deployment       : {env.DeploymentMethod}, {DateTimeOffset.FromUnixTimeMilliseconds(env.DeploymentTimeUtcMs):yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine($"Enabled plugins  : {env.EnabledPluginCount} (load order {env.LoadOrderCount})");
        if (env.Manager == Domain.ModManagerKind.Mo2)
            Console.WriteLine($"Plugin view      : {env.PluginDataPath}");

        var chargen = new CharGenDiscovery(Log.Logger).Discover(env);
        Console.WriteLine($"CharGen exports  : {chargen.Count} ({chargen.Count(c => c.JslotPath != null)} with preset, {chargen.Count(c => c.TintDdsPath != null)} with tint)");

        foreach (var w in env.Warnings)
            Console.WriteLine($"WARNING: {w}");

        if (opts.TryGetValue("json", out var jsonPath) && jsonPath.Length > 0)
        {
            var guard = EnvironmentDiscovery.CreateGuard(env);
            guard.EnsureWritable(jsonPath);
            var payload = new { Environment = env, CharGenExports = chargen };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, JsonOpts));
            Console.WriteLine($"Report written: {jsonPath}");
        }
        return env.Warnings.Count == 0 ? 0 : 3;
    }

    private static int CmdIndex(Dictionary<string, string> opts)
    {
        var env = DiscoverEnvironment(opts);
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
            Console.Error.WriteLine($"No catalogue at {dbPath} — run 'FollowerForge.Cli.exe index' first.");
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
        var results = db.SearchRecords(type, opts.GetValueOrDefault("text"), limit,
            opts.GetValueOrDefault("plugin"));
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

    private static EnvironmentSnapshot DiscoverEnvironment(Dictionary<string, string> opts) =>
        new EnvironmentDiscovery(Log.Logger).Discover(
            opts.GetValueOrDefault("game-path"),
            opts.GetValueOrDefault("mo2-instance"),
            mo2ProfileOverride: opts.GetValueOrDefault("mo2-profile"));

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
            FollowerForge.Cli — FollowerForge CLI

            Commands:
              env     [--game-path DIR] [--mo2-instance DIR] [--mo2-profile NAME] [--json FILE]
              index   [--game-path DIR] [--mo2-instance DIR] [--mo2-profile NAME] [--db FILE] [--if-stale]
                                                        Build the modpack catalogue (records + assets)
              search  [--type TYPE] [--text QUERY] [--plugin NAME] [--limit N] [--db FILE]
                                                        Search the catalogue
              detect  [--game-path DIR] [--db FILE]      Detect follower frameworks + body systems
              inspect --formkey KEY [--db FILE]         Show a record's provenance + analysis detail
              hub     --name NAME [--out DIR] [--game-path DIR]
                                                        Build a shared follower asset hub (light master)
              sample-profile [--out FILE]               Write a starter follower profile JSON
              build   --profile FILE [--face EXPORT] [--out DIR] [--game-path DIR] [--db FILE]
                      [--zip] [--verify-deterministic]
                                                        Compile + validate + publish a follower
                                                        (--face uses a RaceMenu CharGen export by name)
              batch   --profiles FILE|DIR [--out DIR] [--game-path DIR] [--db FILE]
                                                        Build every follower profile in a folder
            """);
    }
}
