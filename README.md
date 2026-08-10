# FollowerForge 3.2.7 - Windows follower-building tool

FollowerForge is an out-of-game application that reads your active Vortex or Mod Organizer 2 setup and creates an installable Skyrim follower mod (ESPFE plus assets).

FollowerForge itself is **not** a Skyrim mod. Do not install the application into Skyrim `Data`, Vortex, or MO2 as game content.

## Download

Download the self-contained Windows x64 build from the [FollowerForge 3.2.7 release](https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.2.7). A separate .NET installation is not required.

## Install and use

1. Extract the release ZIP anywhere outside Skyrim `Data`.
2. Run `FollowerForge.exe`.
3. Select your identity, RaceMenu Export Head, race, voice, combat style, equipment, spells, perks, and placement.
4. Press **Build follower**.
5. Install the generated follower package with Vortex or MO2, enable its plugin, and deploy it.

RaceMenu Export Head NIF/DDS files are required for a custom face. A slider-only preset without usable exported head geometry may not reproduce the face on an NPC.

## Vortex and Mod Organizer 2

FollowerForge auto-detects Vortex first unless MO2 is preferred. The manager control in the left sidebar can switch between them before or during indexing.

MO2 users can click **MO2 setup...** to:

- browse to the exact `ModOrganizer.ini`;
- select the exact profile to index;
- inspect the resolved base, mods, profiles, and overwrite paths;
- save that selection for future runs;
- return to automatic detection later.

Version 3.2.7 supports portable and customized MO2 layouts, including relative paths, environment variables, and case-insensitive `%BASE_DIR%` expansion. An explicitly selected profile is never silently replaced with another profile.

Saved GUI settings live under `%LOCALAPPDATA%\FollowerForge`. FollowerForge does not modify MO2 profile/INI files, Vortex deployment data, Skyrim `Data`, or saves.

See [the detailed MO2 guide](FollowerForge%203.2.7/docs/MO2.md) for setup and troubleshooting.

## CLI and environment options

| Option | Purpose |
|---|---|
| GUI manager button | Prefer MO2 or Vortex |
| GUI **MO2 setup...** | Select and persist the exact MO2 instance/profile |
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

See [CHANGELOG.txt](CHANGELOG.txt) for the complete history and [the 3.2.7 Nexus changelog](FollowerForge%203.2.7/NEXUS-CHANGELOG-3.2.7.txt) for the short user-facing update notes.
