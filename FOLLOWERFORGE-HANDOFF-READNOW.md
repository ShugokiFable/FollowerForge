# FollowerForge UI Redesign Handoff

**Do not continue this work in the Build a Follower task.** Resume it in a dedicated
FollowerForge task from the authoritative root below.

## Authority

- Project root: `Z:\Backup\!Skyrim AE\!!!SkyrimAEaiWorkspace\FollowerForge`
- Active snapshot: `FollowerForge 3.6.0`
- Preserved parent/rollback: `FollowerForge 3.5.0`
- Local design commit: `9998fab` — `docs: define FollowerForge 3.6.0 UI redesign`
- Remote publication: **not pushed**

The 3.6.0 snapshot was a full copy of 3.5.0: 1,473 files copied, zero failed, zero
mismatched. 3.5.0 must remain unchanged.

## Approved UI direction

Use one adaptive workflow, not three separate apps:

1. **Studio dashboard** — category overview, readiness, environment status, follower dossier.
2. **Focus Cards** — default editing surface for ordinary/bounded decisions.
3. **Expert Deck** — dedicated searchable record browser with filters, FormIDs, multi-select,
   inspector, and selection cart for dense catalogues.

User-selected visual themes: Obsidian Gold (default), Arcane Amethyst, Nordic Frost,
Forge Teal, and Light. Themes are cosmetic only.

## Source of truth for implementation

Read this specification before writing code:

`FollowerForge 3.6.0\docs\superpowers\specs\2026-08-18-followerforge-ui-redesign-design.md`

It specifies the category map, shell, persistence model, responsive behavior, accessibility,
error/loading states, compatibility ledger, field-migration gate, tests, and rollback rules.

## Current implementation state

- No 3.6.0 UI source has been changed beyond version/snapshot documentation.
- Exact 3.6.0 build: not run.
- Exact 3.6.0 tests: not run.
- Runtime confirmation: not run.
- Existing 3.5.0 baseline evidence: 388 tests passed.
- User approved the hybrid architecture, but should review the written spec once in the dedicated
  task before implementation planning begins.

## Important preservation rules

- Keep plugin generation, CLI, Vortex/MO2 discovery, MO2 overrides, paths, FaceGen, dialogue,
  transformations, profile JSON, and output safety unchanged.
- Do not stage or overwrite these user-owned root changes without direct instruction:
  `NEXUS-UPLOAD-CHECKLIST.md`, `NEXUS-UPLOAD\README-AUTHOR-UPLOAD.txt`, and
  `NEXUS-UPLOAD\NEXUS-CHANGELOG-3.5.0.txt`.
- Do not push the design commit until implementation is complete and the user explicitly asks
  for publication.

## Resume order

1. Review and approve the written specification.
2. Create a test-first implementation plan.
3. Implement only in `FollowerForge 3.6.0`.
4. Run the exact Release test/build/package gates.
5. Prepare release notes and obtain explicit publish authorization.
