# FollowerForge 3.6.0 UI Redesign Specification

**Status:** Architecture approved; written specification awaiting final user review  
**Date:** 2026-08-18  
**Parent:** FollowerForge 3.5.0  
**Target:** FollowerForge 3.6.0  

## 1. Purpose

Replace the current cramped seven-page Avalonia wizard with a modern, classified desktop
workspace that remains approachable for ordinary users and fast for expert mod authors.
The redesign must preserve every 3.5.0 follower-building capability and every non-UI public
contract. It is a frontend modernization, not a rewrite of plugin generation, indexing,
FaceGen, dialogue, transformation, mod-manager discovery, or packaging.

The approved product model is:

> **Studio dashboard → Focus Cards → Expert Deck**

- **Studio** is the default home and shows the follower, category readiness, environment,
  autosave state, and the next useful action.
- **Focus Cards** are the normal editing surface. Common choices are shown as a small number
  of large, legible cards, with advanced fields folded behind a deliberate drawer.
- **Expert Deck** is the dense record browser. It appears when the user selects “Browse full
  catalogue” or enables the remembered Expert preference.

These are three views of one follower draft, not three applications or three data models.

## 2. Goals and non-goals

### Goals

1. Eliminate nested tab mazes, squeezed list boxes, and long pages of unrelated controls.
2. Make the current state and missing requirements understandable without visiting every page.
3. Keep ordinary decisions calm while making large installed-record catalogues genuinely fast.
4. Offer selectable visual themes without changing information architecture or behavior.
5. Preserve all existing profile, build, CLI, path, MO2/Vortex, and generated-mod behavior.
6. Remain usable at the current 1040×700 minimum while taking advantage of larger screens.
7. Make every action keyboard reachable and every status understandable without color alone.

### Non-goals

- No changes to ESP/ESPFE record generation, FormIDs, scripts, FaceGen output, generated assets,
  dialogue compilation, transformation behavior, CLI syntax, or package layout.
- No removal of advanced fields merely to simplify the default view.
- No separate “beginner edition” and “expert edition.”
- No web frontend, embedded browser, Creation Kit, SSEEdit, or new runtime service.
- No visual portrait renderer in 3.6.0; record and asset metadata remain the available preview.

## 3. Information architecture

The left navigation contains seven stable categories:

1. **Identity & progression**
   - Name, sex/pronouns, protection/mortality, relationship and kin, marriage, level behavior,
     evolution, and other identity/progression settings.
2. **Appearance**
   - RaceMenu export, race, custom/creature race visibility, vampire state, skin/body choices,
     and face-quality guidance.
3. **Voice & dialogue**
   - Voice type and coverage, custom lines, triggers/context, synthesis, and voice assets.
4. **Combat, skills & transformation**
   - Class, combat style, temper, stats/skills, spells/perks where contextually relevant,
     evolution, and transformation options.
5. **Loadout**
   - Armor/accessories, weapons, ammunition and quantities, belongings, books/lore,
     ingestibles/ingredients, spells/perks, and body/skin asset selections.
6. **Placement & routines**
   - Starting location, idle/routine behavior, alternate/random spawns, E2A, and hub choices.
7. **Review, validation & build**
   - Complete dossier, warnings/errors, asset/dependency summary, output destination, build,
     package location, and build history for the current session.

No category may contain a second full navigation system. Small related subareas may use a
segmented control or local section list, but large record selection always opens the dedicated
Deck workspace.

## 4. Application shell

### Persistent top bar

The top bar is visible on every surface and contains:

- FollowerForge identity and current follower/mod name.
- Autosave state: Saved, Saving, Unsaved, or Save failed.
- Environment readiness: Vortex/MO2, selected MO2 profile where applicable, catalogue state,
  and a direct setup action when attention is required.
- Guided/Expert preference.
- Theme menu.
- Command/search entry (`Ctrl+K`) for navigation and common commands.

Environment setup must remain reachable before or during indexing, preserving the 3.5.0 fix
that prevents MO2 users from being trapped behind a Vortex-first load.

### Left navigation

- Studio overview followed by the seven categories.
- Each category shows a text label plus Complete, Needs attention, Error, or In progress.
- Status uses icon + text, never color alone.
- Navigation never discards edits and never triggers a catalogue rebuild.

### Persistent dossier

At wide widths, the right side shows the follower’s essential choices and readiness. At narrow
widths it collapses into a clearly labeled drawer. It summarizes data; edits open the owning
category so that the same field is not implemented twice.

### Bottom action region

- Contextual primary action: Continue, Apply choice, Review, Fix issue, or Build follower.
- Secondary Back action where a Focus sequence needs it.
- Non-blocking status text for indexing, synthesis, and build progress.
- Long operations expose progress and cancellation where the underlying operation supports it.

## 5. Studio dashboard

The Studio is the default landing surface after environment initialization. It contains:

- Follower identity header and current output plugin name.
- Category cards with concise summaries and readiness.
- One “Next recommended action” based on the first blocking error, then warning, then incomplete
  required category.
- Environment card with manager/profile, plugin count, catalogue freshness, xVASynth status,
  and output destination.
