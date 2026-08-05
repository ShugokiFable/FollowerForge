# Follower Forge 3.1.1

**Windows tool:** build a custom Skyrim SE/AE follower (ESPFE plugin + installable mod folder) without the Creation Kit.

## Download

**`FollowerForge-3.1.1-win-x64.zip`**

Unzip anywhere and run **Follower Forge.exe**. Self-contained — no .NET install required.

Also includes `cli\FollowerForge.Cli.exe` for the same engine from the command line.

## This release

- **Slider-only preset warning:** if a RaceMenu preset has many non-zero sliders but **no sculpt**, the face picker marks **NO SCULPT** and the build reports `FACE_SLIDERS_WITHOUT_SCULPT`. Slider-only shaping does not survive on an NPC — sculpt in RaceMenu, then Export Head again.
- **Version resources:** UI and CLI both ship as FileVersion **3.1.1.0** (UI was previously stuck at 2.1.3 metadata).
- **Tests:** 278 xUnit tests pass; self-contained publish + boot check pass.

## Since 3.1.0

- Lore items (books, keepsakes, potions, ingredients) build correctly
- RaceMenu complexion / face texture set (FTST) ships with the follower (neck seam fix)
- Voice list ordered by usefulness; creature voices hidden by default
- Voice pack files verified against the real asset index

## Requirements

- Windows 64-bit
- Skyrim Special Edition / Anniversary Edition managed by **Vortex**
- RaceMenu + Export Head for custom faces
- Optional: xVASynth for custom voiced lines

## Honest limits

- Four scripted features (evolution, transformation, random spawn, enemy-to-ally) compile cleanly but are not yet user-confirmed in long play sessions — test on a disposable save.
- Does not edit your installed plugins, saves, or game folder (read-only evidence).
- Never builds child followers or uses child voices.

## Related

- [FaceForge](https://github.com/ShugokiFable/FaceForge) — photograph → RaceMenu starting preset
