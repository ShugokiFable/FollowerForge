# FollowerForge 3.7.0 - Windows follower-building tool

FollowerForge is an out-of-game application that reads your active Vortex or Mod Organizer 2 setup and creates an installable Skyrim follower mod (ESPFE plus assets).

FollowerForge itself is **not** a Skyrim mod. Do not install the application into Skyrim `Data`, Vortex, or MO2 as game content.

## Download

Download the self-contained Windows x64 build from the [latest release](https://github.com/ShugokiFable/FollowerForge/releases/latest). A separate .NET installation is not required.

## Install and use

1. Extract the release ZIP anywhere outside Skyrim `Data`.
2. Run `FollowerForge.exe`.
3. Start from **Studio**. Follow the next recommended action through the seven categories.
4. Press **Build follower**.
5. Install the generated follower package with Vortex or MO2, enable its plugin, and deploy it.

RaceMenu Export Head NIF/DDS files are required for a custom face. A slider-only preset without usable exported head geometry may not reproduce the face on an NPC.

## New in 3.7.0

- **Combat transformation can turn her into a creature.** Dragons, wolves, trolls, spriggans - whatever your mods provide. Creature races were previously filtered out of the transform picker, which is the one place they belong.
- Choosing a legacy outfit **and** hand-picked armour no longer stops the build. The outfit decides what she starts in, the pieces stay in her inventory, and the build says so instead of failing.
- The build warns when a RaceMenu preset carries a body shape, because a plugin cannot store one. That shape belongs in a BodySlide or OBody preset.

## New in 3.6.1

- **Copy diagnostics** on the Review page and in the command palette. Home folders are tokenised, so a pasted report does not publish your Windows account name.
- **Clear** on every optional picker, and **Clear selection** in the Expert Deck. Nothing is stuck any more.

## New in 3.6.0

- **Studio** dashboard with category readiness and a next recommended action.
- Seven **Focus** categories instead of the old tab wall.
- **Expert Deck** for searching, inspecting, and selecting installed records (FormID + EditorID).
- Guided / Expert mode (`Ctrl+E`). Expert opens the primary catalogue; it does not hide fields.
- Command palette (`Ctrl+K`) for navigation, Build, Paths, MO2 setup, and manager switch.
- Five themes. Theme, experience, and window size are stored separately from follower profiles.
- Werewolf revert, Paths, pronouns, and gear FormIDs from 3.4.0 / 3.5.0 are still in.

Keyboard: `Ctrl+0`–`Ctrl+7` jumps categories. Escape closes an open overlay only.

## Vortex and Mod Organizer 2

FollowerForge auto-detects Vortex first unless MO2 is preferred. The manager control in the left sidebar can switch between them before or during indexing.

MO2 users can click **MO2 setup...** to:

- browse to the exact `ModOrganizer.ini`;
- select the exact profile to index;
- inspect the resolved base, mods, profiles, and overwrite paths;
- save that selection for future runs;
- return to automatic detection later.

Click **Paths...** to set the xVASynth folder and where built followers are saved. Sex on Identity updates she/her or he/him. Gear lists show FormIDs so identically named items can be told apart.

Saved GUI settings live under `%LOCALAPPDATA%\FollowerForge`. FollowerForge does not modify MO2 profile/INI files, Vortex deployment data, Skyrim `Data`, or saves.

See [the detailed MO2 guide](FollowerForge%203.7.0/docs/MO2.md) for setup and troubleshooting.

## CLI and environment options

| Option | Purpose |
|---|---|
| GUI manager button | Prefer MO2 or Vortex |
| GUI **MO2 setup...** | Select and persist the exact MO2 instance/profile |
| GUI **Paths...** | Set xVASynth and the built-mod output folder |
| `FFORGE_XVASYNTH` / `--xvasynth` | Override xVASynth discovery |
| `FFORGE_PREFER_MO2=1` | Try MO2 before Vortex |
| `FFORGE_MO2_INSTANCE=D:\path\to\instance` | Use that MO2 instance folder |
| `--mo2-instance DIR` | Use an exact MO2 instance in the CLI |
| `--mo2-profile NAME` | Use an exact MO2 profile in the CLI |
| `cli\FollowerForge.Cli.exe env` | Print the detected environment |
| `cli\FollowerForge.Cli.exe index` | Rebuild the catalogue |

Do not point FollowerForge at a houseCARL Vortex shim unless you intentionally override the safety gate.

## Requirements

- Windows 10 or 11
- Skyrim Special Edition or Anniversary Edition
- Vortex or Mod Organizer 2
- RaceMenu for custom faces

Optional integrations include xVASynth, dialogue overhauls, High Poly Head, and FSMP when the selected follower assets require them.

## Sharing generated followers

FollowerForge does not grant redistribution rights for third-party assets. Check every asset author's permissions before uploading a generated follower.

Recommended face workflow: [FaceForge](https://github.com/ShugokiFable/FaceForge) -> RaceMenu Export Head -> FollowerForge.

## Version history

See [CHANGELOG.txt](CHANGELOG.txt) for the complete history and [the 3.7.0 Nexus changelog](FollowerForge%203.7.0/NEXUS-CHANGELOG-3.7.0.txt) for the short user-facing update notes.
