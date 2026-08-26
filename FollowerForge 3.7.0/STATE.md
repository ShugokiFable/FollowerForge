# FollowerForge state

- Date: 2026-08-26
- Current snapshot: `FollowerForge 3.7.0` (parent `FollowerForge 3.6.1` preserved)
- Active owner application: Claude Code (Opus 5)
- Milestone: 3.7.0 - creature transformation, the real "Must be fixed", preset body warning
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## 3.7.0

3.6.1 SHIPPED on 2026-08-22 - GitHub `v3.6.1` (Latest). Nexus upload remains the user's step.

User request: creature races in the combat-transformation options, and "not sure why it wasn't
there already". It was a filter in the wrong place rather than an absent feature. Creature races
are hidden from the identity picker because they carry no head data, so no face can ever be
built for one - a constraint on the race she IS. The transform picker inherited that exclusion
even though the face belongs to her base race and FF_Transform only calls SetRace() once combat
starts. Vanilla werewolf, which the feature already offered, is itself classified Creature. The
comment above the offending line already said "all races here, not the follower-suitable subset";
the code did the opposite.

Four call sites fed the transform race list and had drifted apart: the focus card filled from
vanilla+custom, its search box refiltered from the same, the Expert Deck used the identity
source (so it honoured the identity page's creature checkbox), and selection-restore used the
identity source too. All four now use one `_transformRaces` - creatures first, then vanilla,
then custom.

Second finding, and the more valuable one: 3.6.1 shipped without knowing what the reporter's
"Must be fixed" actually was, and flagged the guess as speculation. It was not
INVENTORY_DUPLICATE. Choosing ANY armour together with a legacy outfit produced one
STARTING_ARMOR_NOT_WRITTEN error per piece, every time. FollowerCompiler branches - a chosen
OTFT wins outright, and only otherwise is a private StartingEquipment outfit generated - but
FollowerValidator ran the generated-outfit check unconditionally, searched the freshly written
plugin for an outfit that lives in Skyrim.esm, found nothing, and called every piece missing.
The validator now follows the same branch. The combination is one warning naming the outfit.

Third: a RaceMenu preset's body shape has never transferred and nothing said so. The build now
counts shaped sliders in the preset's bodyMorphs block. Untouched sliders are excluded because
RaceMenu writes the whole set regardless - a reference preset on this machine holds 119 entries
of which 86 are shaped, so counting all of them would warn on every build.

### Evidence

- Snapshot copy: 238 files, 0 failed, 0 mismatched (bin/obj/dist excluded as disposable)
- `dotnet test src/FollowerForge.slnx -c Release` -> 489 passed, 0 failed
- Live catalogue (2,920 enabled plugins, 978 RACE records): identity picker offers 191,
  transformation picker offers 944
- End-to-end CLI build of the reported combination - legacy outfit + 2 armour pieces +
  WerewolfBeastRace as the transform race - BUILD OK, one warning, zero errors
- Published plugin VMAD carries BeastRace = 0x000CDD84 at master index 0; ship gate reads
  HEDR=1.71 numRecords=11 formVersion=44 ESL=True
- jslot block names read from two real presets in the game's CharGen folder, not assumed

### Unresolved

- Whether a creature transform survives a real fight in game. SetRace() on a follower is engine
  behaviour this build cannot exercise: the plugin side is verified, the gameplay side is not.
- RaceMenu OVERLAYS specifically are still undetectable here. Neither reference preset carries
  an overlay block, so the key name is unconfirmed and was not guessed. The body-shape warning
  names overlays as a known limitation without claiming to detect them.
- MO2 `modlist.txt` priority direction still UNVERIFIED (PluginLists.cs:69, open since 3.2.5)
- The publish ZIP is not byte-reproducible; publishing from CI on tag would settle it
- 3.7.0 has not been published, pushed or tagged. Nexus still serves 3.6.0.

## 3.6.1

3.6.0 SHIPPED on 2026-08-19 — GitHub `v3.6.0` (Latest) and Nexus 187479. The earlier note here
that the push was "waiting for authorization" was three days stale; corrected.

Added `DiagnosticsReport` + a "Copy diagnostics" button on the Review page and in the Ctrl+K
palette. Rationale: every 3.x fix this year started from a report too vague to act on — "body
and face colour mismatch", "can't add arrows", "MO2 can't find my mods" — and each cost a
round-trip before any work could start. Rendering is pure and unit-tested; the window only
gathers. Home directories are tokenised so a report pasted publicly does not carry the
reporter's Windows account name.

User-reported blocker fixed in the same snapshot: there was no way to unselect anything. Only
Kin and custom lines had "Remove selected", which taught people a button exists; 18 other
pickers had none, the deck's checkbox column was a read-only status light headed "Selected",
and click-again-to-deselect (which did work on the multi lists) was never stated. Added Clear
to every optional picker, Clear selection to the deck, honest column labelling, and the
instruction. A structural test now fails if any browsable optional picker loses its Clear.

Two UI bugs found by the new screenshot gate, not by reading code: the workspace grid never
reclaimed the hidden dossier's 312px column, so every page was measured 312px narrower than the
window and clipped on the right below 1180px; and the build action row could push a button off
the edge with no horizontal scroll to reach it. Both fixed and re-rendered.

Release hygiene in the same pass:
- CI requested the .NET 9 SDK for net10.0 projects (green only because the windows-latest image
  preinstalls .NET 10, and a 298 MB download wasted every run). Now 10.0.x.
- Deleted `FollowerForge 3.6.0/dist`. It was rebuilt three minutes AFTER the release upload from
  the same commit and did not match it, so a later hash check would have verified a file nobody
  downloaded. Both hashes are in VALIDATION.md.

### Evidence

- Snapshot copy: 235 files, 0 failed, 0 mismatched (bin/obj/dist excluded as disposable)
- `dotnet test src/FollowerForge.slnx -c Release` → 472 passed, 0 failed
- Live-machine render through `EnvironmentDiscovery`: redaction held (`%APPDATA%\Vortex\…`,
  no account name), and the report surfaced a real Vortex "undeployed changes" warning

### Unresolved

- RaceMenu body/face OVERLAYS still do not transfer; 3.6.1 does not yet warn about it
- MO2 `modlist.txt` priority direction still UNVERIFIED (PluginLists.cs:69, open since 3.2.5)
- The publish ZIP is not byte-reproducible; publishing from CI on tag would settle it
- Real-user click-through and in-game behaviour not re-confirmed for this patch

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
