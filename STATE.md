# FollowerForge state

- Date: 2026-08-19
- Current snapshot: `FollowerForge 3.6.0` (parent `FollowerForge 3.5.0` preserved)
- Active owner application: Grok (continued 3.6.0 release-readiness after Kimi polish / Claude review)
- Milestone: 3.6.0 Expert Deck crash fixed + UI polish passes 2-5 + release-readiness (deck apply, readiness colors, palette, EditorIDs); 461 Release tests pass; public push still waiting for explicit authorization
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## 3.6.0

Implemented architecture: Studio dashboard for category readiness, Focus Cards for normal
editing, and a searchable Expert Deck for large record catalogues. Five token-driven themes,
Guided/Expert routing, a responsive dossier, and a command palette are live. UI preferences
remain separate from follower profiles and existing application settings.

Crash fix (2026-08-18, Kimi): every single-choice Expert Deck catalogue (race, face, voice,
class, combat style, outfit, body records, transform race/spell, location) crashed the app on
open. Root cause from Windows Application event 1026: `WizardWindow.RefreshDeck` mutated the
DataGrid `SelectedItems` collection, which Avalonia only allows in Extended selection mode.
Fixed mode-aware in `DeckGridSelection.SyncSelected`; four regression tests added.

UI polish pass 2 (2026-08-18, Kimi): density/clarity rework of the Studio (roomier cards,
taller rows, 8pt spacing, hover/pressed buttons, larger catalogues, clearer type hierarchy)
following Raycast/Linear-style token research. Theme leaks fixed: badge chips bind to
DynamicResource class styles and repaint live on theme switch; MO2 and first-run setup
windows moved off hardcoded dark/gold hex onto tokens. ThemePalette gains Info/OnStatus;
ThemeCoverageTests pin palette completeness, zero Ui XAML hex leaks, and chip wiring.
Pass 3: Warning/Danger hues made distinct per theme (they were near-identical amber/red, so
theme switches visibly changed nothing); chips became tinted pills. Pass 4 (2026-08-19,
screenshot-verified with the new headless tools/UiScreenshots harness): overlay dim 80→95%
so sidebar labels no longer ghost through; Light theme fixed (RequestedThemeVariant was
hardcoded Dark — buttons were white-on-cream); sidebar statuses are tinted pills;
cards/lists auto-size. Pass 5 (2026-08-19, user's 2560×1440 screenshots): wasted-space root
cause found via a container-painting diagnostic render — category host was a plain Panel
(never stretches children), page ScrollViewers allowed unconstrained horizontal measure, and
the Fluent Expander defaults to left alignment; fixed with a Grid host,
HorizontalScrollBarVisibility=Disabled on all eight pages, and a stretching Expander style,
so pages/sections/lists now fill the full content column. Overlay raised to fully opaque
(pixel-verified, zero pill ghosting). All 26 catalogue list MaxHeights raised ~1.5× so
populated lists use tall windows while empty ones still collapse.

Release readiness (2026-08-19, Grok): finished Claude's half-wired review fixes and remaining
Kimi data-loss / palette / readiness bugs. Expert Deck Apply uses OfferedKeys so armor and
belongings slices cannot wipe siblings. Readiness Error is reserved for setup/build failure.
Command palette Enter/arrows, real EditorIDs (including races), checkbox Apply, overlay-safe
Ctrl shortcuts, and palette commands for Build / Paths / MO2 / Switch manager. 461 Release
tests passed. Public GitHub push still requires explicit authorization. NEXUS-UPLOAD dirt
is unrelated and must not be committed with this work.

## Evidence

- Parent copy: 1,473/1,473 files, 0 failed, 0 mismatched
- Fresh 3.6.0 Release suite after release-readiness: 461 passed, 0 failed
- Prior polish suite: 456 passed (441 + 4 deck regression + 11 theme/polish tests)
- Release build: 0 warnings, 0 errors (`Build-FollowerForge.ps1`, clean+build+tests+CLI boot)
- Migration gate: all 158 named 3.5.0 controls preserved exactly once
- Package boot gate: self-contained executable stayed alive for 12 seconds
- Visual gate: 20 headless-rendered frames (5 themes × studio/appearance/deck/loadout-1440p) reviewed; overlay ring pixels verified exact (fully opaque); full-width page layout confirmed at 2560×1440
- Final ZIP (release-readiness rebuild): 99,245,718 bytes; SHA-256 `3EFA07D9FA98B2E955C8D67311E63D278626E72356797D56F2BF830AA7345CFD`
- Prior polish ZIP (replaced): 111,277,638 bytes; SHA-256 `A9D302602647D3479A04D47B3DCB20FFD277EAED8EAA85DF6653F63B1675F778`
- Runtime status: tool-validated; deck-open crash path now covered by real-control tests;
  no real-user click-through or Skyrim gameplay session in this run
- SSEEdit/CK: not launched

## Unresolved

- Public GitHub push/release has not been performed; publication remains an explicit separate action.
- Real-user visual walkthrough (click every "Browse full catalogue" button) and Skyrim follower
  build/gameplay remain unconfirmed.
- UI polish passes 2-5 shipped and headless-screenshot-verified at 1320×900 and 2560×1440 (tools/UiScreenshots); user visual re-rating and click-through pending.
- Inherited 3.5.0 unresolved items remain: in-game werewolf confirmation, GitHub issue #2
  Crazy Hair, and RaceMenu overlays.

---

## Prior state: 3.5.0

- Date: 2026-08-16
- Current snapshot: `FollowerForge 3.5.0` (parent `FollowerForge 3.4.0` preserved)
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## 3.5.0

Werewolf did not revert after combat. Cause: WerewolfChangeFX / WerewolfTransformVisual
Wait(10) + SetRace(Werewolf) after our Revert(). Fixed in FF_Transform + compiler.

## Evidence

- Tests: 388 passed, 0 failed (Release)
- PEX: compiled with russo papyrus.exe from FF_Transform.psc
- Publish: not run this session
- SSEEDIT/CK: not launched

## Unresolved

- In-game confirmation of werewolf revert still needed
- GitHub issue #2 Crazy Hair
- RaceMenu overlays
