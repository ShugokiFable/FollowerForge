# FollowerForge 3.2.1

Hotfix for the 3.2.0 hang on "re-reading your mods".

## Cause

`SKYRIM_MO2_INSTANCE` (set by houseCARL) points at a **Vortex shim** with ~3000 junctioned mods. 3.2.0 treated that as "prefer MO2" and tried to re-index the entire tree.

## Fix

- Prefer **Vortex** when it is available.
- Ignore houseCARL shims unless `FFORGE_ALLOW_HOUSECARL_SHIM=1`.
- Real MO2 still works via `FFORGE_MO2_INSTANCE` or when Vortex is absent.

## Download

**`FollowerForge-3.2.1-win-x64.zip`**

Close any stuck 3.2.0 window, unzip 3.2.1, run **FollowerForge.exe**.
First launch may re-index once under Vortex (~1–2 min) — that is expected after the manager switch.
