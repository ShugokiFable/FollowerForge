# FollowerForge 3.6.0 UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the seven-step wizard with the approved Studio, Focus Card, and Expert Deck workspace while preserving every 3.5.0 follower-profile and build contract.

**Architecture:** Keep `WizardWindow` as the single orchestration owner so catalogue-generation cancellation, manager switching, profile construction, and builds do not fork into a second model. Extract deterministic UI state into small tested classes: UI preferences and theme resources, section navigation/readiness, Expert Deck search/selection, and the field-migration ledger. Rebuild the XAML shell around the existing named inputs and handlers, then move dense record work into one reusable Deck overlay.

**Tech Stack:** C# 14, .NET 10, Avalonia 12.1.0, Avalonia DataGrid 12.1.0, xUnit 2.5.3, PowerShell release scripts.

## Global Constraints

- Modify only `FollowerForge 3.6.0`; `FollowerForge 3.5.0` remains immutable.
- Preserve profile JSON, build pipeline, CLI, Vortex/MO2 discovery, FaceGen, dialogue, transformation, output, and write-guard behavior.
- Preserve the existing generation check that prevents cancelled manager loads from overwriting newer state.
- Keep UI preferences isolated in `%LOCALAPPDATA%\FollowerForge\ui-settings.json`; never rewrite existing app or MO2 settings.
- Support 1040x700 and larger windows; ordinary forms may not require horizontal scrolling.
- Every status uses text plus an icon/word; color is supplementary.
- All production changes use a witnessed red-green test cycle.
- Publication is excluded until the user explicitly authorizes push and GitHub release creation.

---

### Task 1: UI preferences and semantic theme resources

**Files:**
- Create: `src/Ui/UiPreferences.cs`
- Create: `src/Ui/ThemeResources.cs`
- Create: `src/Tests/UiPreferencesTests.cs`
- Modify: `src/Ui/App.axaml`
- Modify: `src/Ui/App.axaml.cs`

**Interfaces:**
- Produces: `UiTheme`, `ExperienceMode`, `WindowPlacement`, and `UiPreferences`.
- Produces: `UiPreferencesStore.Load(string? path = null, Action<string>? warning = null)` and `Save(UiPreferences value, string? path = null)`.
- Produces: `ThemeResources.Apply(Application application, UiTheme theme)` and `ThemeResources.Palette(UiTheme theme)`.
- Consumes: the window constructor accepts the loaded `UiPreferences` instance.

- [ ] **Step 1: Write failing preference tests**

  Add xUnit tests using a unique temporary directory. Assert missing and malformed JSON return schema 1, Obsidian Gold, Guided, and 1320x900; valid JSON round-trips all five themes, experience, expert-introduction state, and window state; unknown enum strings fall back; saved bytes have no UTF-8 BOM; a failed replacement leaves either the old valid file or the new valid file, never a partial JSON file.

