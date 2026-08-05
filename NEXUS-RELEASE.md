# Follower Forge — Nexus Mods page copy

**Version for this page:** 3.1.1  
**Primary file:** `FollowerForge-3.1.1-win-x64.zip`  
**SHA-256:** `F01D836FE2DEFE3D8EEF498514F662B60BE65155B09573CA7D10BE0C61419EF0`  
**Contents:** `Follower Forge.exe`, `cli\FollowerForge.Cli.exe`, `README.md`, `CHANGELOG.txt`

Use the short summary in the mod summary field. Paste the BBCode into the full description.  
Do **not** paste local disk paths, account names, or API keys.

Suggested Nexus category: **Utilities**  
Suggested tags: follower, companion, RaceMenu, ESPFE, tool, utility, Vortex, Windows, dialogue

Name collision note: several follower *characters* on Nexus use “Forge” in the name. This is the **Follower Forge builder tool**, not a character mod.

---

## Short summary (mod card / brief)

```
Windows tool that builds a custom Skyrim SE/AE follower without the Creation Kit. Choose face (RaceMenu export), voice, class, gear, spells, perks, spawn location, optional dialogue and more from your installed mods. Writes a conflict-safe ESPFE plugin and a Vortex-ready mod ZIP. Requires a Vortex-managed install. Self-contained EXE — no .NET install.
```

---

## Detailed description (BBCode)

