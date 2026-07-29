<div align="center">

# ⚒️ Follower Forge

**Make a custom Skyrim SE/AE follower without ever opening the Creation Kit.**

Pick her face, voice, class, gear, and where in Skyrim she waits — all from your own installed
mods, chosen by name. Follower Forge writes a real, conflict-safe ESPFE plugin and an installable
mod folder.

[![Release](https://img.shields.io/github/v/release/ShugokiFable/FollowerForge)](https://github.com/ShugokiFable/FollowerForge/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2064--bit-lightgrey)]()
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

</div>

---

## 3.0 — the follower suite

2.0 could make a follower. **3.0 makes a character.**

| | 2.0 | **3.0** |
|---|---|---|
| Dialogue | voice type only | **custom spoken lines** with lip sync, 13 triggers, reactions to place and time |
| Inherited dialogue | — | **scans your load order** and reports what each voice already says |
| Alternate forms | — | **vampire** (scriptless), **werewolf** and mod transformations |
| Non-humanoid | — | **creature followers** — dragons, mechs, beasts |
| How you meet her | placed and recruited | that, or **enemy to ally** — find her, beat her, summon her |
| Behaviour | — | **AI routine**, sleep schedule, relationship rank, NPC-to-NPC relationships |
| Growth | — | optional **evolution** — she starts timid and grows into the job |
| Inventory | armour, weapons | plus **books and belongings** for lore |
| Marriage | added a faction | **tells you whether it will actually work** on your setup |
| Testing | — | **`test-in-game.txt`** with the console commands for her exact features |

Everything is chosen from records you already have. Follower Forge never invents game data and
never edits your installed plugins.

---

## Getting started

1. Download the latest release, unzip anywhere, run **Follower Forge.exe**.
   Self-contained — no .NET install needed.
2. First launch reads your active Vortex deployment (about a minute).
3. Walk the seven steps and press **Build follower**.
4. Install the produced ZIP with Vortex, enable the plugin, deploy.

Needs a Vortex-managed Skyrim Special Edition / Anniversary Edition install.

There is a CLI too — `cli\FollowerForge.Cli.exe` — running the same engine:

```
fforge env
fforge locations --scan
fforge voice-coverage --scan
fforge build --profile follower.json --zip
```

---

## What it actually does

**Spawn points from your own mods.** 3,300+ places harvested from mods that already put an NPC
there, so the coordinates are ones a real author shipped. Pick "The Bannered Mare", not numbers.

**Faces from RaceMenu.** Sculpt export plus the matching `.jslot`. The look step says up front
whether a face will build, instead of failing at the end.

**Custom voiced dialogue.** Write her lines; [xVASynth](https://www.nexusmods.com/skyrimspecialedition/mods/44184)
speaks them and they are packed to `.fuz` with lip sync. Any line can be limited to a kind of place
(45 location types — caves, inns, cities, mines) or a time of day. Without xVASynth you can ship
subtitles, but you have to opt into that — the build refuses to quietly hand you a mute follower.

**What she already says.** One scan reads every installed plugin for dialogue keyed to a voice
type and reports the total. On a load order with RDO and OOD, `FemaleEvenToned` inherits ~4,600
lines before you write a word. This is also how RDO support works — nothing is added to the
plugin; she is covered the moment she uses a voice RDO handles.

**Marriage that tells the truth.** Vanilla allows eight voice types. Mods add more — with RDO
installed, 48. Follower Forge reads your load order and says which case you are in, including
"she can only marry because of RDO, so anyone who installs her needs it too".

**Vampires, werewolves, creatures.** Vampirism is a race swap plus two keywords — exactly what a
vanilla vampire NPC has, no script. Werewolves are a transformation instead, because Skyrim has
one beast race rather than one per race. Creature races (no head data) are available behind an
explicit tick.

**Enemy to ally.** Optionally she starts as an enemy at one of several places. Beat her, loot the
spell tome she carries, read it, and the spell summons her as a follower.

---

## Experimental features — please read

Four features add a **Papyrus script** to your follower:

- evolution (she grows with combat)
- transformation (werewolf / custom)
- random spawn points
- enemy to ally

They compile cleanly and their records are verified, **but they have not yet been confirmed
running in game.** Scripts persist in save files in a way plain records do not, so test them on a
save you can throw away. Every other feature is records only.

Scripted followers can also behave oddly if you import them into a follower framework (NFF, EFF).
Ordinary followers are unaffected.

Every build writes `test-in-game.txt` with the console commands for that follower's exact
features, and names the symptom that means one did not run.

---

## What it will not do

- Edit your installed plugins, your saves, or your game folder. All of that is read-only.
- Copy another author's assets without an explicit written permission declaration in the profile.
- Launch the Creation Kit or xEdit.
- Make a child follower, or use a child voice.

---

## Sharing what you make

Every build reports its dependencies and says plainly whether she is safe to hand to anyone
(`needs nothing but the base game`) or which mods a downloader also needs — including mods
required only because of her voice or her race.

---

## Building from source

```
git clone https://github.com/ShugokiFable/FollowerForge.git
cd "FollowerForge/Follower Forge 3.0.0"
.\Publish-FollowerForge.ps1
```

.NET 10 SDK. The publish script runs the tests, builds a self-contained single-file exe,
boot-checks it, and writes `dist\FollowerForge-3.0.0-win-x64.zip`.

### Solution layout

| Project | Responsibility |
|---|---|
| `Domain` | Profiles, records, assets, hubs, manifests, validation types |
| `ModManagers` | Vortex discovery, deployment manifest, write-guard |
| `SkyrimRecords` | Mutagen indexing, follower/dialogue/transform compilers, cell placement |
| `AssetIndex` | SQLite catalogue, loose + BSA, CharGen, xVASynth voice synthesis |
| `FaceGen` | NiflySharp head dirty-swap |
| `BuildPipeline` | Atomic builder, location library, voice coverage, hubs, packaging |
| `Validation` | In-process ESP header ship-gate |
| `Cli` | `fforge` |
| `Ui` | Avalonia 7-step wizard |
| `Tests` | xUnit — 249 tests |

---

## License

MIT for Follower Forge source and binaries you build from it.

Follower Forge never redistributes Bethesda masters or third-party mod assets. Portable /
own-hub copies require an explicit redistribution declaration from **you**, and the tool
records that you made the claim — it cannot verify permission with the original authors.

---

## Credits

Built with [Mutagen](https://github.com/Mutagen-Modding/Mutagen) and
[NiflySharp](https://github.com/ousnius/NiflySharp).
Voice synthesis by [xVASynth](https://www.nexusmods.com/skyrimspecialedition/mods/44184).

---

<div align="center">

**[⬇ Download Follower Forge 3.0.0](https://github.com/ShugokiFable/FollowerForge/releases/latest)**

</div>