- Review & Build card that is disabled only by actual build-blocking errors.

Readiness must derive from the same validation rules used by the build pipeline wherever
possible. UI-only completeness checks may improve guidance, but they cannot contradict the
final validator.

## 6. Focus Cards

Focus mode is used for common, bounded decisions. A Focus surface contains:

- One plain-language question.
- Context and recommendation reason.
- Normally three to five large choice cards.
- Current selection with a strong outline/check mark.
- “Find another…” or “Browse full catalogue” when more records exist.
- One Advanced drawer for infrequent but related fields.
- Explicit apply/continue action for decisions with consequences; immediate selection is
  allowed only when it is safely reversible and already behaves that way in 3.5.0.

Cards are not forced onto dense data. Installed armor, weapons, spells, perks, locations,
voices, races, and other large sets use a few ranked/recent cards at most, then open the Deck.

## 7. Expert Deck

The Deck is a reusable full-size record workspace:

### Left tree

- Current category and record family.
- Selected-count badges for multi-select families.
- No unrelated application settings.

### Main data surface

- Virtualized `DataGrid` or equivalent virtualized list.
- Search across display name, EditorID, and `XXXXXX:Plugin.esp` FormID representation.
- Sort and filters appropriate to the family: plugin, slot, compatibility/suitability,
  selected state, record type, or coverage.
- Single selection for race, voice, class, style, and location.
- Multi-selection with a visible selection cart for armor, weapons, ammo, belongings, spells,
  perks, lore, and other collection families.
- Quantity editing remains available for ammunition and belongings.

### Inspector

- Display name, record type, plugin, FormID, EditorID where different, suitability badge,
  explanatory detail, and relevant conflicts/requirements.
- Add/remove/apply action.
- At narrow widths the inspector becomes a drawer, not a permanently squeezed third column.

### Entry and exit

- Any compatible Focus view may open the Deck with its current filter and selection.
- Closing/applying returns to the exact category and preserves scroll/selection state.
- Expert preference makes Deck-first behavior the default for dense pickers; it does not hide
  Focus views or create different follower output.

## 8. Visual system and themes

All visuals use semantic Avalonia resources rather than hardcoded brushes in individual views.
Required token families include window, surface, elevated surface, border, text, muted text,
accent, accent hover/pressed, success, warning, danger, focus, selection, overlay, spacing,
corner radius, and elevation.

### Themes

1. **Obsidian Gold** — default; charcoal/black surfaces with restrained warm gold accents.
2. **Arcane Amethyst** — deep graphite with violet/amethyst accents.
3. **Nordic Frost** — blue-black surfaces with cool ice-blue accents.
4. **Forge Teal** — neutral dark surfaces with teal accents.
5. **Light** — warm neutral light surfaces with dark text and a restrained accent.

Themes change only semantic tokens. Layout, labels, icons, validation severity, capability, and
saved follower output remain identical. Success/warning/error keep consistent meanings and pass
contrast requirements in every theme.

### Shape and typography

- Use the platform UI font stack; no bundled decorative font dependency.
- Clear hierarchy: page title, section title, field label, supporting text, metadata.
- Default control target height at least 36 px; compact rows remain at least 32 px in the Deck.
- Moderate 8–12 px radii, thin borders, restrained shadows, and generous 8/12/16/24/32 spacing.
- Avoid excessive gradients, glow, glass blur, fake-metal textures, and ornamental Skyrim UI
  clichés. The product should feel like a modern creator tool, not a game launcher skin.

## 9. Settings and persistence

UI preferences are stored separately from existing path and MO2 settings so 3.5.0 contracts are
not destabilized.

Proposed `%LOCALAPPDATA%\FollowerForge\ui-settings.json` schema 1:

```json
{
  "schemaVersion": 1,
  "theme": "ObsidianGold",
  "experience": "Guided",
  "window": {
    "width": 1320,
    "height": 900,
    "maximized": false
  }
}
```

- Missing, malformed, or unknown values fall back safely to Obsidian Gold + Guided.
- Settings are written atomically with UTF-8 without BOM, matching existing settings safety.
- Existing `app-settings.json` and MO2 selection files are not migrated or rewritten.
- Follower profile serialization is unchanged.

## 10. State and data flow

The redesign separates presentation from the existing orchestration without rewriting the
build pipeline:

1. One window-level workspace state owns the current draft, environment snapshot, catalogue
   lists, selection sets, loading state, navigation state, and validation summary.
2. Studio, Focus views, and Deck bind to that same state.
3. Existing loaders, filters, controllers, validators, and builders remain the behavior source.
4. View-specific code formats and routes data; it does not duplicate compiler rules.
5. Async indexing/build results are generation-checked as in 3.5.0 so cancelled manager loads
   cannot overwrite newer state.

Implementation may incrementally extract the 3.5.0 `WizardWindow` code-behind into small
presenters/view models using `INotifyPropertyChanged`; it must not introduce a second parallel
draft model. New third-party MVVM frameworks are unnecessary unless implementation evidence
shows a concrete need.

## 11. Error, loading, and empty states

