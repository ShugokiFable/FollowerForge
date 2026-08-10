# FollowerForge 3.2.7 MO2 Instance and Profile Override Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MO2 indexing deterministic for portable, global, and custom-path installations by adding a validated GUI instance/profile override while preserving automatic discovery, CLI overrides, and Vortex behavior.

**Architecture:** A read-only `Mo2InstanceInspector` owns INI parsing and canonical path resolution. `Mo2Discovery` consumes inspection results with strict explicit-profile semantics, while `Mo2UserSettings` persists only FollowerForge-owned GUI choices. A focused Avalonia dialog returns a validated selection to `WizardWindow`, which retains ownership of cancellation and serialized re-indexing.

**Tech Stack:** .NET 10, C# 14, Avalonia 12.1, xUnit 2.5, Serilog, PowerShell release scripts.

## Global Constraints

- Release version is `3.2.7`; parent snapshot `FollowerForge 3.2.6` remains immutable.
- Never write to MO2, Vortex, Skyrim, staging/mods, profiles, overwrite, Data, or saves.
- Persist GUI settings only at `%LOCALAPPDATA%\FollowerForge\mo2-settings.json` using schema version `1` and an atomic same-directory replacement.
- Precedence is explicit CLI arguments, FollowerForge environment overrides, saved GUI override, then automatic discovery and the INI-selected profile.
- Explicit/manual profiles never fall back to a different profile or mod manager.
- Automatic profile selection may fall back to the first profile but must retain an actionable warning.
- Vortex discovery and indexing behavior must remain unchanged.
- Use tests before production changes and explicitly stage only intended source, documentation, release, and checksum files.

---

### Task 1: MO2 INI inspection and path normalization

**Files:**
- Create: `src/ModManagers/Mo2InstanceInspector.cs`
- Create: `src/Tests/Mo2InstanceInspectorTests.cs`
- Modify: `src/ModManagers/Mo2Discovery.cs`

**Interfaces:**
- Produces: `Mo2Inspection Mo2InstanceInspector.Inspect(string iniPath, string? gameRootOverride = null)`.
- `Mo2Inspection` exposes `IniPath`, `InstanceRoot`, `BaseDirectory`, `GameRoot`, `ModsPath`, `ProfilesPath`, `OverwritePath`, `SelectedProfile`, `Profiles`, `Errors`, `Warnings`, and `IsValid`.
- `Mo2Discovery` consumes the inspection instead of duplicating INI and path parsing.

- [ ] **Step 1: Write failing inspector tests**

Create fixture-driven xUnit tests proving case-insensitive `%BASE_DIR%` expansion, environment-variable expansion, relative paths anchored to the resolved base directory, a relative `base_directory` anchored to the instance root, profile enumeration, and exact error messages for missing INI/game Data/mods/profiles.

- [ ] **Step 2: Run the focused tests and confirm RED**

Run: `dotnet test src/FollowerForge.slnx -c Release --filter FullyQualifiedName~Mo2InstanceInspectorTests`

Expected: compilation fails because `Mo2InstanceInspector` and `Mo2Inspection` do not exist.

- [ ] **Step 3: Implement the typed inspector**

Implement these public shapes in `FollowerForge.ModManagers`:

```csharp
public sealed record Mo2Inspection(
    string IniPath,
    string InstanceRoot,
    string BaseDirectory,
    string? GameRoot,
    string ModsPath,
    string ProfilesPath,
    string OverwritePath,
    string? SelectedProfile,
    IReadOnlyList<string> Profiles,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class Mo2InstanceInspector(ILogger log)
{
    public Mo2Inspection Inspect(string iniPath, string? gameRootOverride = null);
}
```

The implementation reads `Settings/base_directory`, resolves it against the instance root, replaces `%BASE_DIR%` case-insensitively in all configurable path fields, expands environment variables, anchors remaining relative paths to the canonical base directory, canonicalizes paths, and performs read-only validation.

- [ ] **Step 4: Run the focused tests and confirm GREEN**

Run the Task 1 test command. Expected: all `Mo2InstanceInspectorTests` pass.

- [ ] **Step 5: Commit the inspector slice**

Stage only the three Task 1 files and commit `Implement MO2 instance path inspection`.

---

### Task 2: Strict profile selection and precedence

**Files:**
- Modify: `src/ModManagers/Mo2Discovery.cs`
- Modify: `src/ModManagers/EnvironmentDiscovery.cs`
- Modify: `src/Cli/Program.cs`
- Create: `src/Tests/Mo2DiscoveryOverrideTests.cs`
- Modify: `src/Tests/DiscoveryPriorityTests.cs`

**Interfaces:**
- Produces: `EnvironmentSnapshot? Mo2Discovery.TryDiscover(string? instanceOverride = null, string? gameRootOverride = null, string? profileOverride = null, bool strictOverride = false)`.
- Produces: `EnvironmentSnapshot EnvironmentDiscovery.Discover(string? gameRootOverride = null, string? mo2InstanceOverride = null, bool? preferMo2 = null, string? mo2ProfileOverride = null, bool strictMo2Override = false)`.
- CLI accepts `--mo2-profile NAME` anywhere `--mo2-instance DIR` is accepted.

