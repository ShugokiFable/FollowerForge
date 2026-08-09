# FollowerForge state

- Date: 2026-08-09
- Active design snapshot: `FollowerForge 3.2.7` (parent 3.2.6)
- Branch: `agent/mo2-manual-setup`
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or MO2 2.5.x

## Current phase

- Root cause confirmed in 3.2.6 source: the backend has CLI/environment instance overrides, but the GUI has no instance path or profile override.
- MO2's `%BASE_DIR%` path token is not expanded by the current parser, so valid custom mods/profiles/overwrite paths can resolve against FollowerForge's working directory instead of the MO2 base directory.
- 3.2.7 design is documented; implementation is pending design review.
- Vortex behavior remains out of scope and must remain unchanged.
- No game Data, MO2 instance/profile/mod, Vortex staging/profile, or save write is authorized.