- Startup opens the shell immediately with a visible environment/indexing state.
- Blocking environment errors use an inline recovery panel with MO2 setup, manager switch,
  path setup, retry, and diagnostic copy actions as appropriate.
- Category errors appear on the card, navigation item, owning field, and Review page.
- Review groups issues into Must fix, Check before building, and Information.
- Empty searches explain the active filters and offer Clear filters.
- No catalogue result is never presented as proof that the record type does not exist.
- Save/build failures preserve the draft and show the exact failed path or operation without
  exposing secrets.
- Toasts are reserved for completed transient actions; actionable failures remain visible.

## 12. Responsive behavior

- Supported minimum remains 1040×700.
- At widths below approximately 1180 px, the dossier and Deck inspector become drawers.
- Category cards move from three/two columns to one column as required.
- The left navigation may collapse to icons only only when every icon has an accessible name
  and an obvious expand control.
- No horizontal scrolling is allowed for ordinary forms. The Deck may horizontally scroll a
  data grid only when optional expert columns are enabled.
- Window size/state is remembered, but startup clamps it to the current monitor work area.

## 13. Accessibility and keyboard behavior

- Every interactive control has a visible focus state and accessible name.
- `Ctrl+K`: command/search palette.
- `Ctrl+1` through `Ctrl+7`: open categories; `Ctrl+0`: Studio overview.
- `Ctrl+E`: toggle the remembered Guided/Expert preference after confirmation the first time.
- Escape closes the topmost drawer/dialog/Deck and returns focus to its opener.
- Enter activates the focused primary action; Space toggles focused selection cards/checks.
- Data grids support arrows, Page Up/Down, Home/End, and keyboard selection.
- Status is conveyed with icon/text in addition to color.
- Theme palettes must meet WCAG AA contrast for normal text and visible focus indicators.

## 14. Compatibility and preservation ledger

The following 3.5.0 behavior must remain unchanged unless a separately proven defect is found:

- Vortex and MO2 discovery, explicit MO2 INI/profile override, manager switching, and safety.
- Catalogue creation/rebuild/recovery and installed-record suitability/ranking.
- RaceMenu NIF/DDS/jslot matching, FaceGen generation, tint/skin behavior, and asset validation.
- Voice coverage, custom dialogue, synthesis, xVASynth paths, and output paths.
- Race, custom/creature visibility, vampire state, class/style, custom skills/stats, evolution,
  transformations, gear, quantities, spells/perks, placement, alternate spawns, E2A, and hubs.
- Follower profile JSON, generated ESPFE and assets, CLI commands, package structure, and write
  guards.
- Gendered copy/pronoun behavior and record FormID display/search.

Before removing the old XAML, implementation must maintain a field migration ledger mapping
every named 3.5.0 input/action to its 3.6.0 destination. Any unmapped field blocks release.

## 15. Verification strategy

### Automated

- Preserve and pass all 388 parent tests before accepting intentional test updates.
- Add tests for theme parsing/fallback and atomic preference persistence.
- Add tests for category routing, readiness aggregation, Guided/Expert routing, and preserved
  Deck selections.
- Add source/layout tests for minimum control sizes and absence of nested navigation tabs.
- Add a field migration ledger test that fails if a required 3.5.0 control/action is missing.
- Preserve UTF-8/source-encoding tests and scan generated sources for mojibake.
- Build Release and self-contained `win-x64` using the exact project lock/dependencies.

### UI smoke checks

- Launch on a clean settings directory and on existing 3.5.0 settings.
- Exercise all five themes at 1040×700, 1320×900, and a large desktop width.
- Navigate every category by mouse and keyboard.
- Open single- and multi-select Decks; filter by name, plugin, EditorID, and FormID.
- Switch Vortex/MO2 while indexing and confirm the latest generation owns the UI.
- Build the same fixture/profile with 3.5.0 and 3.6.0 and compare semantic output.
- Confirm no project write touches Skyrim Data, Vortex staging, MO2 mods/profiles, or saves.

### Release gate

- Final archive starts, indexes, opens all categories, builds a fixture follower, and contains
  no temp/test/cache files.
- README, changelog, Nexus notes, version strings, executable metadata, archive name, and GitHub
  tag agree on 3.6.0.
- Final ZIP SHA-256 is recorded and rechecked against the published GitHub release asset.
- Runtime status remains `tool-validated` until a real user runs the released application.

## 16. Rollback

- FollowerForge 3.5.0 remains immutable and is the full rollback release.
- UI preferences are isolated in `ui-settings.json`; deleting that file resets the UI without
  affecting paths, MO2 selection, follower profiles, generated mods, or saves.
- A 3.6.0 failure must not require changing or deleting generated follower projects.
- If the new shell cannot meet the field-migration or semantic-output gates, do not publish it;
  restore `CURRENT.txt` to 3.5.0 and retain 3.6.0 as an unshipped work snapshot.

## 17. Approved visual reference

The approved mockup is the “Studio → Focus Cards → Expert Deck” hybrid shown in the Codex visual
companion on 2026-08-18. It defines the hierarchy and density model, not pixel-perfect colors or
placeholder record content. Final implementation must follow this specification and the real
3.5.0 field inventory.
