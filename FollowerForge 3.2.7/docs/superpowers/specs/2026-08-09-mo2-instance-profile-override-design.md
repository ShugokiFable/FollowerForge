# FollowerForge 3.2.7: MO2 instance and profile override design

Date: 2026-08-09  
Parent: FollowerForge 3.2.6  
Status: implemented and tool-validated in FollowerForge 3.2.7

## Problem and verified root cause

FollowerForge 3.2.6 can accept an explicit MO2 instance from the CLI or the
`FFORGE_MO2_INSTANCE` environment variable, but the Windows GUI exposes only a
Vortex/MO2 preference toggle. A user whose instance is not found by the known
folder probes has no GUI recovery path.

The profile is read from `ModOrganizer.ini` `General/selected_profile`. If that
folder is missing, 3.2.6 silently chooses the first directory under the profiles
root. This can index the wrong profile and makes failures difficult to diagnose.

MO2 permits portable and global instances, custom path roots, and `%BASE_DIR%`
in its Paths settings. The current parser expands Windows environment variables
but does not expand `%BASE_DIR%` or anchor relative paths to MO2's base directory.
That can turn a valid MO2 path into a nonexistent path under FollowerForge's
working directory.

## Goals

1. Let an MO2 user select the exact `ModOrganizer.ini` used by their instance.
2. Populate a profile dropdown from that instance's resolved profiles directory.
3. Validate the instance, resolved paths, and profile before indexing begins.
4. Persist the manual selection across launches without modifying MO2.
5. Let the user discard the override and return to automatic discovery.
6. Correctly resolve MO2 `%BASE_DIR%`, environment-variable, absolute, and
   relative path forms.
7. Preserve existing Vortex behavior and existing CLI/environment overrides.

## Non-goals

- Do not launch FollowerForge through MO2 or depend on USVFS.
- Do not modify `ModOrganizer.ini`, profiles, modlist, plugins, load order, mods,
  overwrite, Skyrim Data, or saves.
- Do not add a general-purpose settings subsystem or refactor unrelated indexing.
- Do not guess a different profile after the user explicitly selected one.

## Approaches considered

### A. Add more automatic folder guesses

Rejected. Portable/global instances and custom paths are intentionally flexible;
another folder-name list will fail again and cannot choose among multiple profiles.

### B. Document the existing environment variable

Rejected as the only fix. `FFORGE_MO2_INSTANCE` is useful for automation but is
not reasonable recovery UX for ordinary Nexus users and still lacks a profile
override.

### C. Persisted GUI instance and profile selection

Selected. It gives deterministic behavior, keeps automatic discovery as the
default, and provides enough evidence in the UI to diagnose an invalid setup.

## User experience

The existing manager-switch button remains unchanged. A small `MO2 setup...`
button is added below it. Clicking it opens a dedicated dialog.

The dialog contains:

- `ModOrganizer.ini` path textbox;
- `Browse...` file picker filtered to `ModOrganizer.ini`;
- resolved instance and base-directory summary;
- profile dropdown populated from the resolved profiles directory;
- validation/status text showing the exact missing or invalid path;
- `Use this profile and re-index` primary action;
- `Return to automatic detection` action;
- `Cancel`.

When a user switches to MO2 and discovery fails, FollowerForge opens this dialog
instead of merely telling them to set an environment variable.

Saving a valid selection cancels the current catalogue generation, persists the
manual settings, marks MO2 preferred, clears manager-dependent in-memory data,
and starts one serialized re-index against the selected instance/profile.

Vortex users see no setup interruption and require no new configuration.

## Components and data flow

### `Mo2InstanceInspector`

Reads one `ModOrganizer.ini` and returns a typed inspection result containing:

- canonical instance root;
- canonical game root;
- canonical MO2 base directory;
- canonical mods, profiles, and overwrite directories;
- available profile names;
- INI-selected profile;
- validation errors and warnings.

Path resolution order:

1. Read `Settings/base_directory`; default to the instance root when absent.
2. Expand Windows environment variables in the base directory.
3. Resolve a relative base directory against the instance root.
4. For mods/profiles/overwrite, replace `%BASE_DIR%` case-insensitively.
5. Expand Windows environment variables.
6. Resolve remaining relative paths against the canonical base directory.
7. Canonicalize with `Path.GetFullPath` and validate expected directories.

### `Mo2UserSettings`

Stores only FollowerForge-owned preferences under
`%LOCALAPPDATA%\FollowerForge\mo2-settings.json`:

```json
{
  "schemaVersion": 1,
  "instanceRoot": "D:\\Modding\\MO2-SkyrimSE",
  "profileName": "Main"
}
```

Writes are atomic through a temporary file in the same FollowerForge settings
directory. Invalid or unknown schemas are ignored with a warning. No MO2-owned
file is written.

### `Mo2Discovery`

Adds an optional `profileOverride`. It consumes the inspector's resolved paths.
Selection precedence is:

1. explicit CLI arguments;
2. FollowerForge environment overrides;
3. saved GUI override;
4. automatic instance discovery and the INI-selected profile.

An explicit/manual profile must exist and must not fall back. Automatic mode may
retain the existing first-profile fallback, but must surface a warning.

### `WizardWindow` and `Mo2SetupWindow`

The wizard owns cancellation/reload orchestration. The setup dialog owns only
path/profile input and validation. It returns a validated selection; it does not
build the catalogue itself.

## Validation rules and errors

A manual selection is accepted only when:

- the chosen file is named `ModOrganizer.ini` and exists;
- the resolved game root contains `Data`;
- the resolved mods and profiles directories exist;
- the chosen profile directory exists;
- the profile has `plugins.txt` or `loadorder.txt`;
- the profile has `modlist.txt`.

Overwrite may be missing and remains a warning, matching existing behavior.

Manual-mode failures name the exact bad field/path and leave the setup dialog
open. They do not fall back to Vortex, another MO2 instance, or another profile.

## Cache and indexing behavior

The catalogue freshness key already includes manager/profile information. The
3.2.7 settings save additionally invalidates the location cache and cancels the
current generation before starting a new load. The existing catalogue lock and
generation counter remain the single concurrency mechanism.

## Tests

Add failing tests before implementation for:

1. nonstandard instance selected manually;
2. `%BASE_DIR%/mods`, `%BASE_DIR%/profiles`, and `%BASE_DIR%/overwrite`;
3. relative paths anchored to the MO2 base directory;
4. explicit profile override beating `selected_profile`;
5. explicit missing profile returning an actionable error without fallback;
6. automatic mode retaining its warning/fallback behavior;
7. persisted settings round-trip and corrupt-settings recovery;
8. profile preflight rejecting missing load-order/modlist files;
9. Vortex discovery behavior remaining unchanged;
10. switching settings during indexing cancelling the stale generation.

Run the full xUnit suite, clean Release build, self-contained Windows publish,
GUI/CLI boot checks, ZIP inventory/hash validation, and an MO2 fixture index.
A real MO2 user remains the final runtime confirmation for virtualized asset
priority and large-profile performance.

## Release impact

This is a patch release. No follower plugin format, profile JSON schema, FormID,
asset-copy behavior, Vortex discovery, or Skyrim runtime behavior changes.

Rollback is reinstalling FollowerForge 3.2.6; FollowerForge-owned manual MO2
settings may be removed with `Return to automatic detection` or by deleting the
single `mo2-settings.json` file while FollowerForge is closed.
