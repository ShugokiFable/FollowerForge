<div align="center">

# ⚒️ Follower Forge

**Make a custom Skyrim SE/AE follower without ever opening the Creation Kit.**

Pick her name, face, voice, class, combat style, outfit, and **where in Skyrim she waits** —
all from your own installed mods, by name. Follower Forge writes a real, conflict-safe ESPFE
plugin and an installable mod folder.

[![Release](https://img.shields.io/github/v/release/ShugokiFable/FollowerForge)](https://github.com/ShugokiFable/FollowerForge/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2064--bit-lightgrey)]()
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

</div>

---

## What's new in 2.0

Version 1 was an engineer's tool: hand-written JSON profiles, raw FormKeys, and a single
hardcoded Whiterun spawn. **2.0 is a follower-maker.**

| | 1.0 | **2.0** |
|---|---|---|
| UI | Discover / Index / Search / Build panels | **7-step wizard** — real names only |
| Placement | One hardcoded Whiterun cell | **3,000+ spawn spots** harvested from your mods |
| Face pipeline | RaceMenu export + FaceGen | Same, plus **slot-aware** missing-texture warnings |
| Loadout / body | Not in the wizard | **Weapons + spells** multi-select; optional **body** skin (or race default) |
| Marriage | Profile field ignored | **Marriageable** adds PotentialMarriageFaction for real |
| Asset hubs | Manual / advanced | **Free-hub default** (Naz skin maps when installed) + optional own-hub with permission gate |
| Races | Every RACE record | **Vanilla first**; custom races opt-in (they become requirements) |
| Sharing report | Masters list | **"Needs nothing but base game"** or exact mod requirements |
| Ship form | Self-contained zip | **`Follower Forge.exe`** + CLI, no .NET install needed |

---

## Download

➡️ **Grab `FollowerForge-2.0.0-win-x64.zip` from the
[Releases page](https://github.com/ShugokiFable/FollowerForge/releases).**

Self-contained Windows 64-bit package — **no .NET install required**.

```
Follower Forge.exe          GUI wizard
cli\FollowerForge.Cli.exe   same engine, scriptable
README.md                   end-user guide (ships inside the zip)
CHANGELOG.txt               full history
```

### Requirements

- **Windows 10/11 (64-bit)**
- A **Vortex-managed** Skyrim Special Edition / Anniversary Edition install
- Your game Data, Vortex staging, and installed mods are **never written to**

---

## Getting started

1. Unzip anywhere and run **Follower Forge.exe**.
2. First launch reads your mods (about a minute; only repeats after a Vortex deploy).
3. Walk the seven steps and press **Build follower**.
4. Install the produced folder (or `.zip`) in Vortex, enable the plugin, and go find her.

### Wizard steps

| Step | What you choose |
|---|---|
| **Who she is** | Name, sex, protected-by-default, optional **marriageable** |
| **Her look** | RaceMenu head export + race (vanilla first) + optional **body** (skin armor; unset = race default / your body replacer) |
| **Her voice** | FULL FOLLOWER / SOS PACK / unverified — labelled honestly |
| **How she fights** | Class + combat style (clone into her plugin; originals never edited) |
| **What she wears** | Outfit plus multi-select **weapons and spells** loadout |
| **Where she waits** | Searchable location library from real NPC placements |
| **Build her** | Summary, build, and a plain list of anything worth knowing |

### Her face

Load the preset in game → RaceMenu **Sculpt** tab → **F5** to export the head.
A `.jslot` alone is not enough; the exported head mesh carries the face.

### Where she waits

Follower Forge scans every mod that places its own NPC and builds a library of reachable
spots — classic inns (Bannered Mare, Sleeping Giant, Bee and Barb, …) plus hundreds of
mod-author placements. Each entry shows how many mods use it and whether it needs masters
beyond the base game.

---

## Sharing her

The build report tells downloaders exactly what they need:

> She needs nothing but the base game — safe to share with anyone.

or a list like *"Anyone who installs her also needs: KS Hairdos"*.

**Listing a mod as a requirement is not permission to redistribute its assets.** Follower Forge
never copies another author's files unless you explicitly declare redistribution rights, and
it will not make that claim on your behalf. Every build writes `credits.md`,
`dependency-report.json`, and `source-assets.json`.

### Asset hubs

| Mode | Behaviour |
|---|---|
| **Free hubs** *(default)* | When Naz's Asset Collection is installed, skin *support maps* (`_msn` / `_s` / `_sk`) for covered races are retargeted so the follower leans less on private skin packs. Diffuse, eyes, hair, and mouth stay on their real sources. |
| **Own hub** | Copies the face assets she uses into `textures\<Prefix>\…` and repoints the mesh — **only** after you tick the redistribution permission box. BSA-packed assets are never unpacked. |

Free hubs do **not** make a follower fully shareable on their own; the report stays honest.

---

## What each follower package contains

```
FF_YourFollower.esp                         ESPFE / ESL light plugin
meshes/.../facegeom/FF_YourFollower.esp/    FaceGen mesh
textures/.../facetint/FF_YourFollower.esp/  Face tint
manifest.json  source-assets.json  dependency-report.json  rebuild-profile.json
build-report.html  credits.md  PERMISSIONS.md (when own-hub copies)
```

Rebuilding the same follower produces a **byte-identical** plugin.

---

## Command line (optional)

`cli\FollowerForge.Cli.exe` drives the same engine:

```powershell
fforge env                                   # what Follower Forge sees on this PC
fforge index                                 # re-read your mods
fforge locations --scan                      # rebuild the spawn library
fforge locations --text "bannered mare"      # search spawn points
fforge races                                 # usable races + why
fforge hubs                                  # free-hub coverage
fforge assets --path "textures\...\x.dds"    # is this texture installed?
fforge build --profile her.json --zip        # build from a saved profile
fforge batch --profiles .\profiles\          # build a folder of profiles
```

---

## Safety

Your game folder, Vortex staging folder, and installed mods are **only ever read**.
Everything Follower Forge creates goes in its own workspace. A write-guard refuses any
path that would land inside your game or mod manager.

---

## Verification (tool-validated)

Generated plugins are checked for:

- HEDR `1.71`, formVersion `44`, ESL light flag, new FormIDs in the light range
- Clean reopen in Mutagen
- Deterministic rebuild (SHA256 match on the ESP)

**77** xUnit tests. Release build: 0 warnings / 0 errors.

> **Not yet user-confirmed in-game for every path:** recruit / trade / wait / dismiss and
> “she is standing in the inn” still need a Skyrim session. Structural validation is not
> a substitute for that.

---

## Build from source

**Requires:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```powershell
cd "Follower Forge 2.0.0"
.\Build-FollowerForge.ps1        # build + tests
.\Publish-FollowerForge.ps1      # self-contained exe + zip in dist\
```

### Solution layout

| Project | Responsibility |
|---|---|
| `Domain` | Profiles, records, assets, hubs, manifests, validation types |
| `ModManagers` | Vortex discovery, deployment manifest, write-guard |
| `SkyrimRecords` | Mutagen indexing, follower compiler, cell placement, race suitability, analyzers |
| `AssetIndex` | SQLite catalogue, loose + BSA, CharGen, hub catalog |
| `FaceGen` | NiflySharp head dirty-swap |
| `BuildPipeline` | Atomic builder, location library, hubs, batch, packaging |
| `Validation` | In-process ESP header ship-gate |
| `Cli` | `fforge` |
| `Ui` | Avalonia 7-step wizard |
| `Tests` | xUnit |

---

## License

MIT for Follower Forge source and binaries you build from it.

Follower Forge never redistributes Bethesda masters or third-party mod assets. Portable /
own-hub copies require an explicit redistribution declaration from **you**, and the tool
records that you made the claim — it cannot verify permission with the original authors.

---

<div align="center">

**[⬇ Download Follower Forge 2.0.0](https://github.com/ShugokiFable/FollowerForge/releases/latest)**

</div>
