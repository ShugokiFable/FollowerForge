# Mod Organizer 2 setup

FollowerForge reads MO2 and Skyrim content without modifying either one. It does not need to run through MO2 or USVFS.

## Recommended GUI setup

1. Run `FollowerForge.exe`.
2. Click `Use Mod Organizer 2 instead` if FollowerForge starts with Vortex.
3. If the correct instance is not detected, click `MO2 setup...`.
4. Browse to the exact `ModOrganizer.ini` used by the MO2 instance.
5. Choose the profile and click `Use this profile and re-index`.

FollowerForge validates the game `Data` directory, MO2 mods/profiles paths, the selected profile, `modlist.txt`, and either `plugins.txt` or `loadorder.txt` before indexing. A missing overwrite directory is a warning, not a blocker.

The saved choice is stored only in:

`%LOCALAPPDATA%\FollowerForge\mo2-settings.json`

Use `MO2 setup...` -> `Return to automatic detection` to remove it.

## Custom MO2 paths

FollowerForge 3.2.7 supports:

- `Settings/base_directory`;
- `%BASE_DIR%` in mods, profiles, and overwrite paths;
- Windows environment variables;
- absolute paths;
- paths relative to MO2's resolved base directory.

## CLI

```text
cli\FollowerForge.Cli.exe env --mo2-instance "D:\Tools\MO2" --mo2-profile "Main"
cli\FollowerForge.Cli.exe index --mo2-instance "D:\Tools\MO2" --mo2-profile "Main"
```

An explicit profile must exist and contain `modlist.txt` plus `plugins.txt` or `loadorder.txt`. FollowerForge will not substitute another profile.

## Rollback

Reinstall FollowerForge 3.2.6. If desired, remove the 3.2.7 saved selection with the setup window or delete `mo2-settings.json` while FollowerForge is closed. No MO2 or Skyrim rollback is required because FollowerForge does not write to those locations.
