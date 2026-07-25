# Follower Forge

Make a custom Skyrim SE/AE follower without ever opening the Creation Kit.

You pick her name, face, voice, class, combat style, outfit and **where in Skyrim she waits** —
all from your own installed mods, by name. Follower Forge writes a real, conflict-safe plugin
and an installable mod folder.

---

## Getting started

1. Unzip anywhere and run **Follower Forge.exe** (nothing to install — .NET is bundled).
2. First launch reads your mods. That takes about a minute; it only repeats when you install
   or deploy something new.
3. Walk the seven steps and press **Build follower**.
4. Install the produced folder (or `.zip`) in Vortex like any other mod, enable the plugin,
   and go find her.

Requires a Vortex-managed Skyrim Special Edition / Anniversary Edition install.

---

## What each step does

| Step | What you choose |
|---|---|
| **Who she is** | Name, sex, and whether she can die. Protected is the default: only you can kill her. |
| **Her look** | A face you exported from RaceMenu, plus her race. Vanilla races are listed first; custom races sit behind a checkbox because they become a requirement for anyone you share her with. |
| **Her voice** | Voices marked **FULL FOLLOWER** already have every recruit / trade / wait line. **SOS PACK** voices come from the Simply Open Source Voice Pack. Anything unverified is labelled honestly rather than hidden. |
| **How she fights** | Her class and combat style. Every style in your load order appears, including ones added by combat overhauls, with plain-English tags. You can copy a style into her plugin to tweak later — the original is never edited. |
| **What she wears** | Any outfit from your installed armour mods. |
| **Where she waits** | See below. |
| **Build her** | A summary, the build, and a plain list of anything worth knowing. |

### Her face

The best results come from RaceMenu: load your preset in game, open the **Sculpt** tab and press
**F5** to export the head. Follower Forge picks that export up automatically.

A preset file (`.jslot`) on its own is *not* enough to reproduce a sculpt exactly — the exported
head mesh is what carries the actual face.

If the face references textures you no longer have installed, the build says so and names them.
That usually means the mod that provided them was removed or renamed; either reinstall it or
re-export the face with your current mods.

### Where she waits

Follower Forge scans every mod you have that places its own NPC somewhere, and turns those into
a searchable list of places — **over 3,000 on a large setup**. Every one is a spot a real mod
author already shipped a character into, so it is known to be reachable and sensible.

Each entry tells you how many mods use it and whether it needs anything beyond the base game.
Most classic follower haunts — The Bannered Mare, Sleeping Giant Inn, The Bee and Barb,
Winking Skeever — need nothing but Skyrim itself.

---

## Sharing her

The build report tells you exactly what a downloader needs: either

> She needs nothing but the base game — safe to share with anyone.

or a list like *"Anyone who installs her also needs: KS Hairdos"*.

Your follower's face, body and hair come from other people's mods. **Listing them as
requirements is not the same as permission to redistribute them.** Follower Forge never copies
another author's assets unless you explicitly say you hold the right to, and it will not make
that claim on your behalf.

Every build also writes `credits.md`, `dependency-report.json` and `source-assets.json` so you
can see precisely what she leans on.

---

## What you get

```
FF_YourFollower.esp                         the plugin (ESL-flagged, loads in the light slot)
meshes/.../facegeom/FF_YourFollower.esp/    her face mesh
textures/.../facetint/FF_YourFollower.esp/  her face tint
manifest.json  source-assets.json  dependency-report.json  rebuild-profile.json
build-report.html  credits.md
```

Rebuilding the same follower produces a byte-identical plugin, so you can rebuild and re-share
without churn.

---

## Command line (optional)

`cli\FollowerForge.Cli.exe` drives the same engine, useful for batches and troubleshooting:

```
fforge env                                   what Follower Forge sees on this PC
fforge index                                 re-read your mods
fforge locations --text "bannered mare"      search spawn points
fforge races                                 which races are usable, and why
fforge assets --path "textures\...\x.dds"    is this texture actually installed?
fforge build --profile her.json --zip        build from a saved profile
fforge batch --profiles .\profiles\          build a whole folder of them
```

---

## Safety

Your game folder, Vortex staging folder and installed mods are **only ever read**. Everything
Follower Forge creates goes in its own workspace, and a guard actively refuses any write that
would land inside your game or mod manager.

---

## Building from source

Requires the .NET 10 SDK.

```powershell
.\Build-FollowerForge.ps1        # build + tests
.\Publish-FollowerForge.ps1      # self-contained exe + shareable zip in dist\
```

Projects: `Domain`, `ModManagers` (Vortex discovery, write guard), `SkyrimRecords` (Mutagen
indexing, follower compiler, placement), `AssetIndex` (SQLite catalogue, loose + BSA),
`FaceGen` (NiflySharp), `BuildPipeline`, `Validation`, `Cli`, `Ui` (Avalonia), `Tests` (xUnit).
