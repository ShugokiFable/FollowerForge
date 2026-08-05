# Follower Forge 3.0.1

Make a custom Skyrim SE/AE follower without opening the Creation Kit.

You choose the follower's identity, exported face, voice, class, combat style, automatic or fully
customized skills and primary stats, actual starting equipment, spells, perks, level scaling, and
where in Skyrim they wait. Follower Forge reads the active Vortex profile and writes a real
ESL-flagged plugin plus an installable Vortex ZIP.

## Getting started

1. Unzip anywhere and run **Follower Forge.exe**. The self-contained build includes .NET.
2. First launch indexes the active Vortex deployment. The catalogue rebuilds when the deployment
   changes or when Follower Forge's catalogue schema changes.
3. Complete the seven wizard steps and press **Build follower**.
4. Install the produced ZIP in Vortex, enable its plugin, and deploy normally.

Requires a Vortex-managed Skyrim Special Edition or Anniversary Edition installation.

## What each step does

| Step | What you choose |
|---|---|
| **Who they are** | Name, sex, protected/essential status, marriageability, and player-scaled or fixed leveling. |
| **Their look** | A RaceMenu FaceGen export, race, and optional skin armor. Leaving the skin unset inherits the race default and your installed body replacer. |
| **Their voice** | A voice from the active load order, with follower-capability labels where verified. Optionally, custom lines of your own — see below. |
| **How they fight** | Class, combat style, temperament, and optional full customization of all 18 skills plus Health, Magicka, and Stamina. |
| **Equipment** | Real ARMO records grouped as torso, helmet, gauntlets, boots, shields, accessories, and other, plus weapons. Multiple equipment pieces can be selected. Conflicting biped slots are reported. |
| **Spells and perks** | Multiple installed SPEL and PERK records. The builder validates the record types and writes them directly to the NPC. |
| **Where they wait** | A searchable location drawn from placements already present in the active load order, plus what she does while waiting and whether she sleeps at night. |
| **Build** | Summary, validation report, dependency list, mod folder, and Vortex-ready ZIP. |

Legacy OTFT outfit records remain available only as an optional compatibility choice. They are not
the normal equipment workflow in 2.1, and mixing an OTFT with real armor produces a warning.
For real armor choices, Follower Forge creates a private engine starting-equipment set behind the
scenes because Skyrim uses `DefaultOutfit` to decide what an NPC initially wears. The selected
ARMO pieces remain the follower's actual, tradable inventory; weapons remain normal inventory.

### Skills and stats

Automatic calculation remains enabled by default. In that mode Skyrim derives the follower's
skills and primary stats from the selected class and level, matching the normal game workflow.

Open **How they fight → Skills & stats** and choose **Custom** when you want exact control. The
editor exposes One-Handed, Two-Handed, Archery, Block, Smithing, both armor skills, every magic
school, Sneak, Lockpicking, Pickpocket, Alchemy, Speech, and Enchanting, plus Health, Magicka, and
Stamina. Every skill field reserves enough space to show its value as well as the spinner buttons.
Eight optional presets provide editable starting points. Custom mode disables
`AutoCalcStats` and writes a complete DNAM player-skills block; profiles made before 2.1.2 remain
automatic.

### FaceGen

For a RaceMenu face, load the preset in game, open the **Sculpt** tab, and press **F5** to export
the head. Keep a matching `.jslot` with the same name. The NIF/DDS hold the sculpt and baked tint;
the jslot supplies the NPC record's weight, head parts, face values, tints, and hair color. A custom
face build stops if that matching record data is missing instead of silently creating a default face.

Follower Forge checks referenced face assets and reports missing paths. It does not copy another
author's assets unless the profile contains an explicit redistribution declaration.

### Custom lines

The voice step has an optional **Custom lines** tab. Write what she says, choose when she says it
(on greeting, on parting, idle chatter, or a topic the player can pick from the menu), and Follower
Forge compiles a real dialogue quest with DIAL/INFO records, a `SEQ` file so the quest starts in an
existing save, and one lipsynced `.fuz` per line.

