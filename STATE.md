# Follower Forge state

- Date: 2026-08-05
- Application/model: Claude Code / Opus 5
- Parent: `Follower Forge 3.0.0`
- Active: `Follower Forge 3.0.1`
- Runtime target: Skyrim SSE 1.6.1170, Vortex-managed
- Runtime evidence:
  - 2.1.1 remains the last user-confirmed known-good release;
  - 3.0.0's records-only features build correctly but were shipped for user/tester play;
  - 3.0.0's four scripted features (evolution, transformation, random spawn, enemy-to-ally)
    remain runtime-unconfirmed and are unchanged in 3.0.1.
- Reported defects (3.0.0, from the user's own build):
  - selecting any book / misc item / potion / ingredient failed the build with
    REFERENCE_WRONG_TYPE, so no follower carrying belongings could be produced;
  - a High Poly Head follower built from an extended-slider preset looked slightly off and
    had a skin-tone seam at the neck;
  - a marriageable follower reported MARRIAGE_UNKNOWN and told the user to run a CLI command.
- Implementation:
  - inventory references are validated against every type the engine accepts in an NPC
    container list (Armor, Weapon, Book, MiscItem, Ingestible, Ingredient);
  - the RaceMenu jslot's `actor.headTexture` is read, resolved against the live catalogue
    with an ESL-mask retry, and written to the NPC record's FTST — previously dropped, which
    left the follower on her race's default complexion under the preset's exported head;
  - an unresolvable head texture set warns (FACE_HEAD_TEXTURE_UNRESOLVED) instead of failing;
  - the wizard builds the voice-coverage library itself on first launch.
- Cleared by measurement (not a fix, a ruled-out hypothesis):
  - the FaceGen dirty swap was suspected of degrading high-poly sculpt/EFM geometry;
  - `FaceGenRoundTripTests` runs the real swap over every CharGen export on this machine and
    compares every vertex before and after — bit-exact, High Poly Head export included.
- Current validation:
  - Release build: 0 warnings, 0 errors;
  - complete xUnit suite: 257 passed, 0 failed, 0 skipped;
  - self-contained publish and hidden boot check passed;
  - end-to-end CLI build carrying the exact five records that failed for the user: BUILD OK;
  - FTST read back from the shipped plugin bytes: `00051648` -> `051648:Skyrim.esm`,
    matching the preset's `actor.headTexture` (absent before this change);
  - final ZIP SHA-256: `79EDAD6C70F9B515CA52808847AD82CA99FB6B218540180D4C26EA26409C2D90`.
- Rollback point: `Follower Forge 3.0.0` is untouched and matches the published v3.0.0 tag.
- Not yet done: 3.0.1 is committed locally but NOT pushed and has no GitHub release.
- SSEEdit/Creation Kit GUI: not launched.
