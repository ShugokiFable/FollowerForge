# FollowerForge state

- Date: 2026-08-18
- Current snapshot: `FollowerForge 3.6.0` (parent `FollowerForge 3.5.0` preserved)
- Active owner application: Codex
- Milestone: approved UI architecture; written design specification in progress
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## 3.6.0

Approved architecture: Studio dashboard for category readiness, Focus Cards for normal
editing, and a searchable Expert Deck for large record catalogues. Themes are cosmetic;
the underlying profile/build contracts remain unchanged.

## Evidence

- Parent copy: 1,473/1,473 files, 0 failed, 0 mismatched
- Parent baseline: 388 tests passed in 3.5.0 (inherited evidence; not rerun yet)
- Implementation: not started
- SSEEdit/CK: not launched

## Unresolved

- Written design requires final user review before implementation planning.
- Redesigned UI has not been built, test-run, or runtime-confirmed.
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
