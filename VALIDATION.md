# VALIDATION — FollowerForge 3.6.0

## Snapshot gate

```text
Parent: FollowerForge 3.5.0
Active: FollowerForge 3.6.0
Robocopy: 1,473 copied, 0 failed, 0 mismatched
```

## 3.6.0 implementation gate

- Studio → Focus Cards → Expert Deck approved design implemented.
- Five selectable semantic palettes implemented: Obsidian Gold, Arcane Amethyst, Nordic Frost,
  Forge Teal, and Light.
- Guided/Expert routing, command palette, keyboard navigation, responsive dossier, atomic UI
  preferences, actionable startup states, and grouped build findings implemented.
- All 158 named 3.5.0 controls preserved exactly once by the field-migration gate.

## Commands and results

```text
dotnet clean src/FollowerForge.slnx -c Release
  -> PASS, 0 warnings, 0 errors

dotnet test src/FollowerForge.slnx -c Release --no-restore
  -> PASS, 441 passed, 0 failed, 0 skipped

dotnet build src/FollowerForge.slnx -c Release --no-restore
  -> PASS, 0 warnings, 0 errors

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, self-contained app and CLI published
  -> hidden FollowerForge.exe boot stayed alive for 12 seconds
```

## Crash fix re-validation (2026-08-18, Kimi)

Crash evidence: Windows Application log event 1000/1026, FollowerForge.exe 3.6.0.0,
2026-08-18 02:24 — InvalidOperationException at DataGridSelectedItemsCollection.Clear()
from WizardWindow.RefreshDeck <- OpenDeck <- OnOpenDeck (button Click).

```text
dotnet test src/Tests -c Release --filter FullyQualifiedName~ExpertDeckTests
  -> PASS, 15 passed (incl. 4 new: single-mode Clear() pins the throw,
     SyncSelected single/extended/empty)

.\Build-FollowerForge.ps1            (clean + Release build + tests + CLI boot)
  -> PASS, 445 passed, 0 failed, 0 skipped; 0 warnings, 0 errors; CLI OK

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, self-contained app and CLI published
  -> hidden FollowerForge.exe boot stayed alive for 12 seconds
  -> staged docs refreshed (crash-fix changelog + Nexus note) and archive re-zipped
```

Environment note (Kimi shell): `ProgramFiles`, `ProgramFiles(x86)`, `ProgramData`, and
`CommonProgramFiles` are absent from this agent's process tree; NuGet then fails with
`Value cannot be null. (Parameter 'path1')`. All dotnet commands above ran with those
variables injected via `env`, after `dotnet build-server shutdown` cleared stale build nodes.
Codex/PowerShell sessions with a normal user environment do not need this workaround.

## UI polish re-validation (2026-08-18, Kimi)

Scope: density/clarity pass on WizardWindow.axaml, theme-leak fixes (chip class styles via
DynamicResource binding; MO2/paths setup windows de-hardcoded), ThemePalette Info/OnStatus
slots, new ThemeCoverageTests, extended UiPreferencesTests.

```text
dotnet test src/FollowerForge.slnx -c Release
  -> PASS, 454 passed, 0 failed, 0 skipped

.\Build-FollowerForge.ps1            (clean + Release build + tests + CLI boot)
  -> PASS, 454 passed, 0 failed; 0 warnings, 0 errors; CLI OK

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, self-contained app and CLI published
  -> hidden FollowerForge.exe boot stayed alive for 12 seconds
  -> staged docs refreshed (UI polish changelog + Nexus note) and archive re-zipped
```

Theme-leak evidence: grep for `SolidColorBrush|Brushes\.|Color\.Parse` across src/Ui hits
only ThemeResources.cs (the token factory). ThemeCoverageTests pin zero hex brushes in Ui
XAML, palette Info/OnStatus completeness + contrast sanity, and chip-class wiring.
Not runtime-confirmed: live chip repaint on theme switch is reasoned from Avalonia
DynamicResource semantics; user should flip themes and watch the badge chips.

## UI polish pass 3 re-validation (2026-08-18, Kimi)

User feedback on pass 2: "no differences... error is still yellow no matter the theme".
Root cause: pass 2 fixed the plumbing (chips follow theme tokens) but every theme's
Warning/Danger tokens held nearly identical amber/red hex, so theme switches visibly
repainted nothing. Fix: per-theme distinct Warning/Danger hues + Raycast/Linear-style
tinted badge chips (translucent Soft fills + colored text, pill radius).

