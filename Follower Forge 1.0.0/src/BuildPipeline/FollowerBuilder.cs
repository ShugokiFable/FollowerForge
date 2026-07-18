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
    /// <param name="placementWorldspace">WhiterunWorld getter for exterior placement (null = stub).</param>
    public BuildResult Build(
        FollowerProfile profile,
        EnvironmentSnapshot env,
        string workspaceRoot,
        IWorldspaceGetter? placementWorldspace,
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
        var compiled = compiler.Compile(profile, placementWorldspace, cstyToClone);

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

        // 5. FaceGen dirty-swap (optional — only when the profile names a CharGen export).
        var faceGen = RunFaceGen(profile, compiled, env, staging, report, catalog);

        // 5b. Portable standalone: copy referenced assets — but ONLY with explicit permission.
        if (profile.Strategy == OutputStrategy.PortableStandalone)
            CopyStandaloneAssets(profile, faceGen, env, staging, report, catalog);

        // 6. Manifests (using the written master list).
        var manifest = BuildManifest(profile, compiled, masters, faceGen?.Result);
        WriteJson(Path.Combine(staging, "manifest.json"), manifest);
        WriteJson(Path.Combine(staging, "rebuild-profile.json"), profile);
        WriteJson(Path.Combine(staging, "source-assets.json"), BuildSourceAssets(profile, faceGen));
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

    /// <summary>
    /// Portable Standalone: copy the face-referenced textures into the package so it works without
    /// the source appearance mods. Refuses unless the profile carries an explicit permission
    /// declaration — redistribution rights are never inferred. Loose files are copied directly;
    /// BSA-packed textures are reported for manual extraction (their license usually forbids repack).
    /// </summary>
    private void CopyStandaloneAssets(
        FollowerProfile profile, FaceGenOutcome? faceGen, EnvironmentSnapshot env,
        string staging, ValidationReport report, CatalogDb? catalog)
    {
        if (string.IsNullOrWhiteSpace(profile.RedistributionPermission))
        {
            report.Add(ValidationSeverity.Error, "STANDALONE_NO_PERMISSION",
                "Portable Standalone requires an explicit RedistributionPermission declaration; " +
                "no assets were copied. Set it in the profile only if you hold redistribution rights.");
            return;
        }
        if (faceGen?.Result.Textures is not { } textures || catalog is null)
        {
            report.Add(ValidationSeverity.Warning, "STANDALONE_NO_ASSETS",
                "No face textures to copy (build without FaceGen or catalogue).");
            return;
        }

        var guard = VortexDiscovery.CreateGuard(env);
        int copied = 0, fromArchive = 0;
        foreach (var tex in textures.Where(t => t.Resolved))
        {
            var asset = catalog.GetAsset(tex.Path);
            if (asset is null) continue;
            var destAbs = Path.Combine(staging, tex.Path);
            guard.EnsureWritable(destAbs);
            if (asset.Container == AssetContainerKind.Loose)
            {
                var srcAbs = Path.Combine(env.GameDataPath, tex.Path);
                if (File.Exists(srcAbs))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destAbs)!);
                    File.Copy(srcAbs, destAbs, overwrite: true);
                    copied++;
                }
            }
            else
            {
                fromArchive++;
                report.Add(ValidationSeverity.Warning, "STANDALONE_BSA_TEXTURE",
                    $"Texture is inside archive {asset.ContainerName}; extract it manually if its license permits: {tex.Path}",
                    tex.Path);
            }
        }
        log.Information("Standalone: copied {Copied} loose textures ({Archive} left in BSAs)", copied, fromArchive);
        report.Add(ValidationSeverity.Info, "STANDALONE_COPIED",
            $"Copied {copied} loose textures under declared permission; {fromArchive} remain in archives.");
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

    private static SourceAssetsManifest BuildSourceAssets(FollowerProfile profile, FaceGenOutcome? faceGen)
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
