<div align="center">

# ⚒️ Follower Forge

**Compile real custom followers from your modded Skyrim SE/AE installation.**

A Windows desktop app (C# / .NET 10 · Avalonia · Mutagen · NiflySharp · SQLite) that turns
your existing modded game into genuine, recruitable, ESPFE follower mods — complete with
FaceGen, factions, voice, placement, and a Vortex-ready install folder.

[![Release](https://img.shields.io/badge/release-1.0.0-blue)]()
[![Platform](https://img.shields.io/badge/platform-Windows%2064--bit-lightgrey)]()
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

</div>

---

## What it does

Follower Forge reads your **Vortex-managed** Skyrim SE/AE installation (game directory,
staging folder, deployed `Data\` folder, and installed source mods — all **strictly
read-only**) and compiles brand-new follower plugins from the assets and records you
already have.

Every generated follower is a genuine **ESPFE** plugin with:

- Correct NPC records (clone-in-place — the original is never edited)
- Follower factions + a player relationship
- World placement (additive to vanilla persistent cells)
- Voice type, class, and combat style
- FaceGen mesh + tint (RaceMenu CharGen dirty-swap via NiflySharp)
- A dependency report, rebuild profile, and credits manifest
- An installable Vortex mod folder

Builds are **atomic** (staging → validate → publish) and **deterministic** — rebuilding the
same profile produces a byte-identical plugin.

## Download

➡️ **Grab `FollowerForge-1.0.0-win-x64.zip` from the
[Releases page](https://github.com/ShugokiFable/FollowerForge/releases).**

The package is **self-contained** — no .NET SDK install required. Just unzip and run
`FollowerForge.exe` (GUI) or `fforge.exe` (CLI).

### Requirements

- **Windows 10/11 (64-bit)**
- A **Vortex-managed** Skyrim SE/AE install (hardlink deployment)
- Your game Data, Vortex staging, and installed mods are never written to

## GUI

Double-click `FollowerForge.exe`. The Avalonia window exposes the full engine:

**Discover → Index → Search → Build → Detect**

All five operations use the same deterministic build engine as the CLI.

## CLI (`fforge`)

```powershell
fforge.exe env                       # environment + diagnostic report
fforge.exe index                     # build the modpack catalogue (SQLite)
fforge.exe search --type csty --text bandit
fforge.exe inspect --formkey 000800:NPCWeaponVariance.esp
fforge.exe detect                    # follower frameworks + body systems
fforge.exe sample-profile --out aria.json
fforge.exe build --profile aria.json --face A1_Nord_Natalie --zip --verify-deterministic
fforge.exe batch --profiles .\profiles\
fforge.exe hub --name MyAssetHub
```

## Output strategies

| Strategy | What it does |
|---|---|
| **Pack-Local Reference** *(default)* | References installed records/files; adds required plugins as masters; copies nothing but the generated FaceGen. |
| **Shared Hub** | Followers carry a hub marker keyword → master a shared light-master `.esm`; shared body/skin/hair live in the hub, not each follower. |
| **Portable Standalone** | Copies referenced assets **only** after an explicit `RedistributionPermission` declaration; generates full credits + source manifests. |

## What each follower package contains

```
<Plugin>.esp            ← the follower plugin (ESPFE, ESL flag 0x200)
FaceGeom .nif           ← meshes\actors\character\facegendata\facegeom\<Plugin>\<FormID8>.nif
FaceTint .dds           ← textures\actors\character\facegendata\facetint\<Plugin>\<FormID8>.dds
manifest.json           ← build manifest
source-assets.json      ← provenance of every referenced asset
dependency-report.json  ← masters + Vortex source-mod provenance
rebuild-profile.json    ← re-run this follower with one command
build-report.html       ← human-readable build report
credits.md              ← asset credits (portable standalone)
```

## Verification

Generated plugins pass the full **Skyrim ship-gate**:

- HEDR version `1.71`, all records formVersion `44`
- ESL flag `0x200`, new FormIDs `0x800`–`0xFFF`
- HEDR record count = records + GRUPs
- Reopen cleanly in Mutagen / SSEEdit
- Rebuilding the same profile → **byte-identical** plugin (SHA256 match)

> ⚠️ **In-game testing:** actual recruit / trade / wait / dismiss / rehire must be
> validated by launching Skyrim with a generated follower installed — Follower Forge
> cannot run the game. Verify plugins in SSEEdit first, then test in-game.

## Solution architecture

| Project | Responsibility |
|---|---|
| `Domain` | Core types: profiles, records, assets, manifests, validation, FaceGen |
| `ModManagers` | Vortex discovery, deployment manifest (88 MB streaming parser), write-guard |
| `SkyrimRecords` | Mutagen indexing (20 record types), follower compiler, analyzers (combat/race/voice), hub compiler, header fixer, validator |
| `AssetIndex` | SQLite catalogue, loose + BSA file indexers, CharGen discovery |
| `FaceGen` | NiflySharp head-file wrapper, RaceMenu CharGen dirty-swap |
| `BuildPipeline` | Atomic builder, hub builder, batch builder, placement resolver, profile I/O, Vortex packager, determinism verifier |
| `Validation` | In-process ESP header ship-gate |
| `Cli` | `fforge` — env / index / search / inspect / detect / sample-profile / build / batch / hub |
| `Ui` | Avalonia desktop GUI (same engine as CLI) |
| `Tests` | xUnit — 37 tests (plugin lists, write guard, profile detect, catalog, compiler round-trip, determinism, builder outputs, ship-gate, FaceGen swap) |

## Build from source

> Only needed if you want to modify Follower Forge. The release ZIP is pre-built.

**Prerequisites:** .NET 10 SDK

```powershell
cd "Follower Forge 1.0.0"
.\Build-FollowerForge.ps1        # clean + build Release + run tests + boot-verify CLI
```

Or manually:

```powershell
dotnet build src/FollowerForge.slnx -c Release
dotnet test src/Tests
```

## Detection layer

Follower Forge detects (without modifying) the following frameworks and body systems:

- **Follower frameworks:** Nether's Follower Framework, Extensible Follower Frameworkes,
  My Home Is Your Home — matched by exact plugin name (no substring false positives)
- **Body systems:** BodySlide, CBBE, 3BA, HIMBO, UNP, BHUNP
- **Voices:** SOS ResourceIntegrated verified by on-disk voice-file check; vanilla
  follower voice set; unknowns are never hidden

Integration is **soft** and vanilla-compatible by default.

## License

MIT. See the source tree. Follower Forge reads your game/mod installation but never
redistributes game masters or third-party assets — portable-standalone builds refuse to
copy assets without an explicit `RedistributionPermission` declaration.

---

<div align="center">

**[⬇ Download the latest release](https://github.com/ShugokiFable/FollowerForge/releases)**

</div>