```bbcode
[center][size=5][b]Follower Forge[/b][/size]
[i]Make a custom Skyrim SE/AE follower without opening the Creation Kit[/i]

Windows utility for Special Edition / Anniversary Edition.
Unzip, run [b]Follower Forge.exe[/b]. Self-contained — no .NET install.
[/center]

[size=4][b]What it is[/b][/size]
Follower Forge is an [b]out-of-game builder[/b]. It reads your [b]Vortex[/b] deployment and load order [b]read-only[/b], lets you design a follower from records you already have, and writes a [b]new[/b] installable mod folder + ZIP with an ESL-flagged plugin.

It does [b]not[/b] edit your installed plugins, your saves, or your game folder.

[size=4][b]Features (3.x suite)[/b][/size]
[list]
[*][b]Seven-step wizard[/b] — identity, look, voice, combat, equipment, spells/perks, location, build
[*][b]RaceMenu faces[/b] — FaceGen export + matching .jslot; complexion / face texture set (FTST) preserved when present
[*][b]Spawn points from real mods[/b] — places harvested from NPCs already placed by your load order
[*][b]Voices ordered by usefulness[/b] — vanilla → voice packs → mod voices; creature/unique voices hidden until you ask
[*][b]Marriage honesty[/b] — reports whether marriage will work on [i]your[/i] setup (vanilla vs RDO-style expansions)
[*][b]Books & belongings[/b] — lore items, potions, ingredients build correctly (not only armor/weapons)
[*][b]Custom dialogue[/b] — optional spoken lines with lip sync via xVASynth; location/time context; inherited dialogue scan per voice
[*][b]Alternate forms[/b] — vampire (scriptless), werewolf / mod transformations, optional creature followers
[*][b]Enemy to ally[/b], AI routine / sleep, optional evolution — see Experimental section
[*]Every build writes [b]test-in-game.txt[/b] with console commands for that follower’s exact features
[*]CLI included: [font=Courier New]cli\FollowerForge.Cli.exe[/font]
[/list]

[size=4][b]What’s new in 3.1.1[/b][/size]
[list]
[*][b]Slider-only preset warning[/b] — if a RaceMenu preset has many sliders and [b]no sculpt[/b], the face picker marks [b]NO SCULPT[/b] and the build reports [font=Courier New]FACE_SLIDERS_WITHOUT_SCULPT[/font]. Slider-only shaping does not survive on an NPC; sculpt in RaceMenu, then Export Head again.
[*]App and CLI version resources both report [b]3.1.1[/b].
[/list]

[size=4][b]What’s new in 3.1[/b][/size]
[list]
[*]Lore inventory types accepted (books, misc, ingestibles, ingredients)
[*]Preset head texture / complexion written to the NPC (neck seam fix for mismatched complexion)
[*]Voice pack file verification fixed; cleaner voice list UX
[/list]

[size=4][b]Requirements[/b][/size]
[b]Hard[/b]
[list]
[*]Windows 64-bit
[*]Skyrim Special Edition or Anniversary Edition
[*][b]Vortex[/b]-managed install (active deployment / profile)
[/list]

[b]For custom faces[/b]
[list]
[*]RaceMenu + Export Head (and any head mesh mods you want the follower to use, e.g. High Poly Head)
[/list]

[b]Optional[/b]
[list]
[*][url=https://www.nexusmods.com/skyrimspecialedition/mods/44184]xVASynth[/url] for custom voiced dialogue
[*]Relationship Dialogue Overhaul (or similar) if you want expanded marriage / dialogue coverage for more voices
[*][url=https://github.com/ShugokiFable/FaceForge]FaceForge[/url] to produce RaceMenu starting presets from photos
[/list]

[size=4][b]Installation[/b][/size]
[list=1]
[*]Download and unzip [font=Courier New]FollowerForge-3.1.1-win-x64.zip[/font] anywhere.
[*]Run [b]Follower Forge.exe[/b].
[*]First launch indexes the active Vortex deployment (can take a minute or two once).
[*]Walk the wizard and press [b]Build follower[/b].
[*]Install the produced ZIP with Vortex, enable the plugin, deploy.
[/list]

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
[*]Copy another author’s assets without [b]you[/b] declaring redistribution permission in the profile
[*]Launch Creation Kit or xEdit
[*]Create child followers or use child voices
[/list]

[size=4][b]Sharing followers you build[/b][/size]
Each build reports dependencies and whether the follower needs only the base game or also specific mods (race, voice, gear, etc.).  
You are responsible for permissions of any third-party assets packed into a portable/hub distribution.

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
[*][url=https://www.nexusmods.com/skyrimspecialedition/mods/44184]xVASynth[/url] (optional voice synthesis — not bundled)
[*]RaceMenu and the wider facegen ecosystem this tool integrates with (not redistributed)
[/list]

[size=4][b]Source / updates[/b][/size]
GitHub: [url=https://github.com/ShugokiFable/FollowerForge]github.com/ShugokiFable/FollowerForge[/url]

[size=4][b]Troubleshooting[/b][/size]
[list]
[*][b]Won’t find Vortex / game[/b] — confirm Vortex has an active Skyrim SE profile and a successful deployment.
[*][b]Face looks flat / wrong[/b] — ensure Export Head after sculpting; check for NO SCULPT / FACE_SLIDERS_WITHOUT_SCULPT; High Poly Head followers need HPH morphs + baked geometry.
[*][b]Voice “files missing”[/b] — re-run indexing after the voice pack is deployed; 3.1+ verifies files after the asset index exists.
[*][b]Build rejects an item[/b] — only inventory-legal types are accepted (armor, weapons, books, misc, potions, ingredients, etc.).
[*]When reporting issues, include: Follower Forge version, game version, Vortex yes, relevant feature steps, and [font=Courier New]test-in-game.txt[/font] from the build. [b]Do not[/b] post API keys or full paths containing your Windows username.
[/list]

[size=3][i]Utility executable package. Not a character follower. Does not ship Skyrim masters or third-party mod assets.[/i][/size]
```

---

## Files tab notes (for you, the uploader)

| Field | Value |
|-------|--------|
| Main file name | `FollowerForge-3.1.1-win-x64.zip` |
| Version | 3.1.1 |
| Category | Main file |
| Software description | Windows x64 utility + CLI; self-contained .NET; Vortex reader; Mutagen plugin writer |

## Claims you may make (evidence-backed)

- 278 automated tests passed; self-contained publish + boot check passed this session
- App/CLI FileVersion 3.1.1.0 after version-metadata fix
- Does not write to installed game/Vortex trees (writes new output folders only)

## Claims you should not make

- “All 3.x scripted features fully tested in long playthroughs” (still experimental)
- “Works with MO2” as a hard guarantee (primary path is Vortex)
- “Permission to redistribute any assets the user picks” (tool records their declaration only)