- [ ] **Step 1: Write failing discovery tests**

Cover an explicit profile overriding INI `selected_profile`, an explicit missing profile throwing an actionable `DirectoryNotFoundException`, automatic mode warning and first-profile fallback, explicit CLI instance/profile beating environment values, and strict MO2 failure not falling back to Vortex.

- [ ] **Step 2: Run the focused tests and confirm RED**

Run: `dotnet test src/FollowerForge.slnx -c Release --filter "FullyQualifiedName~Mo2DiscoveryOverrideTests|FullyQualifiedName~DiscoveryPriorityTests"`

Expected: new override and strict-mode assertions fail against the old discovery signatures.

- [ ] **Step 3: Implement strict selection and pass CLI profile values**

Refactor `Mo2Discovery` to consume `Mo2InstanceInspector`, select `profileOverride` before INI state, validate `modlist.txt` plus either `plugins.txt` or `loadorder.txt` in strict mode, and preserve warning/fallback only in automatic mode. Update every CLI discovery call and help line to pass `opts.GetValueOrDefault("mo2-profile")` without altering unrelated commands.

- [ ] **Step 4: Run focused and full ModManagers tests**

Run the Task 2 command, then `dotnet test src/FollowerForge.slnx -c Release --filter FullyQualifiedName~FollowerForge.Tests`.

Expected: focused tests and existing discovery tests pass.

- [ ] **Step 5: Commit the discovery slice**

Commit `Add deterministic MO2 profile overrides` with only the Task 2 files.

---

### Task 3: Persisted FollowerForge-owned MO2 settings

**Files:**
- Create: `src/ModManagers/Mo2UserSettings.cs`
- Create: `src/Tests/Mo2UserSettingsTests.cs`

**Interfaces:**
- Produces: `Mo2UserSelection(string InstanceRoot, string ProfileName)`.
- Produces: `Mo2UserSettings.Load(string? settingsDirectory = null, Action<string>? warning = null)`, `Save(Mo2UserSelection selection, string? settingsDirectory = null)`, and `Clear(string? settingsDirectory = null)`.

- [ ] **Step 1: Write failing settings tests**

Use a test-owned temporary directory to cover JSON round-trip, schema `1`, corrupt JSON returning `null` with a warning, unknown schema returning `null` with a warning, replacement of an existing file, and `Clear` removing only `mo2-settings.json`.

- [ ] **Step 2: Run the focused tests and confirm RED**

Run: `dotnet test src/FollowerForge.slnx -c Release --filter FullyQualifiedName~Mo2UserSettingsTests`

Expected: compilation fails because the settings types do not exist.

- [ ] **Step 3: Implement atomic settings storage**

Serialize camel-case JSON to a uniquely named temporary file beside the target, flush and close it, then atomically replace/move it into place. Clean up only the temporary file on failure. Default the directory to `ManagerPreference.SettingsDirectory`; never accept or derive an MO2-owned output path.

- [ ] **Step 4: Run focused tests and confirm GREEN**

Run the Task 3 test command. Expected: all settings tests pass.

- [ ] **Step 5: Commit the settings slice**

Commit `Persist MO2 setup selection safely` with the two Task 3 files.

---

### Task 4: Avalonia MO2 setup dialog and reload orchestration

**Files:**
- Create: `src/Ui/Mo2SetupWindow.axaml`
- Create: `src/Ui/Mo2SetupWindow.axaml.cs`
- Create: `src/Ui/Mo2SetupController.cs`
- Modify: `src/Ui/WizardWindow.axaml`
- Modify: `src/Ui/WizardWindow.axaml.cs`
- Create: `src/Tests/Mo2SetupControllerTests.cs`
- Modify: `src/Tests/SkillEditorLayoutTests.cs`

**Interfaces:**
- Produces: `Mo2SetupResult(Mo2UserSelection? Selection, bool ReturnToAutomatic)`.
- `Mo2SetupController.Inspect(string iniPath)` populates profiles/status without indexing.
- `WizardWindow` loads saved settings, passes strict saved overrides to discovery, and owns `CancelLoadingAndReloadAsync()`.

- [ ] **Step 1: Write failing controller and layout tests**

Test browse-path inspection, profile preflight requiring `modlist.txt` and one load-order file, missing overwrite as warning only, invalid manual selection remaining rejected, and XAML containing `Mo2SetupButton` beneath `ManagerSwitchButton`.

- [ ] **Step 2: Run focused tests and confirm RED**

Run: `dotnet test src/FollowerForge.slnx -c Release --filter "FullyQualifiedName~Mo2SetupControllerTests|FullyQualifiedName~SkillEditorLayoutTests"`

Expected: controller types and XAML setup button are absent.

- [ ] **Step 3: Implement the dialog**

