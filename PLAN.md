# FollowerForge 3.2.7 plan

1. Preserve 3.2.6 unchanged and establish 3.2.7 as a full-copy patch successor.
2. Add failing fixtures for manual MO2 instance/profile selection and `%BASE_DIR%` paths.
3. Resolve MO2 base/mods/profiles/overwrite paths using MO2's documented path semantics.
4. Add a persisted, read-only GUI MO2 Setup dialog with `ModOrganizer.ini` browse and profile selector.
5. Validate the chosen profile before indexing; never silently substitute another profile in manual mode.
6. Keep Vortex discovery and the existing CLI/environment overrides compatible.
7. Cancel the current catalogue job and rebuild against the exact saved MO2 instance/profile.
8. Run the complete clean test/build/publish/archive-validation pipeline.
9. Produce a Nexus-ready ZIP, concise user changelog, and push the exact scoped update to the existing GitHub repository.
