# FollowerForge state

- Date: 2026-08-10
- Current snapshot: `FollowerForge 3.2.9` (parents 3.2.8 / 3.2.7 preserved)
- Runtime target: Windows 10/11, Skyrim SE/AE, Vortex or Mod Organizer 2

## Report triage (3.2.7 CLI + MO2 session)

| Report | Status |
|---|---|
| MO2 master validator uses GameDataPath not PluginDataPath | **Fixed in 3.2.8** |
| CharGen face-export ignores MO2 mods/overwrite | **Fixed in 3.2.8** (Discover(env)) |
| Race classifier "horse" false-positive on BDHorseRace | **Fixed in 3.2.9** |
| Manual Cell+XYZ hardcodes WhiterunWorld | **Fixed in 3.2.9** (+ optional Worldspace) |
| Cell override FormVersion 40 vs ship-gate 44 | **Fixed in 3.2.9** |
| RaceMenu overlays need NiOverride.AddOverlays | **Known gap** — not fixed; bake path TBD |

## 3.2.9 evidence

- Tests: 357 passed, 0 failed
- Publish: PASS, boot window stayed up
- ZIP: `FollowerForge 3.2.9\dist\FollowerForge-3.2.9-win-x64.zip` (99,184,193 bytes)
- ZIP SHA-256: `C7EA9E121910700E4603FD832200CDA3A9C03E6DF8008698DD5CD85E50C0D49F`
- Staged: `NEXUS-UPLOAD\FollowerForge-3.2.9-win-x64.zip`

## Ship gate

- VERSION SNAPSHOT: PASS
- BUILD: PASS
- RUNTIME STATUS: tool-validated; user MO2 runtime confirmation pending
- SSEEDIT/CK: not launched
- UNRESOLVED: overlays; user re-test on BD Ungulate / BD Cat / SOSVoices MO2 builds

No game Data, MO2, Vortex, or save paths were edited.
