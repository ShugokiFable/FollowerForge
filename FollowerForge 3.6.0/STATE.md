# FollowerForge 3.6.0 snapshot state

- Date: 2026-08-19
- Parent: FollowerForge 3.5.0 (untouched)
- Agent: Grok (release-readiness after Kimi polish / Claude review)

## Status

Studio / Focus Cards / Expert Deck UI is implemented. Crash on single-choice deck open is
fixed. UI polish passes 2-5 shipped. Release-readiness fixes (deck apply, readiness colors,
palette, EditorIDs, overlay-safe shortcuts) are in this snapshot.

## Evidence this session

- `dotnet test src/FollowerForge.slnx -c Release --nologo` -> 461 passed, 0 failed
- `Publish-FollowerForge.ps1 -Version 3.6.0 -SkipTests` -> boot check "window stayed up"
- ZIP: `dist/FollowerForge-3.6.0-win-x64.zip`
- Bytes: 99,245,718
- SHA-256: `3EFA07D9FA98B2E955C8D67311E63D278626E72356797D56F2BF830AA7345CFD`
- FileVersion: 3.6.0.0

## Unresolved

- Public GitHub push/release not authorized
- Real-user click-through and in-game werewolf revert unconfirmed
- Do not commit NEXUS-UPLOAD working-tree dirt with this work
