# Follower Forge — Nexus Mods page copy

**Version for this page:** 3.2.0
**SHA-256:** `0BE694467BBB5BFC0A613AA91359D01C1C0658F1C4C686B99B507A7A82D12411`  
**Primary file:** `FollowerForge-3.2.0-win-x64.zip`  
**Contents:** `Follower Forge.exe`, `cli\FollowerForge.Cli.exe`, `README.md`, `CHANGELOG.txt`

Use the short summary in the mod summary field. Paste the BBCode into the full description.  
Do **not** paste local disk paths, account names, or API keys.

Suggested Nexus category: **Utilities**  
Suggested tags: follower, companion, RaceMenu, ESPFE, tool, utility, Vortex, MO2, Windows, dialogue, voice

Name note: several character followers on Nexus use “Forge” in the title. This is the **Follower Forge builder tool**, not a character mod.

---

## Short summary (mod card / brief)

```
Windows tool that builds a full custom Skyrim SE/AE follower without the Creation Kit — voice, dialogue, gear, spells, perks, spawn place, marriage truth, creatures/alts, and more from YOUR load order. Vortex or Mod Organizer 2. Writes an ESPFE plugin + installable mod folder/zip. Self-contained EXE.
```

---

## Detailed description (BBCode)

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

[size=4][b]Why people pick this (the deep features)[/b][/size]
[list]
[*][b]Voices that make sense[/b] — on a big list you can have ~1,000 voice types. Sorted by name, pack voices hide under mudcrabs. Follower Forge ranks [b]vanilla → voice packs → mod voices[/b], hides creature/unique voices until you ask, and checks whether pack files are actually on disk.
[*][b]Marriage that tells the truth[/b] — vanilla allows eight voice types; RDO-style mods expand that. The app reports which case you are in and whether downloaders need the same dialogue overhaul.
[*][b]Inherited dialogue[/b] — scans your load order for lines already keyed to a voice (RDO, OOD, etc.) and shows the count before you write a word.
[*][b]Custom spoken lines[/b] — optional lines with lip sync via xVASynth, with place/time context. The build refuses to quietly ship mute “custom” dialogue.
[*][b]Spawn places from real mods[/b] — pick “The Bannered Mare,” not raw coordinates. Places are harvested from NPCs already placed by your load order.
[*][b]Full gear and lore[/b] — real ARMO/WEAP from the LO, plus books, keepsakes, potions, ingredients.
[*][b]Combat your way[/b] — class, combat style, optional full 18-skill + Health/Magicka/Stamina edit.
[*][b]Creatures, vampires, werewolves[/b] — non-humanoid races behind an explicit tick; scriptless vampire swap; transformation options.
[*][b]Enemy to ally, evolution, random spawn[/b] — optional Papyrus features (see Experimental).
[*][b]RaceMenu faces[/b] — Export Head + jslot; complexion (FTST) preserved when present; [b]slider-only vs sculpt[/b] called out so you do not ship a flat follower face by accident.
[*][b]Honest dependencies[/b] — every build reports what the base game covers vs what installers also need.
[*]Every build writes [b]test-in-game.txt[/b], [b]SHARE-CHECKLIST.txt[/b], credits, and (when relevant) an [b]RSVexclude.ini[/b] for packaged texture folders.
[*]CLI included: [font=Courier New]cli\FollowerForge.Cli.exe[/font]
[/list]

[size=4][b]What’s new in 3.2.0[/b][/size]
[list]
[*][b]Mod Organizer 2 support[/b] — discovers an MO2 instance (or [font=Courier New]FFORGE_MO2_INSTANCE[/font] / [font=Courier New]SKYRIM_MO2_INSTANCE[/font]), reads the selected profile, indexes mods by modlist priority, and builds a private hardlink plugin view for Mutagen. Vortex still works as before.
[*][b]Share-ready package docs[/b] — [font=Courier New]SHARE-CHECKLIST.txt[/font] on every build; [font=Courier New]RSVexclude.ini[/font] when you use own-hub / author texture prefixes.
[*][b]Clearer face import language[/b] — missing .jslot / head export vs [b]NO SCULPT[/b] (slider-only shape) vs missing tint; optional SavePCFace companion noted when present.
[*][b]SMP hair honesty[/b] — RaceMenu HDPT hair can be selected like any head part; physics still need FSMP (or equivalent) on the player’s game. Documented, not oversold.
[/list]