- [ ] **Step 2: Run the focused tests and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UiPreferencesTests` and require failure because `UiPreferencesStore` and `ThemeResources` do not exist.

- [ ] **Step 3: Implement atomic preferences**

  Serialize camel-case schema 1 JSON to a sibling temporary file with `new UTF8Encoding(false)`, flush it, then replace/move it into place. Parse enum names case-insensitively and clamp width/height to at least 1040x700. Catch malformed or unsupported content, issue a warning without exposing secrets, and return defaults.

- [ ] **Step 4: Implement semantic theme tokens**

  Define window, surface, elevated surface, border, text, muted text, accent, accent hover, accent pressed, success, warning, danger, focus, selection, overlay, radius, and spacing resources. Supply literal palettes for Obsidian Gold, Arcane Amethyst, Nordic Frost, Forge Teal, and Light. Change only resource values when a theme is applied.

- [ ] **Step 5: Load preferences before creating the window**

  In `App.OnFrameworkInitializationCompleted`, load preferences, apply their theme resources, and construct `new WizardWindow(preferences)`. Keep a parameterless constructor for XAML tooling that delegates to defaults.

- [ ] **Step 6: Run focused and full tests**

  Run the focused filter, then `dotnet test src/FollowerForge.slnx -c Release --no-restore`. Require all parent tests plus the new preference tests to pass.

### Task 2: Section navigation, readiness, and Studio recommendations

**Files:**
- Create: `src/Ui/WorkspaceNavigation.cs`
- Create: `src/Ui/WorkspaceReadiness.cs`
- Create: `src/Tests/WorkspaceNavigationTests.cs`
- Create: `src/Tests/WorkspaceReadinessTests.cs`
- Modify: `src/Ui/WizardWindow.axaml.cs`

**Interfaces:**
- Produces: `WorkspaceSection` with Studio plus seven stable categories.
- Produces: `WorkspaceNavigator.Open(WorkspaceSection)`, `OpenShortcut(int)`, `Back()`, and `Current`.
- Produces: `ReadinessLevel`, `CategoryReadiness`, `WorkspaceDraftSummary`, and `WorkspaceReadiness.Evaluate(WorkspaceDraftSummary)`.
- Produces: `WorkspaceReadiness.NextRecommended(IReadOnlyList<CategoryReadiness>)` with error, warning, incomplete, then review ordering.

- [ ] **Step 1: Write failing navigation tests**

  Assert Ctrl+0 maps to Studio, Ctrl+1 through Ctrl+7 map to categories in specification order, out-of-range shortcuts do not change the current section, and Back returns to the prior section without mutating draft state.

- [ ] **Step 2: Write failing readiness tests**

  Use literal summaries to prove missing name is an Identity error, unavailable environment is an Appearance/record-loading blocker, missing optional gear is informational, a usable minimum draft reaches Review, and the recommendation chooses the first error before warnings and incomplete categories.

- [ ] **Step 3: Run focused tests and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WorkspaceNavigationTests|FullyQualifiedName~WorkspaceReadinessTests"` and require missing-type failures.

- [ ] **Step 4: Implement deterministic navigation and readiness**

  Keep these classes free of Avalonia controls. Return immutable readiness records containing section, level, visible status text, concise summary, and recommended action. UI guidance may be stricter than optionality but may not claim a build is valid without pipeline validation.

- [ ] **Step 5: Integrate one window-level state owner**

  Add `_navigator`, `_preferences`, and one `RefreshWorkspaceChrome()` path in `WizardWindow`. It reads current controls into `WorkspaceDraftSummary`, updates Studio cards, left-nav status text, persistent dossier, follower/plugin title, environment readiness, and the next action. Do not create a second `FollowerProfile` draft.

- [ ] **Step 6: Verify focused and full tests**

  Run both focused filters and the complete Release suite.

### Task 3: Reusable Expert Deck state and selection preservation

**Files:**
- Create: `src/Ui/ExpertDeck.cs`
- Create: `src/Tests/ExpertDeckTests.cs`
- Modify: `src/Ui/PickerItem.cs`
- Modify: `src/Ui/WizardWindow.axaml.cs`

**Interfaces:**
- Produces: `DeckSelectionMode`, `DeckRecord`, and `ExpertDeckSession`.
- `DeckRecord` exposes `Key`, `Display`, `Detail`, `Badge`, `Plugin`, `EditorId`, `Source`, and `IsSelected`.
- `ExpertDeckSession.Filter(string?)` searches display, EditorID/detail, plugin, and full FormID key.
- `ExpertDeckSession.SetSelected(string key, bool selected)` enforces single or multi selection and returns a visible selection cart.
- `ExpertDeckSession.Commit()` returns immutable selected keys; `Cancel()` leaves original keys unchanged.

- [ ] **Step 1: Write failing Deck tests**

  Assert literal records match by display name, EditorID, plugin, and `XXXXXX:Plugin.esp`; unmatched filters expose an empty-state message; single mode replaces selection; multi mode preserves selections hidden by a filter; cancel returns the original set; commit returns the edited set in stable display order.

