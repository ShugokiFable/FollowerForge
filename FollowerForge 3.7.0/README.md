# FollowerForge 3.7.0 - Windows tool (not a Skyrim mod)

FollowerForge is an out-of-game follower builder. It reads your Vortex or Mod Organizer 2 setup, then creates an installable follower mod (ESPFE plus assets) that you install afterward.

Do not install FollowerForge itself into Skyrim `Data`, Vortex, or MO2 as game content.

## Install the tool

1. Extract the ZIP anywhere outside Skyrim `Data`.
2. Run `FollowerForge.exe`.
3. Build your follower in the Studio workspace.
4. Install the generated follower package with Vortex or MO2.

The Windows x64 release is self-contained and does not require a separate .NET installation.

## New workspace in 3.6.0

- Start from the Studio dashboard and follow the next recommended action.
- Work in seven focused categories without nested tabs.
- Switch between Guided and Expert modes without changing follower data.
- Use Expert Deck for full installed-record browsing, filtering, inspection, and multi-selection.
- Press `Ctrl+K` for the command palette, `Ctrl+0` through `Ctrl+7` for workspace navigation, and `Ctrl+E` to toggle Guided/Expert.
- Choose Obsidian Gold, Arcane Amethyst, Nordic Frost, Forge Teal, or Light; theme, experience, and window size are stored separately from follower profiles.
- Keep the responsive dossier visible on wide windows or open it as a drawer on narrower ones.

## Vortex and MO2

FollowerForge auto-detects Vortex first unless MO2 is preferred. The left sidebar lets you switch managers before or during indexing.

MO2 users can click `MO2 setup...` to:

- browse to the exact `ModOrganizer.ini`;
- choose the profile to index;
- see the resolved base, mods, profiles, and overwrite paths;
- save that exact selection;
- return to automatic detection later.

FollowerForge 3.2.7 resolves MO2 `%BASE_DIR%`, environment-variable, absolute, and relative custom paths. An explicit profile is never silently replaced with another profile.

Click `Paths...` in the left sidebar to:

- point at an xVASynth install Steam did not put on `C:` (or that auto-detect missed);
- choose where built followers are written, including a Vortex or MO2 mods folder.

Empty path boxes keep automatic detection. Game `Data` and the Skyrim saves folder are still refused as output. Saved GUI settings live under `%LOCALAPPDATA%\FollowerForge`. FollowerForge does not modify MO2 INI/profile files, Vortex deployment data, Skyrim `Data`, or saves.

The Sex choice in Identity updates she/her or he/him throughout the workspace. Gear lists show each item's FormID (`XXXXXX:Plugin.esp`) so identically named variants can be told apart.

Detailed MO2 instructions are in `docs/MO2.md` in the source repository.

## Build a follower

1. Choose identity and follower options.
2. Select a RaceMenu Export Head face, race, voice, combat style, gear, spells/perks, and placement.
3. Press `Build follower`.
4. Install the generated mod package, enable its plugin, deploy, and launch Skyrim.

RaceMenu Export Head NIF/DDS files are required for a custom face. A slider-only preset without usable exported head geometry may not reproduce the face on an NPC.

## CLI and environment options

| Option | Purpose |
|---|---|
| GUI manager button | Prefer MO2 or Vortex |
| GUI `MO2 setup...` | Select and persist the exact MO2 instance/profile |
| GUI `Paths...` | Set xVASynth and the built-mod output folder |
| `FFORGE_XVASYNTH=D:\path\to\xVASynth` | Override xVASynth discovery |
| `--xvasynth DIR` | Same override for `voices` / synthesis |
| `--out DIR` | CLI build workspace (default `%LOCALAPPDATA%\FollowerForge\workspace`) |
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

## Sharing

FollowerForge does not grant redistribution rights for third-party assets. Check each asset author's permissions before uploading a generated follower.

Face workflow: [FaceForge](https://github.com/ShugokiFable/FaceForge) -> RaceMenu Export Head -> FollowerForge.

Repository: https://github.com/ShugokiFable/FollowerForge