Speaking the lines aloud requires [xVASynth](https://www.nexusmods.com/skyrimspecialedition/mods/44184)
with its `lip_fuz` plugin, plus the voice model for the voice you picked. The tab states up front
whether the chosen voice can be spoken, so you find out before writing a dozen lines rather than at
build time. Without xVASynth you can still ship the lines, but only as silent subtitles, and you have
to opt into that deliberately — the build fails rather than quietly producing a mute follower.

Lines are scoped to the generated NPC alone (a `GetIsID` condition on the quest). Follower mods
normally scope dialogue by voice type, which is only safe when the mod ships its own unique voice;
doing that with a stock voice would put your lines in the mouth of every NPC sharing it.

Child voices cannot be used for a generated follower, in the wizard or from a profile.

### What she already says

Run `fforge voice-coverage --scan` once. Follower Forge then reads every installed plugin for
dialogue keyed to a voice type and tells you what each voice inherits for free — for example
`FemaleEvenToned  +4,613 lines from 8 mods`.

This is how Relationship Dialogue Overhaul support works, and it needs nothing added to the
generated plugin: RDO keys its lines to voice types, so a follower is covered the moment she uses
one it handles. The same scan finds every other dialogue mod you have without being told about it.
The build report lists the contributing mods and reminds you that anyone installing her needs them
to hear those lines.

### Creature and non-humanoid followers

Most unusual followers are just custom races. A dwarven mech like Steadfast Machine's Maiden has
head data, so it behaves like any other custom race here — you can even give her a RaceMenu face.

True creatures are different: a race like HSF Baby Dragon's has no head data at all, so no face
can ever be built for it. Those are hidden until you tick **Include creatures** on the look step.
She then uses that race's own model and animations — nothing is authored here — but she has no
face to customise, equipment usually will not show, and dialogue depends entirely on what that
race's mod provides. Choosing a creature race together with a RaceMenu face stops the build,
because that combination cannot work.

Child races are never offered, with or without that option.

### Vampires

Tick "She is a vampire" on the look step and her race is swapped for its vampire form, with the
`Vampire` and `ActorTypeUndead` keywords added. That is exactly what a vanilla vampire NPC
carries — no script, no abilities, no special faction — so nothing else is added.

Vampire forms are found by name (`<race>Vampire`), the convention every vanilla playable race
follows and custom-race mods copy. If your chosen race has no vampire form the build stops and
names the race it looked for, rather than handing you a follower who quietly is not a vampire.

Werewolves work differently. Skyrim has a single `WerewolfBeastRace` rather than a werewolf form
per race, so a werewolf is a *transformation* rather than a race swap — see below.

### Changing form in combat (experimental, off by default)

She can transform when a fight starts and change back when it ends.

**Werewolf** uses the game's own `WerewolfBeastRace` and `WerewolfChangeFX`, so it needs nothing
installed beyond Skyrim. **Custom** points at a race and/or spell from your own mods — including
spell-only, which is how the transforming followers on Nexus actually do it (a spell cast a few
seconds into combat).

Like the growth option this adds a script, and carries the same warning: scripts persist in save
files, and this has not been confirmed working in game. A beast form also cannot wear or wield her
equipment — that is Skyrim's behaviour, not a fault in the follower, and reverting restores it.

### Enemy to ally

Instead of waiting somewhere to be recruited, she can start as an enemy you have to beat.

A hostile version of her waits at one of the places you chose, fighting alongside bandits, draugr,
warlocks or creatures so the dungeon's own inhabitants leave her alone. Beat her, loot the spell
tome she is carrying, and read it. The spell it teaches summons her to you as an ordinary
follower — essential, recruitable, with all her dialogue. Until then she does not exist in the
world at all.

There is no death script involved: the tome is simply in her inventory, so beating her is what
hands it over. That is how the Enemy-to-Ally mods do it too.

### Starting somewhere different each game

On the location step you can add up to four places instead of one. She then starts at whichever
of them the game picks at random — the idea behind the Enemy-to-Ally followers.

An invisible marker is placed at each spot and a small quest moves her to one of them. Two things
are worth knowing:

- **It uses our own script.** Every Enemy-to-Ally mod ships the same shared `KWYK_Quest`, which is
  why they all report conflicts with one another. Follower Forge ships `FF_RandomSpawn` instead,
  so it conflicts with nothing.
- **She is moved, not respawned.** The E2A mods place a fresh copy of the actor; we move the one
  that was already placed, so she keeps a single persistent reference that dialogue conditions,
  relationships and follower frameworks can all track.

If the script never runs she simply stays at the first place you chose.

### Her routine

An NPC with no AI package is not frozen — the engine sandboxes actors by default, which is why a
follower built without one still sits and eats. Choosing a package is about control: how far she
strays from where you placed her, whether she keeps to that spot or settles wherever she happens
to be, and whether she goes to bed at night.

Follower Forge references vanilla packages rather than authoring new ones, so this adds no
requirement beyond Skyrim itself. Order matters and fails quietly: Skyrim runs the first package
whose conditions hold, so the sleep package is always listed above the sandbox — put it the other
way round and she would simply never sleep.

### How she regards people

The relationship rank (Lover, Ally, Confidant, Friend, Acquaintance, Rival, Foe, Enemy,
Archnemesis) is written to a real RELA record. It is not cosmetic: mods that vary dialogue by
relationship, Relationship Dialogue Overhaul in particular, choose different lines for a Friend
than for a Confidant.

You can also give her history with other people already in the world — a sister, an old friend,
a rival. Search for them by name and pick a rank; each becomes its own RELA record. Note that
every NPC you reference makes the mod they come from a requirement for anyone installing her.

### Confidence

Her starting confidence uses the game's own five ranks: Cowardly, Cautious, Average, Brave,
Foolhardy. Cowardly makes her flee from danger, which is the right starting point if you want
someone who has to grow into the job — the game and your other mods handle what happens after
that. Only Foolhardy makes her aggressive enough to start fights herself.

### Letting her grow (experimental, off by default)

Optionally she can start timid and grow into the job, gaining confidence, combat skills, health,
stamina and magicka each time she comes through enough fights beside you — the idea behind Melana
the War Maiden. Her phase and progress are stored in globals, so you can read or change them from
the console, and a one-line patch plugin can retune the pace.

**This is the only feature that puts a script in your follower.** Everything else Follower Forge
writes is plain records, and records do not persist in a save the way scripts do. It is off unless
you tick it, the build warns you when it is on, and the script's source ships next to the compiled
version so you can read exactly what is running. It has not been confirmed working in game — test
her on a save you can throw away.

If the script never runs, she is simply a normal follower at her starting values. Nothing is
removed or replaced.

### Marriage

Ticking "she can be married" adds the faction, but whether the wedding option actually appears
depends on her **voice**. Vanilla only allows eight voice types. Mods add far more — on a load
order with Relationship Dialogue Overhaul, 48 voice types can marry instead of 8.

Follower Forge reads your actual load order (from the `voice-coverage` scan) and tells you which
case you are in: no extra mods needed, or "she can only marry because of RDO — anyone who installs
her needs it too".

### Testing her in game

Every build writes `test-in-game.txt` next to the plugin, listing the console commands for the
features that follower actually has. It uses `help "<her name>" 0` rather than fixed FormIDs,
because a light plugin's records land at different runtime IDs in every load order.

Each section also names the symptom that means a feature did not run, which is the fastest way to
tell a broken script from an unlucky test.

### Locations

Follower Forge scans placed NPCs in the active Vortex load order and turns their cells and
coordinates into searchable spawn choices. This is source evidence that a location is used by a
shipped mod; it is not a substitute for testing the generated follower in game.

## Sharing

Every build identifies required plugins and their Vortex source mods. Requirements and
redistribution permission are different: listing a hair, armor, body, race, spell, or perk mod as
a requirement does not grant permission to bundle that author's assets.

Builds include:

```text
FF_YourFollower.esp
meshes/.../facegeom/FF_YourFollower.esp/       optional FaceGen mesh
textures/.../facetint/FF_YourFollower.esp/     optional FaceGen tint
manifest.json
source-assets.json
dependency-report.json
rebuild-profile.json
build-report.html
credits.md
```

Generated file and ZIP timestamps use the actual build time. A profile may explicitly pin
`BuildTimestampUnix` when deterministic metadata is needed. Rebuilding the same profile still
produces a byte-identical plugin.

## Command line

`cli\FollowerForge.Cli.exe` uses the same build and validation pipeline:

```text
fforge env
fforge index
fforge search --type armo --text "iron"
fforge search --type spel --text "flames"
fforge locations --text "bannered mare"
fforge faces
fforge voices
fforge build --profile follower.json --zip
fforge batch --profiles .\profiles\
```

## Safety and validation boundary

Skyrim `Data`, Vortex staging, Vortex profiles, saves, and installed mods are read-only. Generated
work goes to Follower Forge's LocalAppData workspace unless another safe output path is supplied.

The builder validates:

- selected FormKeys and record types against the current catalogue;
- complete installed transitive master chains;
- HEDR 1.71, ESL flagging, form version, local FormID allocation, and record count;
- generated starting equipment, inventory, spells, perks, automatic/custom stat mode, all custom
  skill and primary-stat values, follower factions, relationship, and persistent placement;
- FaceGen and dependency paths;
- duplicate equipment and conflicting biped slots;
- archive structure and stale timestamps.

These are tool-level gates. They do not prove recruitment, equipment behavior, combat, dialogue,
FaceGen appearance, or stability in a running game. Test each generated follower on a disposable
save before sharing it.

## Building from source

Requires the .NET 10 SDK.

```powershell
.\Build-FollowerForge.ps1
.\Publish-FollowerForge.ps1 -Version 3.0.1
```

The publish script runs tests, creates self-contained Windows app and CLI executables, performs a
12-second boot check, and writes `dist\FollowerForge-3.0.1-win-x64.zip`.