```text
dotnet test src/Tests -c Release
  -> PASS, 456 passed, 0 failed, 0 skipped (454 + status-distinctness + soft-alpha pins)

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, self-contained app and CLI published
  -> hidden FollowerForge.exe boot stayed alive for 12 seconds
  -> archive rebuilt by the script itself (flat root layout, 5 entries)
```

Flake note: Phase6Tests Batch_BuildsAllProfiles / NormalFollower_HasNoDuplicateEditorIds
failed intermittently this session inside FollowerBuilder's final Directory.Move. They pass
isolated and on full-suite retries (456/456 green twice after); signature is external file
lock contention on %TEMP% builds, unrelated to the UI-only pass-3 diff. Worth a retry-loop
hardening in the builder if it recurs.

## UI polish pass 4 re-validation (2026-08-19, Kimi)

User feedback on pass 3: "chips overlay the yellow error labels on the left; 0 work on
blocky/compacted". Root causes found by rendering the real window headlessly (new
tools/UiScreenshots harness, Avalonia Headless + Skia -> PNG per theme):
  - OverlayBrush was 80% opaque: sidebar status labels ghosted through deck/palette dim.
  - App.axaml hardcoded RequestedThemeVariant=Dark: Light-theme buttons were white-on-cream.
  - Fixed-height lists/cards produced the "blocky, compacted" look and dead-space pits.
Fixes: ~95% opaque overlay, Fluent variant follows the palette, token-driven default
Button/ComboBox styles, sidebar status pills, auto-height cards, MaxHeight lists,
flattened deck panels.

```text
dotnet run --project tools/UiScreenshots -c Release
  -> PASS, 15 PNGs (5 themes x studio/appearance/deck); overlay bleed and Light-theme
     buttons visually confirmed fixed in the rendered frames

.\Build-FollowerForge.ps1
  -> PASS, 456 passed, 0 failed; 0 warnings, 0 errors; CLI OK

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, boot check 12s; archive rebuilt by the script (flat root layout, 5 entries)
```

## UI polish pass 5 re-validation (2026-08-19, Kimi)

User feedback on pass 4 (with 2560x1440 screenshots): "space allocation and ui design is
trash in some places... overlay theme still happening". Root causes found by extending the
harness to render the Loadout category at the user's 2560x1440 with populated lists, plus a
container-painting diagnostic render (page StackPanel painted provably full-width while the
Expanders stayed narrow):
  - CategoryHost was a plain Panel: arranges children at desired size, never stretches.
  - Page ScrollViewers allowed unconstrained horizontal measure: content sized to its
    widest child instead of the viewport.
  - The Fluent Expander theme left-aligns by default: sections rendered as small chips.
Fixes: CategoryHost is now a Grid; all eight page ScrollViewers (plus sidebar) set
HorizontalScrollBarVisibility=Disabled; the Expander style sets
HorizontalAlignment/HorizontalContentAlignment=Stretch; all 26 catalogue list MaxHeights
raised ~1.5x; OverlayBrush raised from F2 to FF alpha on all five themes.

```text
dotnet run --project tools/UiScreenshots
  -> PASS, 20 PNGs (5 themes x studio/appearance/deck/loadout-1440p)
  -> loadout-1440p frames: expanders and lists fill the full content column (visually
     confirmed, dark + Light themes)
  -> deck frames: overlay ring pixels sampled with PIL = exactly (9,10,12) = FF090A0C,
     zero sidebar pill ghosting possible

.\Build-FollowerForge.ps1
  -> PASS, 456 passed, 0 failed; 0 warnings, 0 errors; CLI OK

dotnet test src/Tests -c Release --nologo   (post-publish confirmation)
  -> PASS, 456 passed, 0 failed, 0 skipped

.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, boot check "window stayed up"; archive rebuilt (flat root layout, 5 entries)
  -> pass-5 changelog confirmed inside the shipped zip
```

## Final package

