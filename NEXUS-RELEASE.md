# Follower Forge — Nexus Mods release kit

**Status:** ready to upload  
**Version:** 3.2.1  
**Main file to upload:** `FollowerForge-3.2.1-win-x64.zip`  
**Size:** 99,153,033 bytes  
**SHA-256:** `D81A1D6E022B1F1BE0294A3B47EA9F83D4AF47A63049007CBA7358D4E0028385`  

**Archive contents:**
- `Follower Forge.exe`
- `cli\FollowerForge.Cli.exe`
- `README.md`
- `CHANGELOG.txt`

**Local path (for you only — do not put on Nexus):**  
`Z:\Backup\!Skyrim AE\!!!SkyrimAEaiWorkspace\Follower Forge\Follower Forge 3.2.1\dist\FollowerForge-3.2.1-win-x64.zip`  

**GitHub release:** https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.1  

Do **not** paste local disk paths, usernames, or API keys on the Nexus page.

---

## Nexus form fields

| Field | Suggested value |
|--------|------------------|
| Mod name | Follower Forge |
| Category | Utilities |
| Version | 3.2.1 |
| Tags | Utilities for Players, Followers, RaceMenu, tool, Vortex, MO2 |
| Language | English |
| Adult content | No (utility; generated followers may use adult assets the user already owns — the tool ships none) |
| Main file | `FollowerForge-3.2.1-win-x64.zip` |
| File type | Main File |
| Software type | Utility / tool (executable) |

**Name note:** Several character followers on Nexus use “Forge” in the title. This is the **builder tool**, not a character.

**Requirements (Nexus requirements list):**
- Skyrim Special Edition / Anniversary Edition (hard)
- Vortex **or** Mod Organizer 2 (hard)
- RaceMenu (hard for custom faces)
- Optional: xVASynth, RDO, High Poly Head, FSMP for SMP hair physics
- Soft recommend: FaceForge for photo → preset before Export Head

---

## Short summary (mod card — paste as-is)

```
Windows tool that builds a full custom Skyrim SE/AE follower without the Creation Kit — voice, dialogue, gear, spells, perks, spawn place, marriage truth, creatures/alts, and more from YOUR load order. Vortex or Mod Organizer 2. Writes an ESPFE plugin + installable mod folder/zip. Self-contained EXE.
```

---

## Detailed description (BBCode — paste as-is)

