# FollowerForge decisions

## 2026-08-26 - 3.7.0: creature transformation, and the outfit branch (Claude Code)

- A creature race is excluded from the IDENTITY picker because it carries no head data, so no
  face can ever be built for it. That is a constraint on the race she IS. It does not apply to
  the race she TURNS INTO: the face belongs to her base race and FF_Transform only calls
  SetRace() once combat starts. Vanilla werewolf, already offered by this feature, is itself
  classified Creature - the exclusion was internally inconsistent.
- The transform race picker gets its OWN source, `_transformRaces`, rather than reusing the
  identity source. Four call sites had drifted apart because there was no single source to
  point at. Creatures lead it; beast forms are the point of transforming.
- The identity page's "show creature races" checkbox does NOT gate the transform picker. They
  answer different questions and must not share a switch.
- Creature rows carry FormID + EditorID. Five installed mods each ship a "BabyDragonRace"; in
  a 944-row list a bare display name identifies nothing.
- A validator must follow the same branch as the compiler it validates. FollowerCompiler picks
  chosen-OTFT XOR generated-outfit; FollowerValidator ran the generated-outfit check
  unconditionally and hard-failed a combination the compiler has always supported. When two
  pieces of code encode the same decision, one of them will be wrong eventually - the check
  now mirrors the branch explicitly and a test pins both sides.
