# Follower Forge 2.1.3 validation

## Inputs and symptom

- [x] Supplied screenshot present and inspected.
- [x] Screenshot shows all 18 custom skill controls with visible spinner buttons but clipped values.
- [x] Authoritative 2.1.2 source confirms `ColumnDefinitions("*,82")`.
- [x] The primary-stat controls use wider columns and display their values, ruling out a global
      foreground or numeric-format problem.

## Version and scope gates

- [x] `Follower Forge 2.1.2` remains unchanged as the parent snapshot.
- [x] `Follower Forge 2.1.3` is a full-copy successor.
- [x] Source diff contains exactly eight intended paths:
  - `CHANGELOG.txt`
  - `Publish-FollowerForge.ps1`
  - `README.md`
  - `src/Tests/FollowerForge.Tests.csproj`
  - `src/Tests/SkillEditorLayoutTests.cs`
  - `src/Ui/FollowerForge.Ui.csproj`
  - `src/Ui/WizardWindow.axaml.cs`
  - `VERSION.txt`
- [x] No installed game, Vortex deployment, staging, profile, save, or reference tree was modified.

## UI repair

- [x] Skill numeric column widened from 82 to 150 pixels.
- [x] Each `NumericUpDown` has `MinWidth = 150`.
- [x] Focused regression test rejects widths below 140 pixels.
- [x] The existing three-column Combat, Magic, and Stealth & utility grouping is preserved.
- [x] Skill ranges, preset values, and stat serialization are unchanged.

## Build, tests, and package

- [x] Exact authoritative clean Release build: 0 warnings, 0 errors.
- [x] Complete xUnit suite: 106 passed, 0 failed, 0 skipped.
- [x] Self-contained app and CLI publish succeeded.
- [x] Published app remained running for the hidden 12-second boot check.
- [x] Ship-gate release-tree validator self-test: PASS.
- [x] Ship-gate final staged-tree validation: PASS.
- [x] Final archive contains exactly four intended files:
  - `Follower Forge.exe`
  - `cli/FollowerForge.Cli.exe`
  - `README.md`
  - `CHANGELOG.txt`
- [x] Archive contains zero PDB, Vortex/MO2 bookkeeping, stale-version, or private-path entries.
- [x] App and CLI FileVersion: `2.1.3.0`.
- [x] Final ZIP SHA-256:
      `7787E4546C85923CEE0DF6B92A39B28F4BEB516294ED23F8B5EB63742033E957`.

## Exact command evidence

- `Build-FollowerForge.ps1`
  - Exit code `0`; exact clean Release build succeeded; 0 warnings, 0 errors;
    106 tests passed; CLI boot verification passed.
- `Publish-FollowerForge.ps1 -Version 2.1.3`
  - Exit code `0`; 106 tests passed; app and CLI published; hidden boot check passed.
- `validate_release_tree.py --self-test`
  - Exit code `0`; `SELF-TEST: PASS`.
- `validate_release_tree.py "...\dist\Follower Forge 2.1.3"`
  - Exit code `0`; `RESULT: PASS`.
- Direct SHA-256, Windows version-resource, ZIP inventory, and negative-entry inspection
  - Exit code `0`; all checks passed.

## Runtime checks still required

- [ ] Open 2.1.3 on the user's normal desktop and confirm every skill number is visible.
- [ ] Build one custom follower with deliberately distinct combat and magic skills.
- [ ] Confirm the generated follower's Health, Magicka, Stamina, and specialties in game.
- [ ] Recruit, fight, trade, save, reload, dismiss, and recruit again.

## Evidence boundary

The 150-pixel layout invariant, complete test suite, executables, boot check, and final archive are
tool-validated. A hidden boot check cannot visually confirm the user's DPI/theme rendering, and
static/plugin tests cannot prove in-game custom-stat behavior. Those remain user-side runtime checks.

## Check-work verdict

```text
SCOPE: PASS
DIFF REVIEW: PASS - exactly eight intended source/release paths changed
BUILD/COMPILE: PASS - exact clean Release build, 0 warnings, 0 errors
TESTS/VALIDATORS: PASS - 106/106 tests; release-tree self-test and final-tree validation passed
PACKAGE INSPECTION: PASS - four intended files, correct versions, no PDB/private/stale entries
UNRESOLVED: visual confirmation under the user's DPI/theme and in-game custom-stat validation
FINAL: PASS - tool-validated release
```
