The old seven-step tab wizard is gone. 3.6.0 is one Studio workspace: a dashboard, seven Focus categories, and an Expert Deck for installed-record catalogues.

3.4.0 / 3.5.0 behavior is still in (Paths, pronouns, gear FormIDs, werewolf revert after combat). Rebuild werewolf followers on the 3.5.0 script if you have not already.

## Workspace

- **Studio** shows category readiness and the next useful action. An unfinished draft is not painted as broken: a missing name needs attention, empty loadout stays optional, Review only turns red after a failed build or a real setup problem.
- Seven categories, no nested tabs.
- **Expert Deck** searches name, EditorID, FormID, and plugin. Apply only replaces the family that deck showed, so armor slots and belongings slices no longer wipe each other.
- Guided / Expert (`Ctrl+E`). Expert opens the primary catalogue. It does not hide fields.
- Command palette (`Ctrl+K`): jump categories, Build, Paths, MO2 setup, switch manager. Enter runs the highlighted command.
- Five themes. Theme, experience, and window size live under `%LOCALAPPDATA%\FollowerForge`, separate from follower profiles.
- `Ctrl+0`–`Ctrl+7` jumps categories. Escape only closes an open overlay.

## Install

Extract `FollowerForge-3.6.0-win-x64.zip` anywhere **outside** Skyrim `Data`. Run `FollowerForge.exe`. Self-contained; no separate .NET install.

## Verify

- CI: 461 tests passed on `FollowerForge 3.6.0` (`net10.0`).
- Local publish boot check: window stayed up.
- SHA-256 `3EFA07D9FA98B2E955C8D67311E63D278626E72356797D56F2BF830AA7345CFD`

Not claimed: a full real-user click-through of every catalogue, or a new in-game werewolf confirmation on this build.
