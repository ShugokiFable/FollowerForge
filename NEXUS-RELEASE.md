# FollowerForge — Nexus Mods release kit

**Status:** ready to upload  
**Version:** 3.2.2  
**Main file to upload:** `FollowerForge-3.2.2-win-x64.zip`  
**Size:** 99,153,165 bytes  
**SHA-256:** `7F0D1F028AB8B95FA24D743CBDED88D18748A3ECA3032D95F0D6F0531D5E7ABA`  

**Archive contents:**
- `FollowerForge.exe`
- `cli\FollowerForge.Cli.exe`
- `README.md`
- `CHANGELOG.txt`

**Local path (for you only — do not put on Nexus):**  
`Z:\Backup\!Skyrim AE\!!!SkyrimAEaiWorkspace\FollowerForge\FollowerForge 3.2.2\dist\FollowerForge-3.2.2-win-x64.zip`  

**GitHub release:** https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.2  

Do **not** paste local disk paths, usernames, or API keys on the Nexus page.

---

## Nexus form fields

| Field | Suggested value |
|--------|------------------|
| Mod name | FollowerForge |
| Category | Utilities |
| Version | 3.2.2 |
| Tags | Utilities for Players, Followers, RaceMenu, tool, Vortex, MO2 |
| Language | English |
| Adult content | No (utility; generated followers may use adult assets the user already owns — the tool ships none) |
| Main file | `FollowerForge-3.2.2-win-x64.zip` |
| File type | Main File |
| Software type | Utility / tool (executable) |

**Name note:** Product name is **FollowerForge** (one word), pairing with **FaceForge**. Several character followers on Nexus use “Forge” in the title — this is the **builder tool**, not a character.

**Requirements (Nexus requirements list):**
- Skyrim Special Edition / Anniversary Edition (hard)
- Vortex **or** Mod Organizer 2 (hard)
- RaceMenu (hard for custom faces)
- Optional: xVASynth, RDO, High Poly Head, FSMP for SMP hair physics
- Soft recommend: FaceForge for photo → preset before Export Head

---

## Short summary (mod card — paste as-is)

```
Windows tool that builds a full custom Skyrim SE/AE follower without the Creation Kit — voice, dialogue, gear, spells, perks, spawn place, marriage truth, creatures/alts, and more from YOUR load order. Vortex or Mod Organizer 2. Writes an ESPFE plugin + installable mod folder/zip. Self-contained EXE. Pairs with FaceForge for photo → RaceMenu preset.
```

---

## Detailed description (BBCode — paste as-is)

