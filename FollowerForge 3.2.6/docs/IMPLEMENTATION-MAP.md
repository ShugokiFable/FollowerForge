# FollowerForge — Implementation Map (grounded in Phase 0 recon)

## Solution layout (`src\FollowerForge.sln`, all net8.0)

| Project | Responsibility |
|---|---|
| `FollowerForge.Domain` | Pure models: `FollowerProfile` (the deterministic build input), record/asset references, output strategies, validation results, manifests. No I/O. |
| `FollowerForge.ModManagers` | Vortex discovery: staging path, profiles, active-profile detection (runtime plugins.txt match), `vortex.deployment.json` streaming parser, load-order construction, read-only write-guard. |
| `FollowerForge.SkyrimRecords` | Mutagen: game environment, winning-override indexing of NPC_, RACE, CSTY, CLAS, VTYP, HDPT, OTFT, ARMO, ARMA, WEAP, SPEL, PERK, PACK, FACT, RELA, CELL, LCTN, KYWD, TXST, FLST; race evaluation; CSTY raw values + tag inference; voice classification; framework detection. |
| `FollowerForge.AssetIndex` | SQLite catalogue: record index tables + loose-file index (from deployment manifest) + BSA content index; winning-file resolution; CharGen/jslot discovery; BodySlide discovery. |
| `FollowerForge.FaceGen` | NiflySharp dirty-swap pipeline; validation; CK handoff report generation when unsafe. |
| `FollowerForge.BuildPipeline` | The follower compiler: deterministic FormID/EditorID allocation, ESP/ESPFE emission via Mutagen, atomic temp→publish, three output strategies, hub generation, manifest/credits emission, Vortex package. |
| `FollowerForge.Validation` | All build validators + `build-report.html`; Mutagen reopen check; ESPFE safety; FaceGen path/texture checks. |
| `FollowerForge.Cli` | `fforge` — `env`, `index`, `search`, `build`, `validate`, `package`, `batch`. Same engine as UI. |
| `FollowerForge.Ui` | Avalonia MVVM desktop app over the identical engine services. |
| `FollowerForge.Tests` | xUnit: pure-unit + environment-gated integration tests (skipped cleanly when the real modpack is absent). |

## Phase → feature mapping

| Phase | Deliverable | Verify |
|---|---|---|
| 0 | `fforge env` → environment/diagnostic JSON+console report (Vortex paths, profile, game, counts, CharGen, SOS, frameworks) | run against live install |
| 1 | `fforge index` (SQLite at `%LOCALAPPDATA%\FollowerForge\catalog.db`), `fforge search --type csty --text …` | live run; counts vs known plugins |
| 2 | `fforge build --profile x.json` → ESPFE + package (vanilla assets, no FaceGen) | ship-gate PASS + Mutagen reopen + validators |
| 3 | CharGen dirty-swap FaceGen + tint rewrite + NIF revalidation; CK handoff report fallback | reopen NIF, texture resolution vs asset index |
| 4 | pack-local reference mode (masters + zero copying) & shared hub ESM/ESPFE + hub-aware spokes | dependency-report.json correctness |
| 5 | custom-race eval, all CSTY exposure + clone-into-plugin, SOS enumeration/verification/attribution, body-system report, framework soft-integration | live records (DWKaPoTunRace, ForHonorBFCO, SOSVoices) |
| 6 | batch builds, deterministic-rebuild proof (byte-compare), build-report.html, credits.md, portable mode w/ permissions gate | rebuild twice → identical outputs |

## Key mechanisms

### Read-only enforcement
`WriteGuard` service: every file write in the app goes through it; it refuses (throws + logs) any path under game root, staging root, or any Vortex-managed folder. Workspace default: `Z:\Backup\!Skyrim AE\z1ClaudeWork\FollowerForge\output\` (configurable). Atomic builds: `workspace\.staging\<buildId>\` → validate → move to `workspace\builds\<FollowerName x.y.z>\`.

### Load-order construction
1. Read active profile `plugins.txt` (enabled = `*` lines) in file order.
2. Cross-check with `loadorder.txt` for full order.
3. Resolve plugin files from `Data\` (deployed, hardlinked ⇒ same bytes as staging).
4. Feed to Mutagen as an explicit load-order listing (not `GameEnvironment.Typical`, so the app works even if INI/registry detection would differ).

### Deterministic builds
- FormIDs: allocated from a stable ordering — NPC_ first at `0x800`, then fixed slot order (RELA, ACHR, cloned CSTY, hub records…), recorded in `rebuild-profile.json`; rebuilds reuse recorded allocations if inputs unchanged (hash of profile + resolved dependency FormKeys).
- EditorIDs: `FF_<SafeName>_<Kind>` slug, collision-checked against the record index.
- Master order: alphabetical within dependency-rank order as Mutagen requires load-order-consistent ordering; recorded in manifest.
- Timestamps in outputs: fixed (profile-defined) so zips/plugins byte-compare.

### Voice classification
- Fully follower-capable: VTYP referenced by vanilla follower dialogue (DialogueFollower quest voice list) — seeded from Skyrim.esm + USSEP winning overrides.
- Resource-integrated: SOSVoices.* VTYPs (fuz presence verified inside SOSVoices*.bsa).
- Non-follower-capable: VTYP with zero follow/trade/wait dialogue INFO coverage.
- Unknown: everything else; never hidden from the user.

### FaceGen dirty-swap (NiflySharp)
Allocate FormID → emit NPC → load CharGen NIF (20.2.0.7) → validate (race/gender/weight from profile vs jslot, BSDynamicTriShape present, head-part sanity) → rewrite FaceTint texture path in the tint-capable shape → preserve all unknown blocks (no optimizer pass) → write `facegeom\<Plugin>\<0000FFFF>.nif` + copy/convert tint DDS → reopen + validate → resolve every texture path against the asset index (loose + BSA). Unsafe ⇒ `ck-handoff-report.md` with plugin, EditorID, action, expected outputs.

## API-verification policy
Every Mutagen/NiflySharp/Avalonia symbol is proven by compilation before use in docs or reports; no API name in this map is final until `dotnet build` is green. (coding-discipline: the compiler is the token verifier.)
