# Follower Forge

A Windows desktop application (C# / .NET 10, Avalonia, Mutagen.Bethesda, NiflySharp, SQLite,
Serilog) that compiles **real custom followers** from an existing heavily-modded Skyrim SE/AE
installation. Every generated follower is a genuine ESPFE with correct NPC records, follower
factions, a player relationship, world placement, voice/class/combat style, FaceGen mesh + tint,
a dependency list, and an installable Vortex mod folder.

The game directory, Vortex staging folder, deployed Data folder, and all installed source mods
are treated as **strictly read-only**. All work happens in a dedicated workspace; builds are
atomic (staging → validate → publish) and deterministic.

## Requirements

- .NET 10 SDK (the solution targets `net10.0`)
- A Vortex-managed Skyrim SE/AE install (hardlink deployment)

## Build

```powershell
.\Build-FollowerForge.ps1        # clean + build Release + run tests + boot-verify CLI
```

## CLI (`fforge`)

The CLI and the UI share the same build engine, so every build is reproducible and scriptable.

```powershell
dotnet run --project src/Cli -- env                       # environment + diagnostic report
dotnet run --project src/Cli -- index                     # build the modpack catalogue (SQLite)
dotnet run --project src/Cli -- search --type csty --text bandit
dotnet run --project src/Cli -- inspect --formkey 000800:NPCWeaponVariance.esp
dotnet run --project src/Cli -- detect                    # follower frameworks + body systems
dotnet run --project src/Cli -- sample-profile --out aria.json
dotnet run --project src/Cli -- build --profile aria.json --face A1_Nord_Natalie --zip --verify-deterministic
dotnet run --project src/Cli -- batch --profiles .\profiles\
dotnet run --project src/Cli -- hub --name MyAssetHub
```

## Output strategies

| Strategy | What it does |
|---|---|
| **Pack-Local Reference** (default) | References installed records/files; adds required plugins as masters; copies nothing but the generated FaceGen. |
| **Shared Hub** | Followers carry a hub marker keyword → master a shared light-master `.esm`; shared body/skin/hair live in the hub, not each follower. |
| **Portable Standalone** | Copies referenced assets **only** after an explicit `RedistributionPermission` declaration; generates full credits + source manifests. |

## Each follower package contains

`<Plugin>.esp` · FaceGeom `.nif` · FaceTint `.dds` · `manifest.json` · `source-assets.json` ·
`dependency-report.json` · `rebuild-profile.json` · `build-report.html` · `credits.md`
(no MO2 `meta.ini`).

FaceGen paths follow the engine convention:
`meshes\actors\character\facegendata\facegeom\<Plugin>\<FormID8>.nif` and
`textures\actors\character\facegendata\facetint\<Plugin>\<FormID8>.dds`.

## Projects

`Domain` · `ModManagers` (Vortex discovery, write-guard) · `SkyrimRecords` (Mutagen indexing,
compiler, analyzers) · `AssetIndex` (SQLite catalogue, loose + BSA) · `FaceGen` (NiflySharp
dirty-swap) · `BuildPipeline` (atomic builder, hub, batch, packaging) · `Validation` (ship-gate)
· `Cli` · `Ui` (Avalonia) · `Tests` (xUnit).

## Verification

Generated plugins pass the Skyrim ship-gate (HEDR 1.71, TES4 + all records formVersion 44,
ESL flag `0x200`, new FormIDs `0x800–0xFFF`, HEDR record count = records + GRUPs) and reopen
cleanly in Mutagen. Rebuilding the same profile produces a byte-identical plugin.

**Not yet done in-game:** actual recruit / trade / wait / dismiss / rehire must be tested by
launching Skyrim with a generated follower installed — the tool cannot run the game.
