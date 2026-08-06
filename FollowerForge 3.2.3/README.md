# FollowerForge 3.2.3 — Windows tool (not a Skyrim mod)

**This is a Windows application.** It is **not** a Skyrim plugin you install as content.
**Do not** install FollowerForge itself into Skyrim `Data`, Vortex, or MO2 as a game mod.

FollowerForge is an **out-of-game follower builder**. It reads your Vortex or Mod Organizer 2
setup **read-only**, then **writes a new** installable follower mod (ESPFE + folder/zip)
that *you* install like any normal mod afterward.

For photo → RaceMenu preset first, use **FaceForge**, bake the head in RaceMenu, then come here.

---

## What to download / what you get

| File | What it is |
|------|------------|
| `FollowerForge.exe` | The main tool — double-click to run |
| `cli\FollowerForge.Cli.exe` | Optional command-line helper |
| `README.md` | This file |
| `CHANGELOG.txt` | Version history |

Self-contained build (no separate .NET install). **Windows 10/11**.

---

## How to install the TOOL (not the game)

1. Unzip this download **anywhere on your PC** (not inside `Skyrim\Data`).
2. Run **`FollowerForge.exe`**.
3. **Do not** enable FollowerForge.exe as a Skyrim mod in Vortex/MO2.
4. When the wizard finishes a build, install the **output** follower zip/folder as a normal mod.

---

## How to use it (short)

1. Run `FollowerForge.exe`.
2. It auto-detects **Vortex** if available, otherwise **Mod Organizer 2**.
3. If you have **both** and want the other manager: click **Use Mod Organizer 2 instead** / **Use Vortex instead** in the left sidebar. The choice is remembered.
4. Walk the wizard: identity, face/RaceMenu Export Head, voice, combat, gear, spells/perks, spawn place.
5. Press **Build follower**.
6. Install the **generated** mod package in Vortex or MO2, enable the new plugin, deploy/launch Skyrim.

### Faces that look wrong

RaceMenu **Export Head** (NIF/DDS) is required for a custom face. A slider-only / "NO SCULPT"
preset alone often will not match in-game. Empty or corrupt head meshes are rejected at import.

Missing a SavePCFace `.npc` file is **normal** for Export Head — that is optional.

### Advanced (CLI / env)

| Switch | Meaning |
|--------|---------|
| GUI sidebar button | Prefer MO2 or Vortex (saved under `%LOCALAPPDATA%\FollowerForge\prefer-mo2`) |
| `FFORGE_PREFER_MO2=1` | Try MO2 before Vortex |
| `FFORGE_MO2_INSTANCE=C:\path\to\instance` | Force that MO2 instance (folder with `ModOrganizer.ini`) |
| `cli\FollowerForge.Cli.exe env` | Print what was detected |
| `cli\FollowerForge.Cli.exe index` | Rebuild the catalogue |

Do **not** point FollowerForge at a houseCARL shim instance.

---

## Requirements

**Hard**
- Skyrim SE/AE
- Vortex **or** Mod Organizer 2 (active profile)
- RaceMenu (for custom faces)

**Optional**
- xVASynth (custom spoken lines)
- RDO / dialogue overhauls (marriage coverage honesty)
- High Poly Head, FSMP (only if your assets need them)
- [FaceForge](https://github.com/ShugokiFable/FaceForge) — photo → RaceMenu preset before bake

---

## What this is / is not

| It is | It is not |
|-------|-----------|
| A Windows character-builder utility | A pre-made follower character on Nexus |
| A tool that *creates* mods | Something you leave enabled as "FollowerForge.esp" |
| Safe outside the game folder | Creation Kit or an SKSE plugin |

**Sharing followers you build:** check each asset author's permissions. FollowerForge does not
grant redistribution rights for third-party meshes/textures.

---

## Pair tool

**FaceForge** (face preset) → RaceMenu Export Head → **FollowerForge** (NPC plugin).

GitHub: https://github.com/ShugokiFable/FollowerForge  
Release: https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.3