Build a modal Avalonia window with a `ModOrganizer.ini` textbox, filtered file picker, resolved-path summary, profile `ComboBox`, exact validation/status text, and the three designed actions. The dialog validates through `Mo2SetupController` and returns a result; it never builds a catalogue or writes outside FollowerForge settings.

- [ ] **Step 4: Wire settings and serialized re-indexing into the wizard**

Add the sidebar button and click handler. On save, call `Mo2UserSettings.Save`, set MO2 preferred, cancel the old generation, clear manager-dependent UI state, and await one new `LoadEverythingAsync`. On automatic reset, clear the settings and rerun automatic MO2 discovery. If preferred MO2 discovery fails, show the setup dialog rather than only recommending an environment variable.

- [ ] **Step 5: Run focused tests and confirm GREEN**

Run the Task 4 test command. Expected: controller and XAML assertions pass.

- [ ] **Step 6: Commit the UI slice**

Commit `Add MO2 setup dialog and safe re-indexing` with only Task 4 files.

---

### Task 5: Version, documentation, and release evidence

**Files:**
- Modify: `src/Ui/FollowerForge.Ui.csproj`
- Modify: `README.md`
- Modify: `CHANGELOG.txt`
- Modify: `VERSION.md`
- Modify: `docs/MO2.md`
- Modify: `STATE.md`
- Modify: repository-root `CURRENT.txt`, `CHANGELOG.txt`, `PLAN.md`, `STATE.md`, and `WORKSPACE_OWNERSHIP.md` as needed for final 3.2.7 status
- Create: `NEXUS-CHANGELOG-3.2.7.txt`

**Interfaces:**
- Published executable metadata reports `3.2.7`.
- User documentation names the exact GUI recovery path and rollback file `%LOCALAPPDATA%\FollowerForge\mo2-settings.json`.

- [ ] **Step 1: Update version metadata and user documentation**

Set `Version`, `FileVersion`, and `InformationalVersion` to 3.2.7 values. Document automatic detection, `MO2 setup...`, profile selection, `--mo2-profile`, settings reset, no-write boundary, and rollback to 3.2.6.

- [ ] **Step 2: Write the short Nexus changelog**

Keep it user-facing: MO2 instance/profile picker, custom/base path correctness, no silent profile substitution, saved selection, re-index behavior, unchanged Vortex workflow, and instruction to use `MO2 setup...` once after updating if automatic detection fails.

- [ ] **Step 3: Run documentation/version scans**

Run `rg -n "3\.2\.[0-6]|PLACEHOLDER|FIXME"` across current-version release files and fix stale release claims or placeholders that would ship.

- [ ] **Step 4: Commit the release documentation slice**

Commit `Document FollowerForge 3.2.7` with only intended documentation and metadata.

---

### Task 6: Full verification, Nexus package, and scoped GitHub publication

**Files:**
- Modify only existing release scripts if a verified packaging defect requires it.
- Create final release ZIP and SHA-256 file in the repository's established distribution directory.

**Interfaces:**
- Produces a self-contained Windows x64 Nexus archive containing the published app and user documentation without source, test fixtures, caches, or temporary files.
- Publishes the exact 3.2.7 commits to `ShugokiFable/FollowerForge`; no second repository.

- [ ] **Step 1: Run clean verification**

Run the complete xUnit suite in Release, a clean Release build, the established self-contained win-x64 publish script, CLI `--help`/environment boot checks, and a bounded GUI process boot check. Record exact pass counts and commands in `STATE.md`.

- [ ] **Step 2: Run the MO2 fixture index**

Create a test-owned fixture outside live MO2/Vortex/Skyrim paths, run discovery/indexing against a nonstandard instance with `%BASE_DIR%` paths and an explicit profile, and verify the resulting environment identifies that exact instance/profile.

- [ ] **Step 3: Build and inspect the Nexus ZIP**

Use the established release script. Expand the ZIP to a test-owned directory, inventory it, confirm executable file metadata is 3.2.7, run archive CRC/extraction validation, boot the extracted CLI/app, scan for secrets/temp/source artifacts, and compute SHA-256.

- [ ] **Step 4: Review the complete diff and repository hygiene**

Run `git status --short`, `git diff --check`, inspect every changed path, verify no live/local settings or test fixtures are tracked, and confirm only `FollowerForge 3.2.7`, canonical root pointers/docs, and intended release artifacts differ from 3.2.6.

- [ ] **Step 5: Commit final evidence and package metadata**

Explicitly stage the final intended paths and commit `Release FollowerForge 3.2.7`.

- [ ] **Step 6: Push and open the scoped pull request**

Push `agent/mo2-manual-setup` to the existing `https://github.com/ShugokiFable/FollowerForge` remote, open a pull request to `main`, verify remote branch/PR contents and checks, and merge only if repository policy and checks permit. Do not create another repository or upload temporary test files.

- [ ] **Step 7: Verify publication artifacts**

Re-fetch the remote commit/PR metadata, compare the remote head SHA with the local release commit, and report the local Nexus ZIP path, checksum path/hash, branch, commit, PR/merge status, CI state, and the remaining real-MO2 runtime caveat.