[size=4][b]Face → follower (how faces fit)[/b][/size]
[list=1]
[*]Sculpt / finish in RaceMenu and [b]Export Head[/b] (NIF + DDS + matching .jslot).
[*]Pick the export in Follower Forge step 2. Read the chip: READY / NO TINT / NO SCULPT / CANNOT BUILD.
[*][b]NO SCULPT[/b] means lots of RaceMenu sliders and no sculpt geometry — looks right on [i]you[/i], flattens on an NPC. Sculpt anything, re-export, rebuild. (Different problem from black face / missing head parts.)
[*]Optional: [url=https://github.com/ShugokiFable/FaceForge]FaceForge[/url] turns a photograph into a RaceMenu [i]starting[/i] preset first.
[/list]

[size=4][b]Requirements[/b][/size]
[b]Hard[/b]
[list]
[*]Windows 64-bit
[*]Skyrim Special Edition or Anniversary Edition
[*][b]Vortex[/b] with a deployed SE profile, [b]or[/b] [b]Mod Organizer 2[/b] with a normal instance (ModOrganizer.ini + profiles + mods)
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
[*]Download and unzip [font=Courier New]FollowerForge-3.2.0-win-x64.zip[/font] anywhere.
[*]Run [b]Follower Forge.exe[/b].
[*]First launch indexes your active Vortex deployment or MO2 profile (can take a minute or two once).
[*]Walk the wizard and press [b]Build follower[/b].
[*]Install the produced folder/ZIP with your mod manager, enable the plugin, deploy/run.
[/list]

MO2 tip: set [font=Courier New]FFORGE_MO2_INSTANCE[/font] to your instance folder if auto-detect misses it. Follower Forge never writes into your MO2 mods tree or game Data.

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
[*]Guarantee “perfect” SMP physics or every niche hair mesh without end-user physics mods
[/list]

[size=4][b]Sharing followers you build[/b][/size]
Each build reports dependencies and whether the follower needs only the base game or also specific mods.  
Read [font=Courier New]SHARE-CHECKLIST.txt[/font] and [font=Courier New]credits.md[/font] before uploading. You are responsible for third-party permissions.

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
[*][b]Won’t find Vortex / MO2[/b] — Vortex: confirm an active SE profile and deployment. MO2: confirm ModOrganizer.ini + selected profile; set FFORGE_MO2_INSTANCE if needed.
[*][b]Face flat / wrong[/b] — check NO SCULPT / FACE_SLIDERS_WITHOUT_SCULPT; sculpt + Export Head; HPH followers need HPH morphs baked into the export.
[*][b]Black face[/b] — incomplete Export Head / missing parts; re-export with every part showing (different from slider-only).
[*][b]Voice “files missing”[/b] — re-index after the voice pack is enabled/deployed (3.1+).
[*]When reporting issues, include: Follower Forge version, Vortex or MO2, game version, and test-in-game.txt. [b]Do not[/b] post API keys or full paths containing your Windows username.
[/list]

[size=3][i]Utility executable package. Not a character follower. Does not ship Skyrim masters or third-party mod assets.[/i][/size]
```

---

## Files tab notes

| Field | Value |
|-------|--------|
| Main file name | `FollowerForge-3.2.0-win-x64.zip` |
| Version | 3.2.0 |
| Category | Main file |

## Claims you may make

- 283 automated tests passed (3.2.0 session)
- Vortex and MO2 discovery paths implemented; hardlink plugin view for MO2 does not write into game Data
- SHARE-CHECKLIST / clearer face notes ship with builds

## Claims you should not make

- “All scripted 3.x features fully tested in long playthroughs”
- “Every SMP hair mod guaranteed”
- “Permission to redistribute any assets the user picks”
