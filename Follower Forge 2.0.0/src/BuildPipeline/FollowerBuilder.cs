using System.Text.Json;
using System.Text.Json.Serialization;
using FollowerForge.AssetIndex;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using FollowerForge.Validation;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace FollowerForge.BuildPipeline;

/// <summary>
/// Atomic follower build: compile → write to a private staging dir → validate (ship-gate +
/// Mutagen reopen) → emit manifests → publish to the workspace. Nothing is written outside the
/// workspace; game/staging are read-only and guarded.
/// </summary>
public sealed class FollowerBuilder(ILogger log)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public sealed record BuildResult(
        bool Success,
        string OutputDirectory,
        string PluginPath,
        FollowerManifest Manifest,
        ValidationReport Validation);

    /// <param name="workspaceRoot">Root of the Follower Forge workspace (must be writable).</param>
    /// <param name="location">
    /// Explicit spawn point. Normally left null: the builder resolves
    /// <see cref="PlacementSpec.LocationId"/> from the location library.
    /// </param>
    public BuildResult Build(
        FollowerProfile profile,
        EnvironmentSnapshot env,
        string workspaceRoot,
        SpawnLocation? location = null,
        CatalogDb? catalog = null)
    {
        var guard = VortexDiscovery.CreateGuard(env);
        guard.EnsureWritable(workspaceRoot);

        var report = new ValidationReport();
        var buildId = DeterministicBuildId(profile);
        var staging = Path.Combine(workspaceRoot, ".staging", buildId);
        var finalDir = Path.Combine(workspaceRoot, "builds", SafeName(profile.Name));

        // Clean staging (idempotent, deterministic).
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);

        // 1. Compile. Resolve the source combat style first when the profile asks to clone it,
        //    keeping the source plugin open until compilation has duplicated the record.
        var compiler = new FollowerCompiler(log);
        using var resolver = new RecordResolver(env.GameDataPath, log);
        ICombatStyleGetter? cstyToClone = null;
        if (profile.CombatStyle is { CloneIntoPlugin: true } cs)
        {
            cstyToClone = resolver.ResolveCombatStyle(cs.Style.FormKey);
            if (cstyToClone is null)
                report.Add(ValidationSeverity.Warning, "CSTY_CLONE_UNRESOLVED",
                    $"Combat style {cs.Style.FormKey} could not be opened for cloning; referencing it instead.");
        }
        location ??= ResolveLocation(profile, report);

        // Fetch the target cell's own definition so the placement override reproduces its
        // lighting/flags instead of blanking them.
        using var cellSources = new CellSourceResolver(env.GameDataPath, log);
        ICellGetter? cellSource = location is not null
            ? cellSources.Resolve(Mutagen.Bethesda.Plugins.FormKey.Factory(location.CellFormKey))
            : null;
        if (location is not null && cellSource is null)
            report.Add(ValidationSeverity.Warning, "LOCATION_CELL_UNREADABLE",
                $"Could not read '{location.Display}' from {location.RequiredPlugin}; " +
                "the placement override may not carry the room's lighting.");

        var compiled = compiler.Compile(profile, location, cstyToClone, cellSource);

        // 2. Write plugin into the package's Data-relative root.
        var pluginRel = profile.PluginName;
        var stagedPlugin = Path.Combine(staging, pluginRel);
        var writer = new PluginWriter(log);
        writer.Write(compiled.Mod, stagedPlugin);

        // 3. Validate: ship-gate header rules + Mutagen reopen.
        //    Masters are computed by Mutagen at write time, so read them back from the file.
        EspHeaderValidator.Validate(stagedPlugin, report, requireEsl: true);
        IReadOnlyList<string> masters = compiled.Masters;
        try
        {
            var reopened = writer.Reopen(stagedPlugin);
            masters = reopened.Masters;
            if (reopened.NpcCount < 1)
                report.Add(ValidationSeverity.Error, "REOPEN_NPC", "Reopened plugin has no NPC records");
            if (!reopened.IsLight)
                report.Add(ValidationSeverity.Error, "REOPEN_ESL", "Reopened plugin is not ESL-flagged");
        }
        catch (Exception ex)
        {
            report.Add(ValidationSeverity.Error, "REOPEN_FAIL", $"Mutagen could not reopen the plugin: {ex.Message}");
        }

        // 4. Follower-correctness validation.
        FollowerValidator.Validate(compiled, profile, report);
        ValidateVoiceCapability(profile, report, catalog);
        ReportSharingRequirements(masters, report, catalog);

        // 5. FaceGen dirty-swap (optional — only when the profile names a CharGen export).
        var faceGen = RunFaceGen(profile, compiled, env, staging, report, catalog);

        // 5b. Appearance assets: swap in free modder's resources, or copy into the author's own
        //     hub (which requires a written redistribution declaration).
        var hubResult = ApplyHubAssets(profile, faceGen, env, staging, report, catalog);

        // 6. Manifests (using the written master list).
        var manifest = BuildManifest(profile, compiled, masters, faceGen?.Result);
        WriteJson(Path.Combine(staging, "manifest.json"), manifest);
        WriteJson(Path.Combine(staging, "rebuild-profile.json"), profile);
        WriteJson(Path.Combine(staging, "source-assets.json"), BuildSourceAssets(profile, faceGen, hubResult));
        WriteJson(Path.Combine(staging, "dependency-report.json"), BuildDependencyReport(profile, masters, faceGen, catalog));
        File.WriteAllText(Path.Combine(staging, "credits.md"), BuildCredits(profile, masters, faceGen));
        File.WriteAllText(Path.Combine(staging, "build-report.html"), BuildReportHtml(profile, manifest, report));

        // 6. Publish atomically only if valid.
        if (report.HasErrors)
        {
            log.Error("Build for {Name} FAILED validation; not publishing.", profile.Name);
            return new BuildResult(false, staging, stagedPlugin, manifest, report);
        }

        if (Directory.Exists(finalDir)) Directory.Delete(finalDir, recursive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(finalDir)!);
        Directory.Move(staging, finalDir);
        var publishedPlugin = Path.Combine(finalDir, pluginRel);
        log.Information("Published follower {Name} → {Dir}", profile.Name, finalDir);
        return new BuildResult(true, finalDir, publishedPlugin, manifest, report);
    }

    /// <summary>
    /// Turns the profile's LocationId into a real spawn point. A missing library or unknown id
    /// is reported, never silently ignored — otherwise the follower would quietly appear in
    /// Whiterun instead of where the user chose.
    /// </summary>
    private SpawnLocation? ResolveLocation(FollowerProfile profile, ValidationReport report)
    {
        var id = profile.Placement.LocationId;
        if (string.IsNullOrWhiteSpace(id)) return null;

        var library = LocationLibraryBuilder.Load();
        if (library is null)
        {
            report.Add(ValidationSeverity.Error, "LOCATION_NO_LIBRARY",
                $"Profile asks for location '{id}' but no library exists. Run 'fforge locations --scan'.");
            return null;
        }

        var match = library.Locations.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            report.Add(ValidationSeverity.Error, "LOCATION_UNKNOWN",
                $"Location '{id}' is not in the library ({library.Locations.Count} places known).");
            return null;
        }
        if (!match.Placeable)
        {
            report.Add(ValidationSeverity.Error, "LOCATION_NOT_PLACEABLE",
                $"'{match.Display}' is an outdoor grid cell; pick an interior or a persistent exterior spot.");
            return null;
        }
        return match;
    }

    /// <summary>
    /// Runs the appearance-asset stage and records what it did, including the author's
    /// redistribution declaration when they built their own hub.
    /// </summary>
    private HubAssetPackager.Result? ApplyHubAssets(
        FollowerProfile profile, FaceGenOutcome? faceGen, EnvironmentSnapshot env,
        string staging, ValidationReport report, CatalogDb? catalog)
    {
        if (faceGen?.Result.FaceGeomPath is not { } geomRel) return null;
        if (profile.Hub == HubMode.ReferenceInstalled) return null;

        var raceEditorId = catalog?.GetRecord(profile.Race.FormKey)?.EditorId;
        var result = new HubAssetPackager(log).Apply(
            profile.Hub,
            faceGeomPath: Path.Combine(staging, geomRel),
            stagingRoot: staging,
            raceEditorId: raceEditorId,
            ownHubPrefix: profile.OwnHubPrefix,
            redistributionPermission: profile.RedistributionPermission,
            env, catalog, report);

        if (profile.Hub == HubMode.FreeHubs && result.Retargeted > 0)
            report.Add(ValidationSeverity.Info, "HUB_FREE_USED",
                $"{result.Retargeted} skin map(s) now come from {string.Join(", ", result.HubsUsed)} " +
                "(a free modder's resource) instead of your own skin mod.");

        if (profile.Hub == HubMode.OwnHub)
        {
            File.WriteAllText(Path.Combine(staging, "PERMISSIONS.md"),
                HubAssetPackager.BuildPermissionsDocument(
                    profile.Name, profile.OwnHubPrefix, profile.RedistributionPermission, result.Assets));
            if (result.Copied > 0)
                report.Add(ValidationSeverity.Info, "HUB_OWN_BUILT",
                    $"Copied {result.Copied} file(s) into your '{profile.OwnHubPrefix}' hub. " +
                    "Check PERMISSIONS.md before sharing — permission is yours to verify.");
        }
        return result;
    }

    /// <summary>
    /// Spells out which mods a downloader will need. Choices like a custom race or a modded
    /// outfit quietly become hard requirements, and that should never be a surprise.
    /// </summary>
    private static void ReportSharingRequirements(
        IReadOnlyList<string> masters, ValidationReport report, CatalogDb? catalog)
    {
        var extra = masters
            .Where(m => !PluginLists.ImplicitBaseMasters.Contains(m, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (extra.Count == 0)
        {
            report.Add(ValidationSeverity.Info, "SHARING_VANILLA_ONLY",
                "She needs nothing but the base game — safe to share with anyone.");
            return;
        }
        foreach (var plugin in extra)
        {
            var mod = catalog?.GetPluginSourceMod(plugin);
            report.Add(ValidationSeverity.Info, "SHARING_REQUIRES",
                $"Anyone who installs her also needs: {plugin}{(mod is null ? "" : $"  (from \"{mod}\")")}");
        }
    }

    /// <summary>Warns when the chosen voice type is not known to carry follower dialogue.</summary>
    private static void ValidateVoiceCapability(FollowerProfile profile, ValidationReport report, CatalogDb? catalog)
    {
        if (catalog is null) return;
        var voice = catalog.GetRecord(profile.VoiceType.FormKey);
        if (voice?.DetailJson is null) return;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(voice.DetailJson);
            if (!doc.RootElement.TryGetProperty("Capability", out var cap)) return;
            var capability = cap.GetString();
            if (capability is "NonFollowerCapable")
                report.Add(ValidationSeverity.Warning, "VOICE_NOT_FOLLOWER",
                    $"Voice '{voice.EditorId}' has no vanilla follower dialogue — recruit/trade/wait will be " +
                    "silent unless you add dialogue (e.g. a follower framework or Mantella).");
            else if (capability is "Unknown")
                report.Add(ValidationSeverity.Warning, "VOICE_UNKNOWN",
                    $"Voice '{voice.EditorId}' follower coverage is unverified; test recruit/trade/wait in-game.");
        }
        catch (System.Text.Json.JsonException) { /* detail malformed — skip */ }
    }

    /// <summary>FaceGen result plus the appearance mods the face requires (from the jslot).</summary>
    private sealed record FaceGenOutcome(FaceGenResult Result, IReadOnlyList<string> RequiredPlugins);

    /// <summary>Resolves and runs the FaceGen dirty-swap when the profile names a CharGen export.</summary>
    private FaceGenOutcome? RunFaceGen(
        FollowerProfile profile, FollowerCompiler.CompileResult compiled, EnvironmentSnapshot env,
        string staging, ValidationReport report, CatalogDb? catalog)
    {
        var app = profile.Appearance;
        var (nifPath, ddsPath, requiredPlugins) = ResolveCharGen(app, env, catalog);
        if (nifPath is null) return null;

        FaceGen.FaceGenSwapper.TextureResolver? resolver = catalog is null
            ? null
            : rel => catalog.AssetExists(rel) ? (true, "load order") : (false, null);

        var req = new FaceGen.FaceGenSwapper.Request(
            SourceNifPath: nifPath,
            SourceDdsPath: ddsPath,
            PluginName: profile.PluginName,
            NpcFormId: compiled.NpcFormKey.ID,
            DataRoot: staging,
            ActorEditorId: compiled.NpcEditorId,
            NpcFormKey: compiled.NpcFormKey.ToString());

        var result = new FaceGen.FaceGenSwapper(log).Swap(req, resolver);
        report.AddRange(result.Findings);
        // A CK handoff is not a hard build failure; the plugin is still valid.
        if (result.NeedsCreationKit)
            report.Add(ValidationSeverity.Warning, "FACEGEN_MANUAL",
                "FaceGen needs a Creation Kit pass — see ck-handoff-report.json");
        return new FaceGenOutcome(result, requiredPlugins);
    }

    /// <summary>Finds the CharGen NIF/DDS (explicit paths win; else by export name) and its jslot plugins.</summary>
    private (string? Nif, string? Dds, IReadOnlyList<string> RequiredPlugins) ResolveCharGen(
        AppearanceSpec app, EnvironmentSnapshot env, CatalogDb? catalog)
    {
        if (app.CharGenNifPath is not null && File.Exists(app.CharGenNifPath))
            return (app.CharGenNifPath, app.CharGenTintPath, ReadJslotPlugins(app.JslotPath));

        if (app.CharGenExportName is null) return (null, null, []);

        var discovery = new CharGenDiscovery(log);
        var export = discovery.Discover(env.GameDataPath)
            .FirstOrDefault(e => string.Equals(e.Name, app.CharGenExportName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(e.Name, app.CharGenExportName + "_head", StringComparison.OrdinalIgnoreCase));
        if (export is null)
        {
            log.Warning("CharGen export '{Name}' not found; building without FaceGen.", app.CharGenExportName);
            return (null, null, []);
        }
        return (export.NifPath, export.TintDdsPath, export.RequiredPlugins);
    }

    private IReadOnlyList<string> ReadJslotPlugins(string? jslotPath) =>
        jslotPath is not null && File.Exists(jslotPath)
            ? new CharGenDiscovery(log).ReadJslotPlugins(jslotPath)
            : [];

    private FollowerManifest BuildManifest(
        FollowerProfile profile, FollowerCompiler.CompileResult c, IReadOnlyList<string> masters,
        FaceGenResult? faceGen) => new()
    {
        Name = profile.Name,
        PluginName = profile.PluginName,
        NpcFormKey = c.NpcFormKey.ToString(),
        NpcEditorId = c.NpcEditorId,
        PlacedFormKey = c.PlacedFormKey.ToString(),
        Strategy = profile.Strategy,
        Masters = masters,
        RaceFormKey = profile.Race.FormKey,
        VoiceFormKey = profile.VoiceType.FormKey,
        CombatStyleFormKey = profile.CombatStyle?.Style.FormKey,
        CombatStyleCloned = profile.CombatStyle?.CloneIntoPlugin ?? false,
        HasFaceGen = faceGen is { Success: true },
        GeneratedAtUtc = DateTimeOffset.FromUnixTimeSeconds(profile.BuildTimestampUnix).UtcDateTime.ToString("O"),
    };

    private static SourceAssetsManifest BuildSourceAssets(FollowerProfile profile, FaceGenOutcome? faceGen,
        HubAssetPackager.Result? hub)
    {
        var assets = new List<SourceAssetEntry>();
        // Pack-local/hub modes copy no shared assets; only the generated FaceGen files are shipped.
        if (faceGen is { Result: { Success: true } r })
        {
            if (r.FaceGeomPath is not null)
                assets.Add(new SourceAssetEntry(r.FaceGeomPath, "generated", null, true, "FaceGen NIF"));
            if (r.FaceTintPath is not null)
                assets.Add(new SourceAssetEntry(r.FaceTintPath, "generated", null, true, "FaceGen tint DDS"));
            foreach (var t in r.Textures.Where(t => !t.Resolved))
                assets.Add(new SourceAssetEntry(t.Path, "unresolved", null, false, "referenced texture (missing)"));
        }

        // Every texture her face uses, where it came from, and whether we repointed it.
        foreach (var a in hub?.Assets ?? [])
        {
            var role = a.RetargetedTo is null
                ? $"{a.Kind} (referenced from your mods)"
                : $"{a.Kind} -> {a.RetargetedTo}";
            assets.Add(new SourceAssetEntry(a.RelPath, a.Container ?? "unresolved", a.SourceMod,
                Copied: a.RetargetedTo is not null && a.RetargetedTo.StartsWith("textures\\", StringComparison.OrdinalIgnoreCase)
                        && profile.Hub == HubMode.OwnHub,
                role));
        }
        return new SourceAssetsManifest(profile.Strategy, profile.RedistributionPermission, assets);
    }

    private static DependencyReport BuildDependencyReport(
        FollowerProfile profile, IReadOnlyList<string> masters, FaceGenOutcome? faceGen, CatalogDb? catalog)
    {
        var entries = new List<DependencyEntry>();
        foreach (var m in masters)
        {
            var reason = m.Equals("Skyrim.esm", StringComparison.OrdinalIgnoreCase)
                ? "Base game (records, factions, placement worldspace)"
                : "Referenced record (race/voice/class/combat style/outfit/item)";
            // Provenance: which Vortex staging mod supplied this master.
            var sourceMod = catalog?.GetPluginSourceMod(m);
            entries.Add(new DependencyEntry(m, reason, sourceMod));
        }
        // Appearance mods the FaceGen head references (hair/eye/skin) are hard requirements to see the face.
        var recommended = faceGen?.RequiredPlugins.ToList() ?? [];

        var warnings = new List<string>();
        if (faceGen is { Result.Textures: { } texs })
            foreach (var missing in texs.Where(t => !t.Resolved))
                warnings.Add($"Face references a texture not in the load order: {missing.Path}");
        if (profile.Strategy == OutputStrategy.PortableStandalone && profile.RedistributionPermission is null)
            warnings.Add("Portable Standalone selected but no redistribution permission declared — asset copying is blocked.");
        return new DependencyReport(entries, recommended, warnings);
    }

    private static string BuildCredits(FollowerProfile profile, IReadOnlyList<string> masters, FaceGenOutcome? faceGen)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Credits — {profile.Name}");
        sb.AppendLine();
        sb.AppendLine($"Generated by Follower Forge on {DateTimeOffset.FromUnixTimeSeconds(profile.BuildTimestampUnix):yyyy-MM-dd}.");
        sb.AppendLine();
        sb.AppendLine("## Plugin masters");
        foreach (var m in masters) sb.AppendLine($"- {m}");
        if (faceGen is { RequiredPlugins.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Appearance requirements (from the RaceMenu preset)");
            sb.AppendLine("These mods supply the hair/eyes/skin the face uses and are REQUIRED to see it correctly:");
            foreach (var p in faceGen.RequiredPlugins) sb.AppendLine($"- {p}");
        }
        sb.AppendLine();
        sb.AppendLine("## Notes");
        sb.AppendLine("- This follower references records/assets from the mods listed above; those mods are required.");
        if (profile.Strategy != OutputStrategy.PortableStandalone)
            sb.AppendLine("- No third-party assets were copied. Appearance/body/voice mods used by the face remain hard requirements.");
        return sb.ToString();
    }

    private static string BuildReportHtml(
        FollowerProfile profile, FollowerManifest manifest, ValidationReport report)
    {
        var rows = string.Join("\n", report.Findings.Select(f =>
            $"<tr class=\"{f.Severity.ToString().ToLowerInvariant()}\"><td>{f.Severity}</td><td>{f.Code}</td><td>{System.Net.WebUtility.HtmlEncode(f.Message)}</td></tr>"));
        var masters = string.Join(", ", manifest.Masters);
        return $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>Follower Forge — {{profile.Name}}</title>
            <style>
              body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#eee;background:#1e1e24}
              h1{color:#c8a24b} table{border-collapse:collapse;width:100%;margin-top:1rem}
              td,th{border:1px solid #444;padding:.4rem .6rem;text-align:left}
              .error{background:#5a1f1f}.warning{background:#5a4a1f}.info{background:#1f3a5a}
              .kv td:first-child{color:#c8a24b;width:16rem}
            </style></head><body>
            <h1>Follower Forge — {{profile.Name}}</h1>
            <table class="kv">
              <tr><td>Plugin</td><td>{{profile.PluginName}}</td></tr>
              <tr><td>NPC FormKey</td><td>{{manifest.NpcFormKey}} ({{manifest.NpcEditorId}})</td></tr>
              <tr><td>Placed ACHR</td><td>{{manifest.PlacedFormKey}}</td></tr>
              <tr><td>Strategy</td><td>{{manifest.Strategy}}</td></tr>
              <tr><td>Masters</td><td>{{masters}}</td></tr>
              <tr><td>FaceGen</td><td>{{manifest.HasFaceGen}}</td></tr>
            </table>
            <h2>Validation</h2>
            <table><tr><th>Severity</th><th>Code</th><th>Message</th></tr>
            {{rows}}
            </table></body></html>
            """;
    }

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    /// <summary>Stable id from the profile so rebuilds reuse the same staging slot.</summary>
    private static string DeterministicBuildId(FollowerProfile profile)
    {
        var basis = $"{profile.PluginName}|{profile.Name}|{profile.Race.FormKey}|{profile.VoiceType.FormKey}";
        var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(basis));
        return Convert.ToHexString(hash)[..12];
    }

    private static string SafeName(string name)
    {
        var cleaned = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '_' or '-' ? ch : '_').ToArray());
        return cleaned.Trim();
    }
}