- [ ] **Step 2: Run the focused test and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ExpertDeckTests` and require missing-type failures.

- [ ] **Step 3: Implement Deck state without UI dependencies**

  Parse plugin and local identifier from the existing FormKey representation without changing that representation. Keep selection keyed case-insensitively so filtering and refreshed object instances cannot discard choices.

- [ ] **Step 4: Add the reusable Deck overlay behavior**

  `WizardWindow` opens one Deck for race, voice, class, combat style, armor families, weapons, ammo, belongings, spells, perks, outfit, skin, transform race/spell, faces, and locations. Apply synchronizes the original named control or remembered `HashSet`; Cancel and Escape restore the original keys; the opener regains focus.

- [ ] **Step 5: Verify focused and full tests**

  Run the Deck filter and complete Release suite.

### Task 4: Replace the wizard shell with Studio, category navigation, dossier, and command palette

**Files:**
- Modify: `src/Ui/WizardWindow.axaml`
- Modify: `src/Ui/WizardWindow.axaml.cs`
- Create: `src/Tests/WorkspaceLayoutTests.cs`

**Interfaces:**
- Consumes: `WorkspaceNavigator`, `WorkspaceReadiness`, `UiPreferencesStore`, and `ThemeResources`.
- Produces named shell controls `StudioPage`, `CategoryHost`, `DossierPanel`, `DossierDrawer`, `CommandPaletteOverlay`, `DeckOverlay`, `TopFollowerName`, `AutosaveState`, `EnvironmentState`, and `ExperienceButton`.
- Preserves every existing input/action name listed by the migration ledger in Task 7.

- [ ] **Step 1: Write failing structural layout tests**

  Parse `WizardWindow.axaml` with `XDocument`. Assert minimum width/height 1040/700, no `TabControl` elements, semantic DynamicResource brushes on the shell, eight route surfaces, seven category status labels, a dossier, command palette, Deck DataGrid, and no ordinary form wider than the minimum window.

- [ ] **Step 2: Run the focused layout tests and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorkspaceLayoutTests`; the inherited wizard must fail on TabControls and missing shell surfaces.

- [ ] **Step 3: Rebuild the shell XAML**

  Use a root Grid with persistent top bar, left navigation, main category host, responsive dossier, bottom action region, and overlay layer. Use semantic resources for every shell surface and status color. Keep control targets at least 36 pixels, Deck rows at least 32 pixels, and accessible text labels on icon buttons.

- [ ] **Step 4: Implement navigation and keyboard commands**

  Handle Ctrl+0 through Ctrl+7, Ctrl+K, Ctrl+E, Escape, Enter, and window SizeChanged. Below 1180 pixels hide the fixed dossier and expose its drawer button. The command palette routes to Studio/categories and invokes manager, paths, theme, experience, review, and build commands.

- [ ] **Step 5: Implement persistence and autosave-state presentation**

  Save UI preferences on theme/experience/window changes and closing. Clamp restored size to current screen working area. This autosave status refers only to UI preferences; follower-profile serialization remains unchanged and is never falsely presented as saved when no profile-save feature exists.

- [ ] **Step 6: Run layout, full tests, and a Release build**

  Run the layout filter, complete tests, and `dotnet build src/FollowerForge.slnx -c Release --no-restore` with zero warnings and errors.

### Task 5: Convert category content to Focus Cards and connect dense choices to the Deck

**Files:**
- Modify: `src/Ui/WizardWindow.axaml`
- Modify: `src/Ui/WizardWindow.axaml.cs`
- Modify: `src/Ui/WizardCopy.cs`
- Create: `src/Tests/FocusRoutingTests.cs`

**Interfaces:**
- Produces: `DensePickerFamily` and `FocusRouting.DefaultSurface(WorkspaceSection, ExperienceMode)`.
- Guided returns the category Focus surface; Expert returns the category’s primary Deck family for Appearance, Voice, Combat, Loadout, and Placement.
- Every Focus card uses the same existing named control that `BuildProfile()` reads.

- [ ] **Step 1: Write failing Focus-routing tests**

  Assert Guided opens Focus for all categories; Expert opens Race, Voice, Class, Armor, and Location Decks for their dense categories; Identity and Review remain Focus surfaces; toggling experience never changes the resulting `FollowerProfile` values.