```bbcode
[center][size=5][b]Follower Forge[/b][/size]
[i]A full custom companion from your load order — not just a face packager[/i]

Windows utility for Skyrim Special Edition / Anniversary Edition.
Unzip, run [b]Follower Forge.exe[/b]. Self-contained — no .NET install.
[/center]

[size=4][b]What it is[/b][/size]
Follower Forge is an [b]out-of-game character builder[/b]. It reads your [b]Vortex[/b] or [b]Mod Organizer 2[/b] setup [b]read-only[/b], lets you design a follower from records you already have, and writes a [b]new[/b] installable mod folder (and zip) with an ESL-flagged plugin.

It does [b]not[/b] edit your installed plugins, your saves, or your game folder.

If you only need “RaceMenu head → zip a HPH face with class/perks,” other tools specialise there. Follower Forge is for when you want a [b]real companion[/b]: where she waits, what she says, what she carries, whether marriage works on [i]your[/i] list, and what anyone else needs to install her.

[size=4][b]Why people pick this[/b][/size]
[list]
[*][b]Voices that make sense[/b] — ranks vanilla → voice packs → mod voices; hides creature/unique voices until you ask; checks whether pack files are on disk
[*][b]Marriage that tells the truth[/b] — reports vanilla vs RDO-style coverage and whether downloaders need the same dialogue overhaul
[*][b]Inherited dialogue[/b] — scans your load order for lines already keyed to a voice and shows the count
[*][b]Custom spoken lines[/b] — optional lines with lip sync via xVASynth, with place/time context; refuses to quietly ship mute “custom” dialogue
[*][b]Spawn places from real mods[/b] — pick “The Bannered Mare,” not raw coordinates
[*][b]Full gear and lore[/b] — real ARMO/WEAP plus books, keepsakes, potions, ingredients
[*][b]Combat your way[/b] — class, combat style, optional full 18-skill + Health/Magicka/Stamina edit
[*][b]Creatures, vampires, werewolves[/b] — non-humanoid races behind an explicit tick; scriptless vampire swap; transformation options
[*][b]Enemy to ally, evolution, random spawn[/b] — optional Papyrus features (see Experimental)
[*][b]RaceMenu faces[/b] — Export Head + jslot; complexion preserved when present; [b]slider-only vs sculpt[/b] called out so you do not ship a flat follower face by accident
[*][b]Honest dependencies[/b] — every build reports what the base game covers vs what installers also need
[*]Every build writes [b]test-in-game.txt[/b], [b]SHARE-CHECKLIST.txt[/b], credits, and (when relevant) [b]RSVexclude.ini[/b]
[*]CLI included: [font=Courier New]cli\FollowerForge.Cli.exe[/font]
[/list]

[size=4][b]What’s new in 3.2.x[/b][/size]
[list]
[*][b]3.2.1[/b] — startup hotfix: no longer hangs when houseCARL’s Vortex shim is present via SKYRIM_MO2_INSTANCE; prefers real Vortex
[*][b]3.2.0[/b] — Mod Organizer 2 support, SHARE-CHECKLIST / RSV exclusion docs, clearer face import language, honest SMP hair notes
[*][b]3.1.x[/b] — lore items build, complexion/FTST ships, voice ranking, NO SCULPT warning for slider-only presets
[/list]

[size=4][b]Face → follower[/b][/size]
[list=1]
[*]Sculpt / finish in RaceMenu and [b]Export Head[/b] (NIF + DDS + matching .jslot).
[*]Pick the export in step 2. Read the chip: READY / NO TINT / NO SCULPT / CANNOT BUILD.
[*][b]NO SCULPT[/b] means lots of RaceMenu sliders and no sculpt geometry — looks right on [i]you[/i], flattens on an NPC. Sculpt anything, re-export, rebuild.
[*]Optional: [url=https://github.com/ShugokiFable/FaceForge]FaceForge[/url] turns a photograph into a RaceMenu starting preset first.
[/list]

[size=4][b]Requirements[/b][/size]
[b]Hard[/b]
[list]
[*]Windows 64-bit
[*]Skyrim Special Edition or Anniversary Edition
[*][b]Vortex[/b] with a deployed SE profile, [b]or[/b] a real [b]Mod Organizer 2[/b] instance
[/list]

[b]For custom faces[/b]
[list]
[*]SKSE + RaceMenu + Export Head
[*]Any head mesh you actually use (vanilla, High Poly Head, etc. — not hard-locked to one head)
[/list]

[b]Optional[/b]
[list]
[*][url=https://www.nexusmods.com/skyrimspecialedition/mods/44184]xVASynth[/url] for custom voiced dialogue
[*]Relationship Dialogue Overhaul (or similar) for expanded marriage / voice coverage
[*]FSMP (or equivalent) if you use SMP hair
[*][url=https://github.com/ShugokiFable/FaceForge]FaceForge[/url] for photo → RaceMenu presets
[/list]

[size=4][b]Installation[/b][/size]
[list=1]
[*]Download and unzip [font=Courier New]FollowerForge-3.2.1-win-x64.zip[/font] anywhere.
[*]Run [b]Follower Forge.exe[/b].
[*]First launch indexes your active Vortex deployment or MO2 profile (can take a minute or two once).
[*]Walk the wizard and press [b]Build follower[/b].
[*]Install the produced folder/ZIP with your mod manager, enable the plugin, deploy/run.
[/list]

MO2 tip: set [font=Courier New]FFORGE_MO2_INSTANCE[/font] to your real MO2 instance folder if auto-detect misses it. Do not point Follower Forge at a houseCARL Vortex shim.

[size=4][b]Experimental (Papyrus) features[/b][/size]
These add a script to the follower. Records compile and validate, but [b]long-session in-game confirmation is still incomplete[/b]:
[list]
[*]Evolution
[*]Transformation (werewolf / custom)
[*]Random spawn points
[*]Enemy to ally
[/list]
Test them on a save you can throw away. Scripted followers can behave oddly if imported into some follower frameworks (NFF, EFF). Ordinary record-only followers are unaffected.

[size=4][b]What it will not do[/b][/size]
[list]
[*]Edit installed plugins, saves, or the game folder
[*]Copy another author’s assets without [b]you[/b] declaring redistribution permission (own-hub / portable modes)
[*]Launch Creation Kit or xEdit
[*]Create child followers or use child voices
[*]Guarantee every niche hair mesh / SMP physics without end-user physics mods
[/list]

[size=4][b]Sharing followers you build[/b][/size]
Each build reports dependencies. Read [font=Courier New]SHARE-CHECKLIST.txt[/font] and [font=Courier New]credits.md[/font] before uploading. You are responsible for third-party permissions.

[size=4][b]Permissions (this tool)[/b][/size]
[list]
[*]You may use Follower Forge to create followers for personal use and for mods you publish, subject to the rights of assets [b]you[/b] include.
[*]Do not reupload Follower Forge itself as your own mod without permission.
[*]Follower Forge does not redistribute Bethesda masters or other authors’ mods inside its own ZIP.
[*]MIT license for Follower Forge source/binaries you build from the public repository (see GitHub).
[/list]

[size=4][b]Credits[/b][/size]
[list]
[*][url=https://github.com/Mutagen-Modding/Mutagen]Mutagen[/url]
[*][url=https://github.com/ousnius/NiflySharp]NiflySharp[/url]
[*][url=https://www.nexusmods.com/skyrimspecialedition/mods/44184]xVASynth[/url] (optional — not bundled)
[*]RaceMenu and the facegen ecosystem this tool integrates with (not redistributed)
[/list]

[size=4][b]Source / updates[/b][/size]
GitHub: [url=https://github.com/ShugokiFable/FollowerForge]github.com/ShugokiFable/FollowerForge[/url]

[size=4][b]Troubleshooting[/b][/size]
[list]
[*][b]Stuck on re-reading mods[/b] — use [b]3.2.1+[/b]. Older 3.2.0 could hang if houseCARL’s Vortex shim was visible via SKYRIM_MO2_INSTANCE.
[*][b]Won’t find Vortex / MO2[/b] — Vortex: active SE profile + deploy. MO2: real instance with ModOrganizer.ini; set FFORGE_MO2_INSTANCE if needed.
[*][b]Face flat / wrong[/b] — check NO SCULPT; sculpt + Export Head.
[*][b]Black face[/b] — incomplete Export Head / missing parts (different from slider-only).
[*]When reporting issues, include: Follower Forge version, Vortex or MO2, game version, and test-in-game.txt. [b]Do not[/b] post API keys or full paths with your Windows username.
[/list]

[size=3][i]Utility executable package. Not a character follower. Does not ship Skyrim masters or third-party mod assets.[/i][/size]
```

---

## Permissions / credits (Nexus checkboxes guidance)

- Tool binaries + MIT source; no redistributed game assets in the tool zip  
- Users must secure permission for any third-party assets they package into followers  
- AI assistance used in development; do not claim all features are long-session runtime proven  

## Claims you may make

- 285 automated tests passed for 3.2.1; publish + boot check passed  
- Vortex primary path confirmed working after 3.2.1 hotfix  
- MO2 supported for real instances (not houseCARL shim by default)  

## Claims you must not make

- All scripted features fully tested in long playthroughs  
- Every SMP hair mod guaranteed  
- Permission to redistribute any assets the user picks  
