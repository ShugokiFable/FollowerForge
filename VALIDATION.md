# VALIDATION — FollowerForge 3.6.0

## Snapshot gate

```text
Parent: FollowerForge 3.5.0
Active: FollowerForge 3.6.0
Robocopy: 1,473 copied, 0 failed, 0 mismatched
```

## Design gate

- User approved the Studio → Focus Cards → Expert Deck hybrid on 2026-08-18.
- Five selectable palettes are planned: Obsidian Gold, Arcane Amethyst, Nordic Frost,
  Forge Teal, and Light.
- No application source has been changed yet.

## Current build/test status

- Exact 3.6.0 Release build: NOT RUN
- Exact 3.6.0 tests: NOT RUN
- UI smoke test: NOT RUN
- Package: NOT BUILT
- Runtime status: inherited 3.5.0 behavior only; 3.6.0 redesign UNTESTED
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