- [ ] **Step 2: Run the focused tests and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FocusRoutingTests` and require missing-type failures.

- [ ] **Step 3: Recompose each category without nested navigation**

  Identity contains basic identity cards plus advanced progression/relationships. Appearance contains face, race, vampire, and body cards. Voice contains voice and custom-dialogue expanders. Combat contains class/style/temper, skills/stats, evolution, and transformation expanders. Loadout contains armor, weapons, ammo, belongings, magic, legacy outfit, and body cards. Placement contains primary place, routine, alternate spawn, and enemy-to-ally cards. Review contains dossier, grouped findings, asset mode, build controls, and build log.

- [ ] **Step 4: Add recommended cards and Browse full catalogue actions**

  Show at most five ranked/recent rows in a Focus choice list where practical, preserve current selection, and route the full list to the Deck. Every dense family exposes a text-labelled Deck action. Multi-select families display selected-count text outside color-only chips.

- [ ] **Step 5: Keep Expert mode capability-equivalent**

  In Expert mode, category entry opens the primary Deck family once, while Focus remains reachable and all advanced controls remain available. Persist the preference separately from follower data.

- [ ] **Step 6: Run focused and complete tests**

  Run Focus-routing tests and the full Release suite.

### Task 6: Readiness, empty/error states, and build-result grouping

**Files:**
- Modify: `src/Ui/WizardWindow.axaml`
- Modify: `src/Ui/WizardWindow.axaml.cs`
- Modify: `src/Ui/WorkspaceReadiness.cs`
- Create: `src/Tests/WorkspaceErrorStateTests.cs`

**Interfaces:**
- Produces: stable text states `Complete`, `Needs attention`, `Error`, and `In progress`.
- Produces: Deck empty state that names the active filter and clears it without discarding selection.
- Review presents `Must fix`, `Check before building`, and `Information` groups from actual validation/build findings.

- [ ] **Step 1: Write failing error-state tests**

  Assert manager/indexing errors remain actionable, no-result text does not claim absence from the game, a failed UI-preference save leaves the current draft untouched, and validation severities map to the three Review groups.

- [ ] **Step 2: Run the focused tests and witness RED**

  Run `dotnet test src/Tests/FollowerForge.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorkspaceErrorStateTests` and require missing mapping behavior.

- [ ] **Step 3: Implement visible recovery and progress states**

  Keep manager switch, MO2 setup, paths setup, retry, and diagnostic text visible during startup/indexing failures. Preserve generation cancellation. Show exact failed paths/operations from caught exceptions without credentials.

- [ ] **Step 4: Group build findings on Review**

  Keep the exact existing build result and output directory behavior. In addition to the text log, populate visible issue groups from the result validation findings. Do not call structural success runtime confirmation.

- [ ] **Step 5: Verify focused and full tests**

  Run the error-state filter and full Release suite.

### Task 7: Field migration and semantic-regression gates

**Files:**
- Create: `src/Ui/FieldMigrationLedger.cs`
- Create: `src/Tests/FieldMigrationLedgerTests.cs`
- Create: `src/Tests/ProfileSemanticParityTests.cs`
- Modify: `src/Tests/SourceEncodingTests.cs`

**Interfaces:**
- Produces: `FieldMigrationEntry(ControlName, WorkspaceSection, Destination, Contract)` for every named 3.5.0 input and action.
- Produces: a ledger test that compares required legacy names with 3.6.0 XAML and rejects missing or duplicate named controls.
- Produces: a profile-semantic fixture proving UI navigation/theme/experience do not alter serialized follower output.

- [ ] **Step 1: Write the failing migration ledger test**

  Hand-enumerate all 3.5.0 user inputs/actions from the preserved parent XAML. Parse 3.6.0 XAML and assert every required name appears exactly once and has a non-empty category/destination/contract entry. The test fails until the ledger exists and all fields are migrated.

- [ ] **Step 2: Write the failing semantic-parity test**

  Build a literal `FollowerProfile` fixture representing identity, appearance, dialogue, combat, custom stats, evolution, transformation, equipment quantities, placement, E2A, and hub choices. Serialize before and after changes to UI preferences/navigation/Deck state and require byte-identical profile JSON.

- [ ] **Step 3: Run focused tests and witness RED**

  Run filters for `FieldMigrationLedgerTests` and `ProfileSemanticParityTests` and require missing-ledger/parity-helper failures.

- [ ] **Step 4: Implement the complete ledger and parity helper**

  Map each old input/action to one 3.6.0 Focus card, Advanced expander, Deck family, shell command, or Review action. Do not map one field to two editable controls. Record summary-only dossier entries as non-editing destinations.

- [ ] **Step 5: Extend encoding validation**

  Scan all added `.cs`, `.axaml`, `.md`, and `.txt` files for UTF-8 validity and known mojibake signatures while preserving intended Unicode punctuation.

- [ ] **Step 6: Run migration, parity, encoding, and complete tests**

  Require all new gates and the full Release suite to pass.

### Task 8: Version, package, smoke, and release-readiness gate

**Files:**
- Modify: `src/Ui/FollowerForge.Ui.csproj`
- Modify: `VERSION.txt`
- Modify: `VERSION.md`
- Modify: `README.md`
- Modify: `CHANGELOG.txt`
- Create: `NEXUS-CHANGELOG-3.6.0.txt`
- Modify: root `PLAN.md`, `STATE.md`, `DECISIONS.md`, `VALIDATION.md`, and `CHANGELOG.txt`
- Modify only through its own output: `dist/FollowerForge-3.6.0-win-x64.zip`

**Interfaces:**
- Produces: consistent 3.6.0 assembly, documentation, archive, and checksum identity.
- Consumes: `Publish-FollowerForge.ps1` exact self-contained win-x64 pipeline.

- [ ] **Step 1: Update every version surface to 3.6.0**

  Set assembly/file/informational version, snapshot version files, README headings/download names, changelog, Nexus notes, and publish archive name to 3.6.0. Remove inherited 3.4.0 text from `VERSION.md`.

- [ ] **Step 2: Run exact clean Release tests and build**

  Run `dotnet clean src/FollowerForge.slnx -c Release`, `dotnet test src/FollowerForge.slnx -c Release --no-restore`, and `dotnet build src/FollowerForge.slnx -c Release --no-restore`. Record counts, warnings, errors, SDK, and command lines.

- [ ] **Step 3: Run UI smoke checks**

  Launch from a temporary clean LocalAppData root and an existing-settings fixture. Exercise construction at 1040x700, 1320x900, and wide widths; all five themes; keyboard navigation; single and multi Deck sessions; manager recovery visibility; and clean shutdown. Use automated window construction/boot checks where interactive input is not available and label unperformed manual checks explicitly.

- [ ] **Step 4: Run semantic and safety scans**

  Compare the 3.5.0 and 3.6.0 fixture profile/build outputs semantically, scan source and generated logs for stale paths outside the active snapshot, parse shipped PowerShell scripts, scan for mojibake, and verify no writes occurred under Skyrim Data, Vortex staging, MO2 mods/profiles, or saves.

- [ ] **Step 5: Build and inspect the final archive**

  Run the publish script, start the packaged executable for the boot gate, inventory the ZIP, reject bin/obj/cache/test/temp files, confirm executable metadata and included docs say 3.6.0, and record the ZIP byte count and SHA-256.

- [ ] **Step 6: Update project ledgers and inspect Git diff**

  Record changed files, exact commands, test/build/package results, runtime status `tool-validated`, and unresolved real-user UI/game confirmation. Verify the three user-owned root Nexus files remain unstaged and byte-preserved.

- [ ] **Step 7: Stop before publication**

  Report the local archive path and checksum. Do not push commit, tag, release, or upload until the user gives explicit publication authorization.

## Self-review

- Spec coverage: all seventeen design sections map to Tasks 1 through 8; non-UI contracts are guarded by Tasks 7 and 8.
- Structure: preference/theme, navigation/readiness, Deck state, shell, category composition, error handling, parity ledger, and release gate have separate owners.
- Type consistency: `WorkspaceSection`, `ExperienceMode`, `ExpertDeckSession`, `UiPreferences`, and `FieldMigrationEntry` have one declared producer and named consumers.
- Placeholder scan: every task names exact files, commands, interfaces, expected failures, and completion evidence.
- Publication boundary: local implementation/package is authorized; remote push/release remains excluded.
