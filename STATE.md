# FollowerForge state

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