```bbcode
[center][size=5][b]FollowerForge[/b][/size]
[i]A full custom companion from your load order — not just a face packager[/i]

Windows utility for Skyrim Special Edition / Anniversary Edition.
Unzip, run [b]FollowerForge.exe[/b]. Self-contained — no .NET install.
Pairs with [b]FaceForge[/b] (photo → RaceMenu preset).
[/center]

[size=4][b]What it is[/b][/size]
FollowerForge is an [b]out-of-game character builder[/b]. It reads your [b]Vortex[/b] or [b]Mod Organizer 2[/b] setup [b]read-only[/b], lets you design a follower from records you already have, and writes a [b]new[/b] installable mod folder (and zip) with an ESL-flagged plugin.

It does [b]not[/b] edit your installed plugins, your saves, or your game folder.

If you only need “RaceMenu head → zip a HPH face with class/perks,” other tools specialise there. FollowerForge is for when you want a [b]real companion[/b]: where she waits, what she says, what she carries, whether marriage works on [i]your[/i] list, and what anyone else needs to install her.

[size=4][b]Why people pick this[/b][/size]
[list]
[*][b]Voices that make sense[/b] — ranks vanilla → voice packs → mod voices; hides creature/unique voices until you ask; checks whether pack files are on disk
[*][b]Marriage that tells the truth[/b] — reports vanilla vs RDO-style coverage and whether downloaders need the same dialogue overhaul
[*][b]Inherited dialogue[/b] — scans your load order for lines already keyed to a voice and shows the count
[*][b]Custom spoken lines[/b] — optional lines with lip sync via xVASynth, with place/time context; refuses to quietly ship mute “custom” dialogue
[*][b]Spawn places from real mods[/b] — pick “The Bannered Mare,” not raw coordinates
[*][b]Loadout from your mods[/b] — armor, weapons, spells, perks, factions drawn from the active profile
[*][b]Face clarity[/b] — distinguishes missing Export Head vs slider-only (NO SCULPT) presets so you know when the face will not match RaceMenu
[*][b]Share package honesty[/b] — SHARE-CHECKLIST.txt and PERMISSIONS.md; no false “you may upload” claims
[*][b]SMP honesty[/b] — does not invent physics; FSMP is optional for hair that already has it
[/list]

[size=4][b]Requirements[/b][/size]
[b]Hard[/b]
[list]
[*]Skyrim Special Edition or Anniversary Edition
[*]Vortex or Mod Organizer 2 (active profile / deployment)
[*]RaceMenu (for custom faces / Export Head)
[/list]
[b]Optional[/b]
[list]
[*]xVASynth — custom spoken lines with lips
[*]Relationship Dialogue Overhaul (or similar) — marriage/dialogue coverage
[*]High Poly Head — if your preset uses it
[*]FSMP — only if the hair assets need it
[*][url=https://github.com/ShugokiFable/FaceForge]FaceForge[/url] — photo → RaceMenu starting preset before bake
[/list]

[size=4][b]Install[/b][/size]
[list=1]
[*]Download [font=Courier New]FollowerForge-3.2.2-win-x64.zip[/font]
[*]Unzip anywhere (not inside game Data)
[*]Run [b]FollowerForge.exe[/b]
[*]Point it at your Vortex or MO2 setup if it does not auto-detect
[*]Build → install the output mod folder/zip like any other mod
[/list]

[size=4][b]Update[/b][/size]
Replace the FollowerForge folder/EXE with the new zip contents. Rebuilt follower mods do not auto-update; rebuild if you need new tool features in a follower package.

[size=4][b]What this is not[/b][/size]
[list]
[*]Not a pre-made follower character
[*]Not Creation Kit
[*]Not an SKSE plugin inside Skyrim
[*]Not a guarantee that every third-party asset is redistributable — you must check permissions before sharing a built follower
[/list]

[size=4][b]3.2.2[/b][/size]
Brand alignment: product name, EXE, and docs are [b]FollowerForge[/b] (one word) to match FaceForge and the GitHub repo. Includes the 3.2.1 hang fix (Vortex preferred over houseCARL MO2 shim).

[size=4][b]Source / releases[/b][/size]
GitHub: [url=https://github.com/ShugokiFable/FollowerForge]ShugokiFable/FollowerForge[/url]
Release: [url=https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.2]v3.2.2[/url]

[size=4][b]Credits[/b][/size]
Bethesda (Skyrim). RaceMenu / CharGen ecosystem. Mutagen. Avalonia. Your installed mods remain the asset sources — credit their authors when you share a follower built from them.
```

---

## Permissions (Nexus form)

- You may upload this tool as released (utility EXE + docs).
- Generated followers are the [b]user's[/b] responsibility: credit asset authors; do not claim FollowerForge granted redistribution rights.
- No assets from other mods ship inside the FollowerForge download.

---

## Changelog (Nexus version field / sticky)

```
3.2.2
- Product name unified to FollowerForge (EXE, UI, docs) — pairs with FaceForge
- Same engine as 3.2.1 (Vortex-first hang fix retained)

3.2.1
- Prefer Vortex over SKYRIM_MO2_INSTANCE houseCARL shim; skip shim instances
- Catalogue freshness checks manager kind

3.2.0
- MO2 support, share package UX, face clarity, Nexus rewrite
```

---

## Troubleshooting (short)

- Hang / “re-reading mods” forever: use 3.2.1+; if SKYRIM_MO2_INSTANCE points at a houseCARL shim, Vortex should win automatically. Or set FFORGE_MO2_INSTANCE to a real MO2 instance.
- Face wrong in-game: Export Head from RaceMenu (not slider-only / NO SCULPT alone).
- Logs: under LocalAppData\FollowerForge when diagnosing.

---

## Upload checklist

See `NEXUS-UPLOAD-CHECKLIST.md` in this folder.