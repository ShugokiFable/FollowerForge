# FollowerForge 3.2.7 plan

1. [x] Preserve 3.2.6 and establish a complete 3.2.7 sibling snapshot.
2. [x] Add red/green tests for manual MO2 instance/profile selection and `%BASE_DIR%` paths.
3. [x] Resolve base/mods/profiles/overwrite paths using MO2 path semantics.
4. [x] Add a persisted GUI MO2 setup dialog with INI browse and profile selector.
5. [x] Reject invalid manual choices without substituting another profile or manager.
6. [x] Preserve Vortex and existing CLI/environment behavior; add `--mo2-profile`.
7. [x] Cancel stale catalogue generations and re-index the saved selection safely.
8. [x] Run complete tests, publish, boot, archive, metadata, and isolated fixture-index validation.
9. [x] Produce the Nexus ZIP, checksum, and concise user changelog.
10. [x] Publish 3.2.7 directly to the existing repository's `main` branch and release `v3.2.7` with verified assets.