```text
Path: FollowerForge 3.6.0 WIP\dist\FollowerForge-3.6.0-win-x64.zip
Bytes: 111,277,638
SHA-256: A9D302602647D3479A04D47B3DCB20FFD277EAED8EAA85DF6653F63B1675F778
Entries: 5 (flat root layout, as produced by Publish-FollowerForge.ps1)
Forbidden bin/obj/cache/test/temp/PDB entries: 0
FollowerForge.exe FileVersion: 3.6.0.0
README and Nexus notes: 3.6.0 (Nexus notes + changelog include pass-2/3/4/5 polish entries)
Supersedes pass-4 package: 99,240,266 bytes,
  SHA-256 4E0EF712BEAA2C3A1334C0D9B27AAC96D1E8AD801EB2F8ED6F8C9E4938E6688F (replaced)
Earlier packages: 99,239,699 bytes D076092D…CABA18 (pass 3, replaced);
  99,239,667 bytes 318B7F6A…E3CD15 (pass 2, replaced);
  99,239,399 bytes A182E8EE…C562559FB (crash-fix-only, replaced);
  99,738,273 bytes 296E6EA5…93A2895A (broken deck, deleted)
```

## Release-readiness re-validation (2026-08-19, Grok)

Scope: finish Claude's half-wired 3.6.0 review fixes and remaining Kimi data-loss / palette /
readiness defects. No new product surface beyond the approved Studio / Deck / palette spec.

```text
dotnet test src/Tests -c Release --nologo --filter FullyQualifiedName~WorkspaceReadiness|FullyQualifiedName~ExpertDeck|FullyQualifiedName~RaceSuitability|FullyQualifiedName~WorkspaceLayout|FullyQualifiedName~ThemeCoverage
  -> PASS, 52 passed, 0 failed

dotnet test src/FollowerForge.slnx -c Release --nologo
  -> PASS, 461 passed, 0 failed, 0 skipped
```

Pins added or updated: belongings/armor OfferedKeys merge, empty-draft readiness, failed-build
review Error, palette command titles, race EditorID, multi-select checkbox Commit.

Publish/hash for this exact tree is recorded after `Publish-FollowerForge.ps1` in this session.
NEXUS-UPLOAD working-tree dirt was not included.

```text
.\Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests
  -> PASS, boot check "window stayed up"
  -> zip: FollowerForge 3.6.0\dist\FollowerForge-3.6.0-win-x64.zip
  -> 99,245,718 bytes
  -> SHA-256 3EFA07D9FA98B2E955C8D67311E63D278626E72356797D56F2BF830AA7345CFD
  -> 5 entries (FollowerForge.exe, cli\FollowerForge.Cli.exe, CHANGELOG.txt,
     NEXUS-CHANGELOG-3.6.0.txt, README.md)
  -> FollowerForge.exe FileVersion 3.6.0.0
```

## Runtime status

- Status: `tool-validated`
- Confirmed: compile, 461 tests, XAML load/build, packaged desktop boot, archive inventory/version/hash,
  real-control regression tests for the crashing deck-open path, OfferedKeys apply, readiness, palette titles
- Not confirmed: real-user visual walkthrough, follower generation on the active mod setup, Skyrim gameplay
- Publication: not pushed or released remotely in this run
- SSEEdit/CK: not launched

---

## Prior validation: FollowerForge 3.5.0

## Commands run

```text
dotnet test FollowerForge 3.5.0\src\Tests -c Release --nologo
  → 388 passed, 0 failed
```

PEX compile (russo papyrus.exe, headers from Skyrim Data\Source\Scripts):
  FF_Transform.psc → FF_Transform.pex (2340 bytes)
  Disassembly confirms GetCombatState, originalRace, RestoreRace, OnLoad, OnUpdate, DispelSpell.
  String table does not contain WerewolfChangeFX.

SSEEdit/CK: not launched.

## Targeted checks

| Test | Intent | Result |
|---|---|---|
| Werewolf_UsesTheGamesOwnRace_WithoutWerewolfChangeFx | compiler must not attach the delayed-SetRace spell | PASS |
| BundledTransformScript_DoesNotCastWerewolfChangeFx | shipped PSC has no that EditorID | PASS |
| BundledTransformScript_AbortsIfTheFightAlreadyEnded | GetCombatState after wait | PASS |

## Package

Not published this session. Use:

```text
.\Publish-FollowerForge.ps1 -Version 3.5.0 -SkipTests
```

from `FollowerForge 3.5.0`.