- One situation gets one finding. LEGACY_OUTFIT_WITH_EQUIPMENT ("the engine MAY choose the
  OTFT pieces") was hedging about something certain, and fired alongside the new definite
  warning. Deleted rather than kept for compatibility.
- Warn only about what can be measured. The deferred "RaceMenu overlay warning" became a
  bodyMorphs warning because two real presets on disk carry bodyMorphs and neither carries an
  overlay block - so the overlay key name cannot be confirmed, and a guessed schema is exactly
  what the workspace rules forbid. Overlays are named inside the warning as a known
  limitation, with no claim of detection.
- Untouched sliders do not count as a body shape. RaceMenu writes the whole slider set
  regardless of use (119 present, 86 shaped in the reference preset), so counting entries
  rather than values would warn on every single build and be ignored within a week.

## 2026-08-19 - 3.6.0 release-readiness: deck apply, readiness colors, palette (Grok)

- Stay on snapshot 3.6.0. It was never a public release; 3.5.0 remains the rollback.
- Expert Deck Apply must replace only OfferedKeys (the session catalogue), never the search
  slice and never an entire shared HashSet. That is what keeps the seven armor decks and the
  four belongings slices from deleting each other.
- Readiness Error means something is broken (setup or a must-fix build). Unfinished work is
  Needs attention. Unused optional work is Optional. A red badge on an unnamed draft is a lie.
- Command palette filters on KeyUp (Avalonia Text is stale on KeyDown). Enter/arrows must not
  refilter. Escape is Handled only when an overlay is actually open.
- Race rows must carry the plugin EditorID. Friendly names like "Nord" are not NordRace.

## 2026-08-19 - 3.6.0 UI polish pass 4: verify visually, fix variant + overlay (Kimi)

- Never ship UI changes unrendered again: three passes of "polish" read fine in XAML but the
  user saw no difference. tools/UiScreenshots (Avalonia.Headless + UseSkia,
  UseHeadlessDrawing=false, CaptureRenderedFrame) renders the real WizardWindow to PNG per
  theme; the pass-4 fixes were found AND verified from those frames. Harness stays out of
  the solution and the shipped package.
- RequestedThemeVariant must follow the palette: App.axaml hardcoded Dark, so Fluent
  internals (button/glyph colors) stayed white-on-cream in the Light theme — buttons were
  literally invisible. ThemeResources.Apply now sets the Light/Dark variant with the theme,
  plus token-driven default Button/ComboBox styles as belt-and-braces.
- Overlay dim alpha 80% -> 95%: bright status text ghosted through the deck/palette dim and
  read as overlapping labels. A modal dim must be effectively opaque over saturated content.
- Fixed-height ListBoxes were both the "compressed scroll menus" and the empty black pits;
  MaxHeight everywhere now (empty collapses, populated grows). Studio cards dropped their
  fixed 280x172 sizing. Sidebar statuses use the same tinted-pill chip system as rows.

## 2026-08-18 - 3.6.0 UI polish pass 3: status colors must differ per theme (Kimi)

- User feedback on pass 2 ("no differences, error still yellow no matter the theme") exposed
  a semantic miss: chips followed theme tokens, but every theme's Warning/Danger hex was
  nearly identical amber/red, so switching themes visibly repainted nothing. A theme system
  whose status colors never change reads as broken to users even when the plumbing is right.
- Warning/Danger hues are now distinct per theme (gold/peach/straw/lime-amber/dark-amber;
  red/rose/coral/vermilion/brick); a test pins per-theme distinctness so nobody re-merges them.
- Badge chips moved to the Raycast/Linear badge idiom: translucent tinted fill (new
  SuccessSoft/InfoSoft/WarningSoft/DangerSoft tokens, ~15-18% alpha) + colored text, pill
  radius. Solid status blocks read as "stuck yellow boxes" the moment a theme looks off.
- Phase6Tests MoveDirectory flake: intermittent full-suite-only failure at FollowerBuilder's
  publish move; passes isolated and on retry; UI-only diff cannot cause it. If it recurs,
  harden the builder's delete+move with a short retry instead of blaming new work.

## 2026-08-18 - 3.6.0 UI polish pass (Kimi)

- Badge/chip theming uses Avalonia class styles + `Classes.chip-warn="{Binding ChipWarn}"`-style
  bindings on DynamicResource brushes instead of code-held IBrush fields. Static brushes cannot
  follow a runtime theme switch; the old `Chips` holder was deleted and `IPickerRow` now exposes
  ChipGood/ChipOk/ChipWarn/ChipBad/ChipDim as plain properties on every row class (default
  interface members did not resolve through the template binding path).
- Setup windows (MO2 + first-run paths) moved from hardcoded dark/gold hex to theme tokens;
  ThemeCoverageTests keep a zero-hex scan over src/Ui/*.axaml so the leak cannot return.
- Density/type values follow Raycast/Linear-style token guidance from web research: 8pt spacing
  grid, 40-46px rows, 12px card radius, rationed accent with hover/pressed states.
- The user asked for Firecrawl for the UI research; it is not installed on this machine (no MCP
  tool visible), so kimi_search_v2 was used instead. Install a Firecrawl MCP server if repeated
  design-site scraping is wanted.

## 2026-08-18 - 3.6.0 crash fix (Kimi)

- Fix inside the existing 3.6.0 WIP snapshot instead of cutting 3.6.1: 3.6.0 was never pushed
  or released anywhere, so its local archive is replaceable; 3.5.0 remains the untouched
  rollback. Version strings stay 3.6.0.
- Fix the crash at the source (`RefreshDeck` selection sync), not by catching the exception:
  Avalonia only allows `DataGrid.SelectedItems` mutation in Extended mode; single-choice decks
  now apply selection through `SelectedItem` via `DeckGridSelection.SyncSelected`.
- Cover the fix with real-control tests (a bare `DataGrid` works in the plain xunit host; no
  headless package needed). One test pins that Single-mode `SelectedItems.Clear()` throws, so
  the regression guard cannot silently rot.
- Kimi shell note: this agent's process tree strips `ProgramFiles`/`ProgramFiles(x86)`/
  `ProgramData`/`CommonProgramFiles`, which makes NuGet fail with `Value cannot be null.
  (Parameter 'path1')`. Inject them with `env` per invocation; also run
  `dotnet build-server shutdown` once so reused MSBuild nodes from a stripped-environment
  run stop flaking the restore graph.

## 2026-08-18 - 3.6.0

- Preserve 3.5.0 unchanged and perform the UI modernization only in 3.6.0.
- Use one adaptive product architecture: Studio is the home, Focus Cards are the normal
  editing surface, and Expert Deck handles dense catalogues and FormID-level work.
- Do not create separate beginner and expert applications. Both surfaces operate on the same
  in-memory follower profile and validation/build pipeline.
- Replace nested tabs and cramped inline list boxes with seven classified categories and
  dedicated record-picker workspaces.
- Keep a contextual “Browse full catalogue” action everywhere it is useful. Also remember a
  global Guided/Expert preference; Expert changes defaults, not available capability.
- Themes are token-driven and cosmetic. They must not alter navigation, validation, profile
  serialization, or build results.
- Preserve the existing public contracts: follower profile JSON, app/MO2 settings, CLI,
  indexing, plugin generation, assets, output locations, and write guards.
- Keep every legacy named input/action as the canonical editable control. Studio readiness and
  dossier fields are summaries; Expert Deck applies back to those controls instead of creating
  a second follower-data model.
- Store schema-1 UI preferences atomically in a separate LocalAppData file. A preference failure
  can affect presentation only and must never mutate the follower draft.
- Classify actual validation results into Must fix, Check before building, and Information while
  preserving the full existing build log and output-directory behavior.
- Treat the hidden packaged desktop boot as tool validation, not Skyrim runtime confirmation.
- Stop after the validated local archive; remote push/release requires separate publication authority.
- Do not launch SSEEdit or Creation Kit GUI.

## 2026-08-16 - 3.5.0

- Werewolf revert is a script bug, not a UI one. 3.4.0 is left untouched.
- Do not cast WerewolfChangeFX on followers. houseCARL + vanilla
  WerewolfTransformVisual.psc show it Wait(10) then SetRace(Werewolf) on any
  non-player target. That is why revert "worked" then they became wolves again.
- SetRace(WerewolfBeastRace) / SetRace() is the documented Actor API and is enough.
- After DelaySeconds, abort if GetCombatState()==0 so a short fight cannot
  transform them after the battle.
- OnUpdate + OnLoad are backups, not the primary path.

## 2026-08-16 - 3.4.0

- Keep 3.3.0 untouched. Three independent UX requests in one minor release.
- xVASynth: explicit path > FFORGE_XVASYNTH > saved settings > Steam library scan > default.
  The previous default-only path is why a Steam install off C: was invisible.
- Output: default stays LocalAppData\FollowerForge\workspace\builds\Name.
  A user-chosen folder publishes to Folder\Name so Vortex/MO2 can enable it as a mod.
  Staging always stays under LocalAppData. WriteGuard.Allow is only for that explicit dest.
  Game Data and My Games\Skyrim Special Edition remain rejected.
- Pronouns use named slots (subject/object/possessive). A her→him replace would break
  "her look" vs "kill her" and would also hit "published".
- Gear FormIDs use the catalogue key already stored (XXXXXX:Plugin.esp). That is the
  base ID plus plugin, which is what tells Makeshift Eyeglasses variants apart.
- Do not launch SSEEdit or Creation Kit GUI.

## 2026-07-28 - 2.1.3

- Preserve 2.1.2 unchanged and use a patch release for the custom-skill visibility repair.
- Treat the supplied screenshot as direct evidence of a UI layout failure, not a stat-data failure.
- Increase the skill value column from 82 to 150 pixels. This leaves room for the numeric text after
  Avalonia lays out both spinner buttons while retaining the three-category skill layout.
- Apply the same width as the control's `MinWidth` so parent-grid measurement cannot collapse it.
- Enforce a 140-pixel minimum through a focused regression test.
- Do not alter presets, allowed ranges, AutoCalcStats defaults, or DNAM serialization in this patch.
- Do not launch SSEEdit or Creation Kit GUI.

## 2026-07-28 - 2.1.2

- Preserve 2.1.1 unchanged because the user confirmed it works in Skyrim.
- Use patch version 2.1.2 for the optional skill/stat editor.
- Keep AutoCalcStats enabled by default. The class and level remain the easy recommended workflow.
- Custom mode is explicit and writes every DNAM skill key plus Health, Magicka, and Stamina.
- Write exact skill values with all DNAM skill offsets set to zero so no hidden adjustment changes what the editor shows.
- Keep the selected class even in custom mode for compatibility and classification; custom DNAM values control the serialized starting stats.
- Limit the friendly skill editor to Skyrim's normal 0-100 skill range while preserving the full unsigned 16-bit storage range for Health, Magicka, and Stamina.
- Treat presets as editable starting points, not locked classes.
- Keep pre-2.1.2 profiles automatic through the default-initialized Stats property.

## 2026-07-28

- Use patch version 2.1.1 because this completes and repairs the unreleased 2.1 equipment/appearance implementation without replacing its user workflow.
- Preserve `FollowerForge 2.1.0` unchanged for rollback.
- Keep equipment-first UI semantics. Skyrim still requires an OTFT on `NPC.DefaultOutfit` to decide initial worn armor, so the compiler creates a private OTFT from the user's selected ARMO records; it is an engine implementation detail, not a legacy outfit choice.
- Keep every equipped ARMO in NPC inventory so gear remains real, tradable equipment. Weapons remain inventory and are not placed in OTFT.
- Treat a RaceMenu export as NIF/DDS plus matching jslot record data. A missing/unreadable matching jslot is a build-stopping error.
- Prefer `formIdentifier` when resolving jslot head parts; it preserves dependencies omitted from RaceMenu's mods array, including light/overlay plugins.
- Convert RaceMenu tint ARGB alpha to Skyrim TINV as `alpha / 255`, preserving partial makeup and complexion strength.
- Resolve FaceGen textures against the catalogue first, then the deployed Data tree; reject traversal outside Data.
- Preserve installed matching face/body textures by default. Never retarget head-only skin support maps unless a complete matched head-and-body skin set is available.
- Repair only disposable FollowerForge SQLite cache files: close handles, move the DB/WAL/SHM aside with a `.broken-<timestamp>` suffix, then rebuild once. Never loop or hide non-cache failures.
- Treat FollowerForge NG and NPCMANAGER as read-only references. NPCMANAGER's portrait is a Fallout 4 OpenTK/NiflySharp renderer with a full material/skin pipeline; a Skyrim portrait preview is deferred rather than importing incompatible code.
- Do not launch SSEEdit or Creation Kit GUI.
