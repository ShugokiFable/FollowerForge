# Follower Forge state

- Date: 2026-08-05
- Active: `Follower Forge 3.1.1` (parent 3.1.0)
- Runtime target: Skyrim SSE 1.6.x, Vortex-managed
- Product: self-contained Windows x64 app + CLI

## Validation (this session)

- xUnit: **278 passed**, 0 failed, 0 skipped
- Publish: `Publish-FollowerForge.ps1 -Version 3.1.1` — tests, single-file app+CLI, 12s boot check PASS
- App FileVersion / Product: **3.1.1.0** / **3.1.1** (was incorrectly still 2.1.3 in UI csproj; fixed)
- CLI FileVersion / Product: **3.1.1.0** / **3.1.1**
- Final ZIP: `FollowerForge-3.1.1-win-x64.zip`
  - SHA-256: `F01D836FE2DEFE3D8EEF498514F662B60BE65155B09573CA7D10BE0C61419EF0`
  - Contents only: `Follower Forge.exe`, `cli/FollowerForge.Cli.exe`, `README.md`, `CHANGELOG.txt`
- No game Data / Vortex staging / saves written
- SSEEdit / Creation Kit: not launched

## Runtime evidence boundary

- 2.1.1 remains last user-confirmed known-good baseline for core follower recruitment loop
- 3.x features tool-validated; four scripted features (evolution, transformation, random spawn,
  enemy-to-ally) still **runtime-unconfirmed** in-game
- Flat-face-from-slider-only presets: static warning added; fix is RaceMenu sculpt + re-export
  (upstream FaceForge 0.23.0 also omits inert sliders)

## GitHub

- Repo: https://github.com/ShugokiFable/FollowerForge
- Push 3.1.1 + create release `v3.1.1` after commit
