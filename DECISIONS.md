# FollowerForge decisions

## 2026-08-16 - 3.5.0

- Werewolf revert is a script bug, not a UI one. 3.4.0 is left untouched.
- Do not cast WerewolfChangeFX on followers. houseCARL + vanilla
  WerewolfTransformVisual.psc show it Wait(10) then SetRace(Werewolf) on any
  non-player target. That is why revert "worked" then they became wolves again.
- SetRace(WerewolfBeastRace) / SetRace() is the documented Actor API and is enough.
- After DelaySeconds, abort if GetCombatState()==0 so a short fight cannot
  transform them after the battle.
- OnUpdate + OnLoad are backups, not the primary path.

## 2026-08-16 - 3.4.0

- Keep 3.3.0 untouched. Three independent UX requests in one minor release.
- xVASynth: explicit path > FFORGE_XVASYNTH > saved settings > Steam library scan > default.
  The previous default-only path is why a Steam install off C: was invisible.
- Output: default stays LocalAppData\FollowerForge\workspace\builds\Name.
  A user-chosen folder publishes to Folder\Name so Vortex/MO2 can enable it as a mod.
  Staging always stays under LocalAppData. WriteGuard.Allow is only for that explicit dest.
  Game Data and My Games\Skyrim Special Edition remain rejected.
- Pronouns use named slots (subject/object/possessive). A her→him replace would break
  "her look" vs "kill her" and would also hit "published".
- Gear FormIDs use the catalogue key already stored (XXXXXX:Plugin.esp). That is the
  base ID plus plugin, which is what tells Makeshift Eyeglasses variants apart.
- Do not launch SSEEdit or Creation Kit GUI.

## 2026-07-28 - 2.1.3

- Preserve 2.1.2 unchanged and use a patch release for the custom-skill visibility repair.
- Treat the supplied screenshot as direct evidence of a UI layout failure, not a stat-data failure.
- Increase the skill value column from 82 to 150 pixels. This leaves room for the numeric text after
  Avalonia lays out both spinner buttons while retaining the three-category skill layout.
- Apply the same width as the control's `MinWidth` so parent-grid measurement cannot collapse it.
- Enforce a 140-pixel minimum through a focused regression test.
- Do not alter presets, allowed ranges, AutoCalcStats defaults, or DNAM serialization in this patch.
- Do not launch SSEEdit or Creation Kit GUI.

## 2026-07-28 - 2.1.2

- Preserve 2.1.1 unchanged because the user confirmed it works in Skyrim.
- Use patch version 2.1.2 for the optional skill/stat editor.
- Keep AutoCalcStats enabled by default. The class and level remain the easy recommended workflow.
- Custom mode is explicit and writes every DNAM skill key plus Health, Magicka, and Stamina.
- Write exact skill values with all DNAM skill offsets set to zero so no hidden adjustment changes what the editor shows.
- Keep the selected class even in custom mode for compatibility and classification; custom DNAM values control the serialized starting stats.
- Limit the friendly skill editor to Skyrim's normal 0-100 skill range while preserving the full unsigned 16-bit storage range for Health, Magicka, and Stamina.
- Treat presets as editable starting points, not locked classes.
- Keep pre-2.1.2 profiles automatic through the default-initialized Stats property.

## 2026-07-28

- Use patch version 2.1.1 because this completes and repairs the unreleased 2.1 equipment/appearance implementation without replacing its user workflow.
- Preserve `FollowerForge 2.1.0` unchanged for rollback.
- Keep equipment-first UI semantics. Skyrim still requires an OTFT on `NPC.DefaultOutfit` to decide initial worn armor, so the compiler creates a private OTFT from the user's selected ARMO records; it is an engine implementation detail, not a legacy outfit choice.
- Keep every equipped ARMO in NPC inventory so gear remains real, tradable equipment. Weapons remain inventory and are not placed in OTFT.
- Treat a RaceMenu export as NIF/DDS plus matching jslot record data. A missing/unreadable matching jslot is a build-stopping error.
- Prefer `formIdentifier` when resolving jslot head parts; it preserves dependencies omitted from RaceMenu's mods array, including light/overlay plugins.
- Convert RaceMenu tint ARGB alpha to Skyrim TINV as `alpha / 255`, preserving partial makeup and complexion strength.
- Resolve FaceGen textures against the catalogue first, then the deployed Data tree; reject traversal outside Data.
- Preserve installed matching face/body textures by default. Never retarget head-only skin support maps unless a complete matched head-and-body skin set is available.
- Repair only disposable FollowerForge SQLite cache files: close handles, move the DB/WAL/SHM aside with a `.broken-<timestamp>` suffix, then rebuild once. Never loop or hide non-cache failures.
- Treat FollowerForge NG and NPCMANAGER as read-only references. NPCMANAGER's portrait is a Fallout 4 OpenTK/NiflySharp renderer with a full material/skin pipeline; a Skyrim portrait preview is deferred rather than importing incompatible code.
- Do not launch SSEEdit or Creation Kit GUI.
