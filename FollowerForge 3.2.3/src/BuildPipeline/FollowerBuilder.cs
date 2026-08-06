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

    /// <param name="workspaceRoot">Root of the FollowerForge workspace (must be writable).</param>
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
        var guard = EnvironmentDiscovery.CreateGuard(env);
        guard.EnsureWritable(workspaceRoot);

        var report = new ValidationReport();
        profile = HydrateProfile(profile, env, catalog, report);
        var buildTimeUtc = ResolveBuildTime(profile);
        ValidateProfileReferences(profile, catalog, report);
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

        // The voice type's EditorID names the folder the game reads voice files from, so it has to
        // come from the record itself rather than from the display name shown in the wizard.
        var voiceTypeEditorId = resolver.ResolveVoiceTypeEditorId(profile.VoiceType.FormKey);
        if (VoiceSuitability.Blocker(voiceTypeEditorId) is { } voiceBlocker)
            report.Add(ValidationSeverity.Error, "VOICE_UNSUITABLE",
                $"'{voiceTypeEditorId}' is a {voiceBlocker} and cannot be used for a follower.");
        ReportVoiceCoverage(profile, report);
        ReportCreatureRace(profile, catalog, report);
        ReportMarriage(profile, voiceTypeEditorId, report);
        if (profile.Dialogue.HasContent && voiceTypeEditorId is null)
            report.Add(ValidationSeverity.Error, "DIALOGUE_VOICE_UNRESOLVED",
                $"Custom dialogue needs the EditorID of voice type {profile.VoiceType.FormKey}, " +
                "which could not be read from its plugin. The lines would be silent.");

        var vampireRace = ResolveVampireRace(profile, catalog, report);

        var alternateSpawns = ResolveAlternateSpawns(profile, cellSources, report);

        var compiled = compiler.Compile(profile, location, cstyToClone, cellSource, voiceTypeEditorId,
            vampireRace, alternateSpawns);

        // 2. Write plugin into the package's Data-relative root.
        var pluginRel = profile.PluginName;
        var stagedPlugin = Path.Combine(staging, pluginRel);
        var writer = new PluginWriter(log);
        IReadOnlyList<ModKey>? masterOrder = null;
        string? masterRoot = null;
        if (env.ActiveProfileId is not null && Directory.Exists(env.GameDataPath))
        {
            var (entries, _) = new LoadOrderBuilder(log).BuildEntryList(env);
            masterOrder = PluginLists.ImplicitBaseMasters
                .Concat(entries.Where(e => e.Enabled).Select(e => e.PluginFileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => ModKey.FromFileName(name))
                .ToList();
            masterRoot = env.GameDataPath;
        }
        writer.Write(compiled.Mod, stagedPlugin, masterOrder, masterRoot);

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
        FollowerValidator.ValidateFile(stagedPlugin, profile, report);
        ValidateInstalledMasters(masters, env, catalog, report);
        ValidateMasterClosure(profile, masters, catalog, report, compiled.CombatStyleCloned);
        ValidateEquipmentSlots(profile, catalog, report);
        ValidateVoiceCapability(profile, report, catalog);
        ReportSharingRequirements(masters, report, catalog);

        // 4b. Dialogue audio and the SEQ file. Both need the master list read back from the
        //     written plugin, so this runs after the reopen above.
        BuildDialogueAssets(profile, compiled, staging, masters, voiceTypeEditorId, report);

        // 4c. The evolution script, when the user opted into it.
        WriteEvolutionScript(profile, compiled, staging, report);
        WriteTransformScript(profile, compiled, staging, report);
        WriteRandomSpawnScript(compiled, staging, report);
        WriteSummonScript(profile, compiled, staging, report);

        // 5. FaceGen dirty-swap (optional — only when the profile names a CharGen export).
        var faceGen = RunFaceGen(profile, compiled, env, staging, report, catalog);

        // 5b. Appearance assets: swap in free modder's resources, or copy into the author's own
        //     hub (which requires a written redistribution declaration).
        var hubResult = ApplyHubAssets(profile, faceGen, env, staging, report, catalog);

        // 6. Manifests (using the written master list).
        var manifest = BuildManifest(profile, compiled, masters, faceGen?.Result, buildTimeUtc);
        WriteJson(Path.Combine(staging, "manifest.json"), manifest);
        WriteJson(Path.Combine(staging, "rebuild-profile.json"), profile);
        WriteJson(Path.Combine(staging, "source-assets.json"), BuildSourceAssets(profile, faceGen, hubResult));
        WriteJson(Path.Combine(staging, "dependency-report.json"), BuildDependencyReport(profile, masters, faceGen, catalog));
        File.WriteAllText(Path.Combine(staging, "credits.md"), BuildCredits(profile, masters, faceGen, buildTimeUtc));
        // Said once, only when she actually carries a script.
        if (compiled.Evolution is not null || compiled.Transformation is not null
            || compiled.RandomSpawn is not null || compiled.EnemyToAlly is not null)
            report.Add(ValidationSeverity.Warning, "SCRIPTED_FOLLOWER", FrameworkNote);

        File.WriteAllText(Path.Combine(staging, "build-report.html"), BuildReportHtml(profile, manifest, report));
        TestingNotes.Write(staging, profile, compiled, location);
        SharePackageDocuments.WriteAll(staging, profile, masters, hubResult);
        StampGeneratedTree(staging, buildTimeUtc);

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
                $"Profile asks for location '{id}' but no library exists. Run 'FollowerForge.Cli.exe locations --scan'.");
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
    /// Makes old profiles safe to rebuild and turns a RaceMenu selection into complete,
    /// serialized NPC-record appearance. The original export assets are still resolved later.
    /// </summary>
    private FollowerProfile HydrateProfile(
        FollowerProfile profile,
        EnvironmentSnapshot env,
        CatalogDb? catalog,
        ValidationReport report)
    {
        var equipped = profile.EquippedArmor;
        if (equipped.Count == 0 && catalog is not null)
        {
            equipped = profile.InventoryItems
                .Where(item => catalog.GetRecord(item.FormKey)?.Type == IndexedRecordType.Armor)
                .ToList();
        }

        var inventory = profile.InventoryItems.ToList();
        foreach (var armor in equipped)
            if (!inventory.Any(item => item.FormKey.Equals(
                    armor.FormKey, StringComparison.OrdinalIgnoreCase)))
                inventory.Add(armor);

        var appearance = profile.Appearance;
        AppearanceSpec? parsedAppearance = null;
        if (appearance.JslotPath is { } explicitJslot && File.Exists(explicitJslot))
        {
            parsedAppearance = new CharGenDiscovery(log).ReadJslotAppearance(explicitJslot);
        }
        else if (!string.IsNullOrWhiteSpace(appearance.CharGenExportName))
        {
            var export = FindCharGenExport(appearance.CharGenExportName, env.GameDataPath);
            parsedAppearance = export?.PresetAppearance;
            if (export is null)
            {
                report.Add(ValidationSeverity.Error, "FACE_EXPORT_MISSING",
                    $"RaceMenu face export '{appearance.CharGenExportName}' is no longer present.");
            }
            else if (parsedAppearance is null)
            {
                report.Add(ValidationSeverity.Error, "FACE_PRESET_MISSING",
                    $"'{appearance.CharGenExportName}' has a head export but no readable matching jslot. " +
                    "Export/save the RaceMenu preset with the same name so the NPC record can match the head.");
            }
        }

        if (parsedAppearance is not null)
        {
            appearance = appearance with
            {
                HeadParts = appearance.HeadParts.Count > 0
                    ? appearance.HeadParts
                    : parsedAppearance.HeadParts,
                Weight = parsedAppearance.Weight,
                FaceMorphs = appearance.FaceMorphs.Count > 0
                    ? appearance.FaceMorphs
                    : parsedAppearance.FaceMorphs,
                FacePresets = appearance.FacePresets.Count > 0
                    ? appearance.FacePresets
                    : parsedAppearance.FacePresets,
                TintLayers = appearance.TintLayers.Count > 0
                    ? appearance.TintLayers
                    : parsedAppearance.TintLayers,
                HairColorArgb = appearance.HairColorArgb ?? parsedAppearance.HairColorArgb,
                SkinToneRgba = appearance.SkinToneRgba ?? parsedAppearance.SkinToneRgba,
                HeadTextureSet = appearance.HeadTextureSet ?? parsedAppearance.HeadTextureSet,
                SliderCount = parsedAppearance.SliderCount,
                SculptedVertices = parsedAppearance.SculptedVertices,
            };
            ReportSliderOnlyPreset(appearance, report);
        }

        appearance = appearance with
        {
            HeadTextureSet = ResolveHeadTexture(appearance.HeadTextureSet, catalog, report),
        };

        return profile with
        {
            Appearance = appearance,
            EquippedArmor = equipped,
            InventoryItems = inventory,
        };
    }

    /// <summary>
    /// Warns when a preset's whole shape lives in RaceMenu sliders with nothing sculpted.
    ///
    /// Only two things reach a follower's face: the vanilla 19 morphs, which go on the NPC
    /// record, and geometry, which is baked into the exported head mesh. RaceMenu's extra
    /// sliders (CME_/EFM_/SPG_) are neither — they move the head's NODES on a live actor, which
    /// is why the same preset can look right on your character and flat on a follower. Measured
    /// on this install: no chargen .tri defines a single one of those names as a vertex morph.
    ///
    /// A preset that also sculpts is fine: sculpt is real geometry and bakes.
    /// </summary>
    private static void ReportSliderOnlyPreset(AppearanceSpec appearance, ValidationReport report)
    {
        if (appearance.SliderCount < 10 || appearance.SculptedVertices > 0) return;

        report.Add(ValidationSeverity.Warning, "FACE_SLIDERS_WITHOUT_SCULPT",
            $"This preset shapes the face with {appearance.SliderCount} RaceMenu slider values and "
            + "sculpts nothing. Those sliders move the head's nodes on a live actor — they are not "
            + "geometry, and an NPC has nowhere to store them, so she will come out flatter than "
            + "the preset looks on your own character. To carry the look across, sculpt the face "
            + "in RaceMenu's Sculpt tab (even roughly) before exporting the head, or start from a "
            + "preset that already has sculpt data.");
    }

    /// <summary>
    /// RaceMenu writes the head texture set as a bare "Plugin.esp|001234" string with no hint of
    /// whether the plugin is light, so the local id can be off by the ESL mask. Confirm the id
    /// really names a TXST, try the light form, and otherwise drop it — a complexion is worth a
    /// warning, never a failed build.
    /// </summary>
    private static RecordRef? ResolveHeadTexture(
        RecordRef? candidate, CatalogDb? catalog, ValidationReport report)
    {
        if (candidate is null || catalog is null) return candidate;
        if (catalog.GetRecord(candidate.FormKey)?.Type == IndexedRecordType.TextureSet)
            return candidate;

        var separator = candidate.FormKey.IndexOf(':');
        if (separator > 0
            && uint.TryParse(candidate.FormKey[..separator], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var localId))
        {
            var light = new RecordRef($"{localId & 0xFFF:X6}{candidate.FormKey[separator..]}");
            if (catalog.GetRecord(light.FormKey)?.Type == IndexedRecordType.TextureSet)
                return light;
        }

        report.Add(ValidationSeverity.Warning, "FACE_HEAD_TEXTURE_UNRESOLVED",
            $"The preset's face texture set ({candidate.FormKey}) is not in the active load order. " +
            "She will use her race's default complexion, which can show as a seam at the neck.");
        return null;
    }

    private CharGenExport? FindCharGenExport(string name, string gameDataPath) =>
        new CharGenDiscovery(log).Discover(gameDataPath)
            .FirstOrDefault(export =>
                string.Equals(export.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(export.Name, name + "_head", StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// Copies the compiled evolution script next to the plugin, and says plainly that this is
    /// the experimental path.
    ///
    /// The plugin references the script by name, so a missing .pex means the follower loads and
    /// plays but never evolves — silent, and exactly the kind of thing to fail the build over
    /// rather than ship.
    /// </summary>
    private void WriteEvolutionScript(FollowerProfile profile, FollowerCompiler.CompileResult compiled,
        string staging, ValidationReport report)
    {
        if (compiled.Evolution is null) return;

        if (!CopyBundledScript(EvolutionCompiler.ScriptName, staging, report, "EVOLUTION_SCRIPT_MISSING",
                "The evolution script is not bundled with this build, so she would never evolve."))
            return;

        report.Add(ValidationSeverity.Warning, "EVOLUTION_EXPERIMENTAL",
            $"She carries the EXPERIMENTAL evolution script ({profile.Evolution.Phases} phases, " +
            $"{profile.Evolution.CombatsPerPhase} fights each). This is the only script Follower " +
            "Forge writes, and scripts persist in save files — test her on a save you can throw " +
            "away before committing to a playthrough.");

        log.Information("Wrote Scripts/{Script}.pex", EvolutionCompiler.ScriptName);
    }

    /// <summary>
    /// Copies the transformation script when she has something to turn into.
    ///
    /// A werewolf needs nothing installed beyond Skyrim — the beast race and change effect are
    /// vanilla. A custom transformation points at the user's own records, so the mods those come
    /// from become requirements, which the dependency report already covers.
    /// </summary>
    private void WriteTransformScript(FollowerProfile profile, FollowerCompiler.CompileResult compiled,
        string staging, ValidationReport report)
    {
        if (compiled.Transformation is not { } transformation) return;

        if (!CopyBundledScript(TransformCompiler.ScriptName, staging, report, "TRANSFORM_SCRIPT_MISSING",
                "The transformation script is not bundled with this build, so she would never change form."))
            return;

        var what = transformation.Kind == TransformKind.Werewolf
            ? "a werewolf, using the game's own beast race and change effect"
            : "her chosen form";
        report.Add(ValidationSeverity.Warning, "TRANSFORM_EXPERIMENTAL",
            $"She turns into {what} when a fight starts"
            + (profile.Transformation.RevertOutOfCombat ? " and back afterwards" : " and stays that way")
            + ". This is EXPERIMENTAL and adds a script; scripts persist in save files. Note that "
            + "a beast form cannot wear or wield her equipment — that is Skyrim's behaviour, and "
            + "reverting restores it.");

        log.Information("Wrote Scripts/{Script}.pex", TransformCompiler.ScriptName);
    }

    /// <summary>
    /// Extracts one bundled script (and its source, so the shipped code is auditable) into the
    /// package. Returns false when the script is missing from this build, which is an error
    /// rather than a silent no-op: the plugin references it by name and would do nothing.
    /// </summary>
    private static bool CopyBundledScript(string scriptName, string staging, ValidationReport report,
        string missingCode, string missingMessage)
    {
        var assembly = typeof(FollowerBuilder).Assembly;
        var names = assembly.GetManifestResourceNames();
        var compiled = names.FirstOrDefault(n => n.EndsWith(scriptName + ".pex", StringComparison.OrdinalIgnoreCase));
        if (compiled is null)
        {
            report.Add(ValidationSeverity.Error, missingCode, missingMessage);
            return false;
        }

        var target = Path.Combine(staging, "Scripts", scriptName + ".pex");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var input = assembly.GetManifestResourceStream(compiled)!)
        using (var output = File.Create(target))
            input.CopyTo(output);

        if (names.FirstOrDefault(n => n.EndsWith(scriptName + ".psc", StringComparison.OrdinalIgnoreCase))
            is { } source)
        {
            var sourceTarget = Path.Combine(staging, "Scripts", "Source", scriptName + ".psc");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceTarget)!);
            using var input = assembly.GetManifestResourceStream(source)!;
            using var output = File.Create(sourceTarget);
            input.CopyTo(output);
        }
        return true;
    }

    /// <summary>
    /// Turns the profile's alternate location ids into real spawn points.
    ///
    /// An id that is not in the library is reported rather than skipped: silently dropping one
    /// would give her fewer possible starts than the user asked for, with nothing to show why.
    /// </summary>
    private List<(SpawnLocation Location, ICellGetter? CellSource)> ResolveAlternateSpawns(
        FollowerProfile profile, CellSourceResolver cellSources, ValidationReport report)
    {
        var resolved = new List<(SpawnLocation, ICellGetter?)>();
        // Enemy-to-Ally spots are where her HOSTILE form waits, so they feed the same machinery.
        var ids = profile.EnemyToAlly.IsUsable
            ? profile.EnemyToAlly.LocationIds
            : profile.Placement.AlternateLocationIds;
        if (ids.Count == 0) return resolved;

        var library = LocationLibraryBuilder.Load();
        if (library is null)
        {
            report.Add(ValidationSeverity.Error, "SPAWN_NO_LIBRARY",
                "Alternate spawn points were chosen but no location library exists. "
                + "Run 'FollowerForge.Cli.exe locations --scan'.");
            return resolved;
        }

        foreach (var id in ids.Take(RandomSpawnCompiler.MaxSpots))
        {
            var match = library.Locations.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                report.Add(ValidationSeverity.Error, "SPAWN_UNKNOWN_LOCATION",
                    $"Alternate spawn point '{id}' is not in the location library.");
                continue;
            }
            if (!match.Placeable)
            {
                report.Add(ValidationSeverity.Error, "SPAWN_NOT_PLACEABLE",
                    $"'{match.Display}' cannot hold a marker (exterior grid cell), so it cannot "
                    + "be an alternate spawn point.");
                continue;
            }
            resolved.Add((match, cellSources.Resolve(FormKey.Factory(match.CellFormKey))));
        }

        if (ids.Count > RandomSpawnCompiler.MaxSpots)
            report.Add(ValidationSeverity.Warning, "SPAWN_TOO_MANY",
                $"Only the first {RandomSpawnCompiler.MaxSpots} alternate spawn points are used.");
        return resolved;
    }

    /// <summary>Ships the random-spawn script when she has more than one possible start.</summary>
    private void WriteRandomSpawnScript(FollowerCompiler.CompileResult compiled, string staging,
        ValidationReport report)
    {
        if (compiled.RandomSpawn is not { } spawn) return;

        if (!CopyBundledScript(RandomSpawnCompiler.ScriptName, staging, report, "SPAWN_SCRIPT_MISSING",
                "The random-spawn script is not bundled with this build, so she would always "
                + "start in the same place."))
            return;

        report.Add(ValidationSeverity.Info, "SPAWN_RANDOM",
            $"She starts at one of {spawn.Markers.Count} place(s) chosen at random. This uses our "
            + "own script, so it does not conflict with the Enemy-to-Ally mods, which all ship "
            + "the same shared one.");
        log.Information("Wrote Scripts/{Script}.pex", RandomSpawnCompiler.ScriptName);
    }

    /// <summary>
    /// Ships the summon script and explains the loop the player is signing up for.
    ///
    /// The chain has one failure mode worth stating plainly: if she is set up as an enemy but has
    /// nowhere to be found, she can never be beaten and therefore never recruited. The spec
    /// refuses to emit anything in that case, and this reports it.
    /// </summary>
    private void WriteSummonScript(FollowerProfile profile, FollowerCompiler.CompileResult compiled,
        string staging, ValidationReport report)
    {
        if (profile.EnemyToAlly.Enabled && compiled.EnemyToAlly is null)
        {
            report.Add(ValidationSeverity.Error, "E2A_NO_LOCATIONS",
                "She is set up as an enemy to defeat, but no places were chosen for her hostile "
                + "form to appear. She could never be found, beaten, or recruited.");
            return;
        }
        if (compiled.EnemyToAlly is null) return;

        if (!CopyBundledScript(EnemyToAllyCompiler.ScriptName, staging, report, "E2A_SCRIPT_MISSING",
                "The summon script is not bundled with this build, so she could be beaten but "
                + "never summoned."))
            return;

        report.Add(ValidationSeverity.Info, "E2A_ENABLED",
            "Enemy to ally: her hostile form waits at one of the chosen places. Beat her, take "
            + "the spell tome she is carrying, read it, and the spell summons her to you as a "
            + "follower. She does not exist in the world until then.");
        log.Information("Wrote Scripts/{Script}.pex", EnemyToAllyCompiler.ScriptName);
    }

    /// <summary>
    /// Followers that carry a script can misbehave if a follower framework also manages them.
    /// Worth saying once, at the end, rather than repeating on every script feature.
    /// </summary>
    private const string FrameworkNote =
        "Scripted followers can behave oddly if you also import them into a follower framework "
        + "(NFF, EFF and similar). Ordinary followers are fine; this only applies to the "
        + "experimental options above. If something misbehaves, try not importing her.";

    /// <summary>
    /// Says whether she can actually be married, and what that costs the downloader.
    ///
    /// Vanilla only lets eight voice types marry (the VoicesMarriageNeutral list). Relationship
    /// Dialogue Overhaul ships its own marriage dialogue for roughly 28 more, so on a modded
    /// setup far more followers can marry than vanilla allows — but anyone installing her then
    /// needs RDO too. Reporting from the vanilla list alone would call perfectly good voices
    /// unmarriageable.
    /// </summary>
    private static void ReportMarriage(FollowerProfile profile, string? voiceTypeEditorId,
        ValidationReport report)
    {
        if (!profile.Marriageable) return;

        var library = VoiceCoverageScanner.Load();
        if (library is null)
        {
            report.Add(ValidationSeverity.Warning, "MARRIAGE_UNKNOWN",
                "Cannot tell whether this voice can marry — run 'FollowerForge.Cli.exe voice-coverage --scan' once "
                + "and rebuild. Only eight voice types can marry in vanilla.");
            return;
        }

        var support = library.Marriage.FirstOrDefault(m =>
            m.VoiceFormKey.Equals(profile.VoiceType.FormKey, StringComparison.OrdinalIgnoreCase));
        var name = voiceTypeEditorId ?? profile.VoiceType.FormKey;

        if (support is null)
        {
            report.Add(ValidationSeverity.Warning, "MARRIAGE_VOICE_UNSUPPORTED",
                $"'{name}' has no marriage dialogue in your load order, so the wedding option will "
                + "never appear however high her relationship is. Pick a voice that supports it, "
                + "or leave her unmarried.");
            return;
        }

        if (support.Vanilla)
        {
            report.Add(ValidationSeverity.Info, "MARRIAGE_VANILLA",
                $"'{name}' can marry with no extra mods — it is one of the eight voices in the "
                + "base game's own marriage list.");
            return;
        }

        // Base-game files are not a "requirement" anyone needs telling about.
        var others = support.Plugins
            .Where(p => !PluginLists.ImplicitBaseMasters.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (others.Count == 0) return;

        report.Add(ValidationSeverity.Warning, "MARRIAGE_NEEDS_MOD",
            $"'{name}' can only marry because of {string.Join(", ", others)}. Anyone who installs "
            + "her needs that too, or the wedding option will not appear for them.");
    }

    /// <summary>
    /// Says what a creature race costs her, and refuses the one combination that cannot work.
    ///
    /// A creature race has no head data, so no face can ever be built for it — asking for a
    /// RaceMenu export on a dragon is not a warning, it is impossible. The rest (equipment,
    /// dialogue) degrades rather than fails, so it is reported and left to the user.
    ///
    /// Measured on the reference mods: HSF Baby Dragon's BabyDragonRaceFire has no head data at
    /// all, while Steadfast Machine's Maiden — despite being a dwarven mech — DOES, which is why
    /// she is a normal custom race here and builds a face like anyone else.
    /// </summary>
    private static void ReportCreatureRace(FollowerProfile profile, CatalogDb? catalog,
        ValidationReport report)
    {
        if (catalog is null) return;
        var record = catalog.GetRecord(profile.Race.FormKey);
        if (record is null) return;

        var option = RaceSuitability.Classify(record);
        if (option.Class != RaceClass.Creature) return;

        if (profile.Appearance.CharGenExportName is { Length: > 0 } face)
        {
            report.Add(ValidationSeverity.Error, "CREATURE_RACE_HAS_NO_FACE",
                $"'{option.Name}' is a creature race with no head data, so the face '{face}' "
                + "cannot be built for her. Clear the face, or pick a race that has one.");
            return;
        }

        report.Add(ValidationSeverity.Warning, "CREATURE_RACE",
            $"'{option.Name}' is a creature race. She will use its own model and animations, but "
            + "she has no face to customise, equipment will usually not show, and dialogue "
            + "depends entirely on what that race's mod provides.");
    }

    /// <summary>
    /// Finds the vampire form of her race.
    ///
    /// Vampire races follow an EditorID convention (&lt;race&gt;Vampire) that all eleven vanilla
    /// playable races and the custom-race mods that support vampirism both use. A race with no
    /// vampire form is an ERROR, not a shrug: the alternative is handing someone a follower they
    /// asked to be a vampire who simply is not one, with nothing to explain why.
    /// </summary>
    private static FormKey? ResolveVampireRace(FollowerProfile profile, CatalogDb? catalog,
        ValidationReport report)
    {
        if (!profile.IsVampire) return null;
        if (catalog is null) return null;

        var race = catalog.GetRecord(profile.Race.FormKey);
        if (race?.EditorId is not { Length: > 0 } raceEditorId)
        {
            report.Add(ValidationSeverity.Error, "VAMPIRE_RACE_UNREADABLE",
                $"Could not read race {profile.Race.FormKey}, so her vampire form cannot be found.");
            return null;
        }

        // Already a vampire race: nothing to swap, and the keywords still get added.
        if (VampireRaces.IsVampireRace(raceEditorId)) return FormKey.Factory(race.FormKey);

        var wanted = VampireRaces.VariantEditorId(raceEditorId);
        var match = catalog.SearchRecords(IndexedRecordType.Race, wanted, 200)
            .FirstOrDefault(r => string.Equals(r.EditorId, wanted, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            report.Add(ValidationSeverity.Error, "VAMPIRE_RACE_MISSING",
                $"'{raceEditorId}' has no vampire form ('{wanted}' is not installed), so she "
                + "cannot be made a vampire. Pick a vanilla race, or a custom race whose mod "
                + "ships a vampire variant.");
            return null;
        }
        return FormKey.Factory(match.FormKey);
    }

    /// <summary>
    /// Reports the dialogue this follower inherits for free from her voice type.
    ///
    /// Mods like Relationship Dialogue Overhaul need nothing in the generated plugin — they key
    /// their lines to a voice type, so she is covered the moment she uses one. What is worth
    /// saying is how much she gains and from where, and (when she gains nothing) that a different
    /// voice would give her more.
    /// </summary>
    private static void ReportVoiceCoverage(FollowerProfile profile, ValidationReport report)
    {
        var library = VoiceCoverageScanner.Load();
        if (library is null) return;   // never scanned; silence beats a misleading "0 lines"

        var match = library.Voices.FirstOrDefault(v =>
            v.VoiceFormKey.Equals(profile.VoiceType.FormKey, StringComparison.OrdinalIgnoreCase));
        if (match is null || match.TotalLines == 0)
        {
            report.Add(ValidationSeverity.Warning, "VOICE_NO_EXTRA_DIALOGUE",
                "No installed mod adds dialogue for this voice type, so she only says what the " +
                "base game gives her. A vanilla follower voice would inherit far more.");
            return;
        }

        var top = match.Contributions.Take(4).Select(c => $"{c.Plugin} (+{c.Lines})");
        report.Add(ValidationSeverity.Info, "VOICE_EXTRA_DIALOGUE",
            $"She inherits ~{match.TotalLines:N0} extra line(s) from {match.Contributions.Count} " +
            $"installed mod(s) via her voice type: {string.Join(", ", top)}" +
            (match.Contributions.Count > 4 ? ", …" : "") +
            ". Anyone who installs her needs those mods to hear them.");
    }

    /// <summary>
    /// Emits everything the custom dialogue needs beyond the records themselves: the SEQ file
    /// that starts the quest in an existing save, and one .fuz of lipsynced audio per line.
    ///
    /// Failures here are errors rather than warnings on purpose. A follower whose dialogue quietly
    /// does not start, or whose lines are silent, looks identical to a working one until the user
    /// is standing in front of her in-game.
    /// </summary>
    private void BuildDialogueAssets(FollowerProfile profile, FollowerCompiler.CompileResult compiled,
        string staging, IReadOnlyList<string> masters, string? voiceTypeEditorId, ValidationReport report)
    {
        if (compiled.Dialogue is not { } dialogue) return;
        // Synthesis is the slow part of a build; there is no point spending it on a package that
        // has already failed validation and will not be published.
        if (report.HasErrors)
        {
            log.Warning("Skipping dialogue audio — the build has already failed validation");
            return;
        }

        // The plugin's own records live at the index just past its masters; SeqWriter needs that
        // to write full FormIDs. Anything else and the quest silently never starts.
        var seqRel = SeqWriter.Write(staging, profile.PluginName, [dialogue.Quest], (byte)masters.Count);
        log.Information("Wrote {Seq} for dialogue quest {Quest}", seqRel, dialogue.QuestEditorId);

        if (!profile.Dialogue.Synthesize)
        {
            report.Add(ValidationSeverity.Warning, "DIALOGUE_SILENT",
                $"{dialogue.Lines.Count} custom line(s) will show as subtitles only — voice " +
                "synthesis is turned off, so she will mouth them silently.");
            return;
        }

        var catalog = new VoiceModelCatalog();
        using var synth = new VoiceSynthesizer(catalog, log);
        if (!synth.Available)
        {
            report.Add(ValidationSeverity.Error, "DIALOGUE_NO_XVASYNTH",
                "Voiced dialogue was requested but xVASynth (with its lip_fuz plugin) was not " +
                $"found at {catalog.Root}. Install it, or turn voice synthesis off to ship the " +
                "lines as silent subtitles deliberately.");
            return;
        }

        var spoken = 0;
        foreach (var line in dialogue.Lines)
        {
            var target = Path.Combine(staging, line.VoiceRelativePath);
            var result = synth.SpeakAsync(voiceTypeEditorId ?? string.Empty, line.Text, target)
                .GetAwaiter().GetResult();
            if (result.Success) { spoken++; continue; }

            report.Add(ValidationSeverity.Error, "DIALOGUE_SYNTH_FAILED",
                $"Could not voice \"{Shorten(line.Text)}\": {result.Error}");
        }

        if (spoken > 0)
            log.Information("Voiced {Count}/{Total} lines", spoken, dialogue.Lines.Count);
    }

    private static string Shorten(string text) =>
        text.Length <= 48 ? text : text[..45] + "…";

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

        FaceGen.FaceGenSwapper.TextureResolver resolver =
            rel => ResolveFaceTexture(rel, env.GameDataPath, catalog);

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

    /// <summary>
    /// Resolves a NIF texture against the catalogue first, then the actual deployed Data tree.
    /// The fallback matters for valid Vortex hardlinks absent from a stale/incomplete manifest.
    /// It is read-only and rejects paths that escape Data.
    /// </summary>
    public static (bool Resolved, string? Container) ResolveFaceTexture(
        string relPath, string gameDataPath, CatalogDb? catalog)
    {
        if (catalog?.AssetExists(relPath) == true)
            return (true, "load order catalogue");

        var normalized = relPath.Replace('/', '\\').TrimStart('\\');
        if (normalized.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[5..];

        var dataRoot = Path.GetFullPath(gameDataPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(dataRoot, normalized));
        if (!candidate.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
            return (false, null);
        return File.Exists(candidate)
            ? (true, "deployed Data file")
            : (false, null);
    }

    /// <summary>Finds the CharGen NIF/DDS (explicit paths win; else by export name) and its jslot plugins.</summary>
    private (string? Nif, string? Dds, IReadOnlyList<string> RequiredPlugins) ResolveCharGen(
        AppearanceSpec app, EnvironmentSnapshot env, CatalogDb? catalog)
    {
        if (app.CharGenNifPath is not null && File.Exists(app.CharGenNifPath))
            return (app.CharGenNifPath, app.CharGenTintPath, ReadJslotPlugins(app.JslotPath));

        if (app.CharGenExportName is null) return (null, null, []);

        var discovery = new CharGenDiscovery(log);
        var export = FindCharGenExport(app.CharGenExportName, env.GameDataPath);
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
        FaceGenResult? faceGen, DateTimeOffset buildTimeUtc) => new()
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
        GeneratedAtUtc = buildTimeUtc.UtcDateTime.ToString("O"),
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

    private static string BuildCredits(
        FollowerProfile profile, IReadOnlyList<string> masters, FaceGenOutcome? faceGen,
        DateTimeOffset buildTimeUtc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Credits — {profile.Name}");
        sb.AppendLine();
        sb.AppendLine($"Generated by FollowerForge on {buildTimeUtc:yyyy-MM-dd}.");
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
            <!doctype html><html><head><meta charset="utf-8"><title>FollowerForge — {{profile.Name}}</title>
            <style>
              body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#eee;background:#1e1e24}
              h1{color:#c8a24b} table{border-collapse:collapse;width:100%;margin-top:1rem}
              td,th{border:1px solid #444;padding:.4rem .6rem;text-align:left}
              .error{background:#5a1f1f}.warning{background:#5a4a1f}.info{background:#1f3a5a}
              .kv td:first-child{color:#c8a24b;width:16rem}
            </style></head><body>
            <h1>FollowerForge — {{profile.Name}}</h1>
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

    private static DateTimeOffset ResolveBuildTime(FollowerProfile profile) =>
        profile.BuildTimestampUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(profile.BuildTimestampUnix)
            : DateTimeOffset.UtcNow;

    /// <summary>
    /// File.Copy can preserve a 2019-era source FaceGen timestamp. Normalize every generated
    /// file and directory to this build's real (or explicitly pinned) time so Explorer and
    /// Vortex show the follower as newly built.
    /// </summary>
    private static void StampGeneratedTree(string root, DateTimeOffset buildTimeUtc)
    {
        var utc = buildTimeUtc.UtcDateTime;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, utc);
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(p => p.Length))
            Directory.SetLastWriteTimeUtc(dir, utc);
        Directory.SetLastWriteTimeUtc(root, utc);
    }

    /// <summary>Record types the engine accepts inside an NPC's container item list.</summary>
    private static readonly IndexedRecordType[] CarryableTypes =
    [
        IndexedRecordType.Armor, IndexedRecordType.Weapon, IndexedRecordType.Book,
        IndexedRecordType.MiscItem, IndexedRecordType.Ingestible, IndexedRecordType.Ingredient,
    ];

    internal static void ValidateProfileReferences(
        FollowerProfile profile, CatalogDb? catalog, ValidationReport report)
    {
        if (catalog is null) return;

        Check(profile.Race, "race", [IndexedRecordType.Race]);
        Check(profile.VoiceType, "voice", [IndexedRecordType.VoiceType]);
        Check(profile.Class, "class", [IndexedRecordType.Class]);
        if (profile.CombatStyle is { } combat)
            Check(combat.Style, "combat style", [IndexedRecordType.CombatStyle]);
        if (profile.Outfit is { } outfit)
            Check(outfit, "legacy outfit", [IndexedRecordType.Outfit]);
        if (profile.SkinArmor is { } skin)
            Check(skin, "body/skin armor", [IndexedRecordType.Armor]);
        foreach (var armor in profile.EquippedArmor)
            Check(armor, "initially equipped armor/accessory", [IndexedRecordType.Armor]);
        // InventoryItems is everything she carries, not just what she fights in: the lore step
        // adds books, keepsakes, food and ingredients here too. Gating this on Armor/Weapon
        // failed every build that used the lore step.
        foreach (var item in profile.InventoryItems)
            Check(item, "equipment/inventory item", CarryableTypes);
        foreach (var spell in profile.Spells)
            Check(spell, "spell", [IndexedRecordType.Spell]);
        foreach (var perk in profile.Perks)
            Check(perk, "perk", [IndexedRecordType.Perk]);

        var duplicates = profile.InventoryItems
            .GroupBy(i => i.FormKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            report.Add(ValidationSeverity.Error, "INVENTORY_DUPLICATE",
                $"Duplicate equipment/inventory FormKeys: {string.Join(", ", duplicates)}");

        void Check(RecordRef reference, string label, IndexedRecordType[] allowed)
        {
            var record = catalog.GetRecord(reference.FormKey);
            if (record is null)
            {
                report.Add(ValidationSeverity.Error, "REFERENCE_MISSING",
                    $"Selected {label} does not exist in the active Vortex catalogue: {reference.FormKey}");
                return;
            }
            if (!allowed.Contains(record.Type))
                report.Add(ValidationSeverity.Error, "REFERENCE_WRONG_TYPE",
                    $"Selected {label} {reference.FormKey} is {record.Type}, expected {string.Join("/", allowed)}.");
        }
    }

    private static void ValidateInstalledMasters(
        IReadOnlyList<string> masters, EnvironmentSnapshot env, CatalogDb? catalog,
        ValidationReport report)
    {
        if (catalog is null) return;
        foreach (var master in masters)
            if (!File.Exists(Path.Combine(env.GameDataPath, master)))
                report.Add(ValidationSeverity.Error, "MASTER_FILE_MISSING",
                    $"Generated plugin requires {master}, but it is not deployed in Skyrim Data.");
    }

    private static void ValidateMasterClosure(
        FollowerProfile profile, IReadOnlyList<string> writtenMasters, CatalogDb? catalog,
        ValidationReport report, bool combatStyleCloned = false)
    {
        if (catalog is null) return;

        var selected = new List<RecordRef>
        {
            profile.Race, profile.VoiceType, profile.Class,
        };
        // A cloned combat style is a COPY living in this plugin, so its origin is not a master
        // any more — that is the whole point of copying it. Requiring it here failed builds for
        // styles from mods like Nether's Follower Framework even though the written plugin was
        // correct. Only a style we merely reference keeps its source as a dependency.
        if (profile.CombatStyle is { } combat && !combatStyleCloned) selected.Add(combat.Style);
        if (profile.Outfit is { } outfit) selected.Add(outfit);
        if (profile.SkinArmor is { } skin) selected.Add(skin);
        selected.AddRange(profile.EquippedArmor);
        selected.AddRange(profile.InventoryItems);
        selected.AddRange(profile.Spells);
        selected.AddRange(profile.Perks);

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in selected)
        {
            var record = catalog.GetRecord(reference.FormKey);
            if (record is null) continue;
            required.Add(record.SourcePlugin);
            // RequiredMasters describes the winning plugin. It is the authoritative origin
            // chain only when that winner is also the plugin that owns the FormKey.
            if (record.WinningPlugin.Equals(record.SourcePlugin, StringComparison.OrdinalIgnoreCase))
                required.UnionWith(record.RequiredMasters);
        }
        required.Remove(profile.PluginName);

        var missing = required
            .Where(m => !writtenMasters.Contains(m, StringComparer.OrdinalIgnoreCase))
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count > 0)
            report.Add(ValidationSeverity.Error, "MASTER_CHAIN_INCOMPLETE",
                $"Generated plugin omitted required origin/master-chain entries: {string.Join(", ", missing)}");
    }

    private static void ValidateEquipmentSlots(
        FollowerProfile profile, CatalogDb? catalog, ValidationReport report)
    {
        if (catalog is null) return;

        var selectedArmor = profile.EquippedArmor
            .Select(item => catalog.GetRecord(item.FormKey))
            .Where(record => record?.Type == IndexedRecordType.Armor)
            .ToList();
        if (selectedArmor.Count == 0 && profile.Outfit is null)
            report.Add(ValidationSeverity.Warning, "NO_STARTING_ARMOR",
                "No actual armor/accessory records or legacy outfit were selected; the follower may spawn without clothing.");
        if (selectedArmor.Count > 0 && profile.Outfit is not null)
            report.Add(ValidationSeverity.Warning, "LEGACY_OUTFIT_WITH_EQUIPMENT",
                "A legacy OTFT and actual armor were both selected. The engine may choose the OTFT pieces over inventory gear.");

        var bySlot = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in profile.EquippedArmor)
        {
            var record = catalog.GetRecord(item.FormKey);
            if (record?.Type != IndexedRecordType.Armor) continue;
            if (record.DetailJson is null)
            {
                report.Add(ValidationSeverity.Warning, "ARMOR_SLOTS_UNKNOWN",
                    $"Could not read biped slots for {record.DisplayName ?? record.EditorId ?? item.FormKey}; verify it equips in game.");
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(record.DetailJson);
                if (!doc.RootElement.TryGetProperty("BipedSlots", out var slots)
                    || slots.GetArrayLength() == 0)
                {
                    report.Add(ValidationSeverity.Warning, "ARMOR_SLOTS_EMPTY",
                        $"{record.DisplayName ?? record.EditorId ?? item.FormKey} occupies no reported biped slot.");
                    continue;
                }
                foreach (var slot in slots.EnumerateArray()
                             .Select(s => s.GetString())
                             .Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    if (!bySlot.TryGetValue(slot!, out var occupants))
                        bySlot[slot!] = occupants = [];
                    occupants.Add(record.DisplayName ?? record.EditorId ?? item.FormKey);
                }
            }
            catch (JsonException)
            {
                report.Add(ValidationSeverity.Warning, "ARMOR_SLOTS_INVALID",
                    $"Stored armor-slot data is invalid for {record.DisplayName ?? record.EditorId ?? item.FormKey}.");
            }
        }

        var conflicts = bySlot
            .Where(pair => pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(pair => $"{pair.Key}: {string.Join(" + ", pair.Value.Distinct(StringComparer.OrdinalIgnoreCase))}")
            .ToList();
        if (conflicts.Count > 0)
            report.Add(ValidationSeverity.Warning, "EQUIPMENT_SLOT_CONFLICT",
                "Some selected armor pieces cannot be worn together: " + string.Join("; ", conflicts));
    }

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
