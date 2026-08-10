# FollowerForge state

- Date: 2026-08-09
- Current snapshot: `FollowerForge 3.2.7` (parent 3.2.6 preserved)
- Published branch: `main`
- Release tag: `v3.2.7` (`d39ef3a54f0ca8641810a5862a8de716a36ae558`)
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## Release evidence

- Complete Release test suite: 349 passed, 0 failed, 0 skipped.
- Self-contained `win-x64` app and CLI publish: PASS.
- Published GUI hidden boot check: window remained alive for 12 seconds.
- Extracted CLI usage boot: PASS, expected exit code 2.
- Archive inventory: 5 intended files; no source, PDB, temporary, credential, or secret-shaped files.
- App file version: 3.2.7.0; product version: `3.2.7+d217ffd5f461647cdb9b0699a07a73a90d964d37`.
- Isolated MO2 fixture: exact `Custom Profile` selected over an invalid INI-selected profile.
- Fixture path semantics: relative base directory and case-insensitive `%BASE_DIR%` paths resolved correctly.
- Fixture catalogue index: 43,769 records from hardlinked `Skyrim.esm`; 0 failures.
- Nexus ZIP: `FollowerForge-3.2.7-win-x64.zip`, 99,181,134 bytes.
- ZIP SHA-256: `79766F2C352E2A9C85A53BCBA43EB5F4BDC0C0E3C195520A77116AA84699F86C`.
- GitHub release: `https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.7` (published, Latest).
- GitHub-hosted ZIP downloaded and re-hashed: PASS; SHA-256 matched the declared checksum.

## Ship gate

- VERSION SNAPSHOT: PASS
- SOURCE INVENTORY: PASS
- BUILD: PASS
- FRAMEWORK VALIDATION: N/A (standalone Windows application)
- ASSET VALIDATION: PASS (package inventory and executable metadata)
- DEPENDENCY GRAPH: PASS (clean restore/publish)
- PACKAGE ROOTS: PASS
- UPGRADE/UNINSTALL: PASS (portable replacement; saved override removable from the GUI or one FollowerForge-owned JSON file)
- SAVE/MULTIPLAYER: N/A (application does not edit Skyrim saves or run in-game)
- RUNTIME STATUS: tool-validated
- UNRESOLVED: final confirmation on a real MO2 user's multi-instance/profile setup; GUI picker interaction was not human-driven in this environment.

No game Data, MO2 instance/profile/mod, Vortex staging/profile, or save file was edited.
