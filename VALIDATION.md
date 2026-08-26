# VALIDATION — FollowerForge 3.7.0

## Snapshot gate

```text
Parent: FollowerForge 3.6.1 (released, untouched)
Active: FollowerForge 3.7.0
Robocopy: 238 copied, 0 failed, 0 mismatched (bin/obj/dist excluded — disposable, regenerated)
```

## Commands and results

```text
dotnet build src/FollowerForge.slnx -c Release      -> PASS, 0 warnings, 0 errors
dotnet test  src/FollowerForge.slnx -c Release      -> 489 passed, 0 failed (478 + 11 new)
Publish-FollowerForge.ps1 -Version 3.7.0            -> NOT RUN (release not authorised yet)
```

## Creature transformation gate

Fixtures prove the classifier; they do not prove the picker has anything to show. Read from
the live catalogue on this machine (2,920 enabled plugins, 978 RACE records):

```text
FollowerForge.Cli.exe races                 -> 978 race records → 191 usable
FollowerForge.Cli.exe races --creatures     -> 978 race records → 944 offered (creatures included)
```

753 beast forms the feature was written for had never been reachable. Sampled rows confirm the
new entries are real forms, not junk: BabyDragonRace (×5, different plugins), Bear, Cave Bear,
Blue Spriggan, Burnt inside out Vampire Lord.

The ×5 duplicate names are why creature rows now carry FormID + EditorID.

## End-to-end build gate — the exact reported failure

Profile: legacy outfit `01DC10:Skyrim.esm` + `EquippedArmor` and `InventoryItems` of
`012E49`/`012E4B:Skyrim.esm` + `Transformation.Kind = Custom` with
`BeastRace = 0CDD84:Skyrim.esm` (WerewolfBeastRace — a Creature-class race).

```text
[INF] Transformation: Custom, beast race 0CDD84:Skyrim.esm, reverts True
[INF] Compiled follower Beast Repro: NPC 000800, ACHR 000802, masters []
[INF] Wrote plugin FF_BeastRepro.esp (1060 bytes, HEDR numRecords=11)
[INF] Wrote Scripts/FF_Transform.pex
  [Warning] OUTFIT_OVERRIDES_ARMOR: She will start in 'FarmClothesOutfit01', not the 2 armor
            piece(s) you picked ...
  [Info]    SHIP_GATE_PASS: HEDR=1.71 numRec=11 formVersion=44 (5 records) ESL=True
BUILD OK (published)
```

Before this snapshot the same profile produced `STARTING_ARMOR_NOT_WRITTEN` once per piece and
stopped the build. No errors now, and exactly one warning for the situation (the old vague
`LEGACY_OUTFIT_WITH_EQUIPMENT` was removed rather than left to duplicate it).

Bytes, not logs — the published plugin's VMAD read back directly:

```text
FF_Transform         present=True
BeastRace            present=True     -> 84 dd 0c 00  (0x000CDD84, master index 0)
RevertOutOfCombat    present=True
DelaySeconds         present=True
```

## Preset body-shape gate

The jslot block names were read from two real presets in the game's CharGen folder, never
assumed:

```text
Woo-Female-Imperial_Silvia.jslot   bodyMorphs: 119 entries, 86 shaped (keyed "OBody")
READ_ALL_SLIDERS_TEST.jslot        bodyMorphs: 135 entries
```

Top-level keys present in both: actor, bodyMorphs, faceTextures, headParts, modNames, mods,
morphs, tintInfo, transforms, version. The parser consumes mods, headParts, actor, morphs and
tintInfo — so bodyMorphs is genuinely unread, and genuinely lost.

`transforms` was deliberately NOT warned about: in the reference preset it holds SHIELD and
WEAPON node placement from a weapon-position mod, which has nothing to do with the follower.

## Runtime status

```text
tool-validated: desktop build, full suite, shipped-CLI end-to-end build, plugin bytes read back
NOT confirmed : a creature transform surviving a real fight in game. SetRace() on a follower is
                engine behaviour this build cannot exercise.
SSEEdit/CK    : not launched
```

## Deliberately unresolved

- RaceMenu OVERLAYS cannot be detected here. Neither reference preset carries an overlay block,
  so the key name is unconfirmed and was not guessed. The body-shape warning names overlays as
  a known limitation without claiming to detect them.
- MO2 `modlist.txt` priority direction still UNVERIFIED (`PluginLists.cs:69`, open since 3.2.5).
- The publish ZIP is not byte-reproducible; publishing from CI on tag would settle it.
- 3.7.0 is not published, pushed or tagged. Nexus still serves 3.6.0; GitHub still serves 3.6.1.

---

# VALIDATION — FollowerForge 3.6.1 (previous)

## Snapshot gate

```text
Parent: FollowerForge 3.6.0 (released, untouched)
Active: FollowerForge 3.6.1
Robocopy: 235 copied, 0 failed, 0 mismatched (bin/obj/dist excluded — disposable, regenerated)
```

## Commands and results

```text
dotnet build src/FollowerForge.slnx -c Release      -> PASS, 0 warnings, 0 errors
dotnet test  src/FollowerForge.slnx -c Release      -> 478 passed, 0 failed
Publish-FollowerForge.ps1 -Version 3.6.1            -> PASS, boot check "window stayed up"
```

Shipped artifact: `FollowerForge-3.6.1-win-x64.zip`, 99,249,351 bytes,
SHA-256 `EB48F7867CA19167E2A6720BECED21DF6B32DC5D58AF8860B3AF81F8F60695C6`.
The GitHub release asset digest matched this exactly, unlike 3.6.0 where they diverged.
Exe stamps `3.6.1+f009a21156e5e3ca6cb5764703acaf338f6caee2`.
