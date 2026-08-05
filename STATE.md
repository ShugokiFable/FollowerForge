# Follower Forge state

- Date: 2026-08-05
- Application/model: Claude Code / Opus 5
- Parent: `Follower Forge 3.0.0`
- Active: `Follower Forge 3.1.0`
- Note: `Follower Forge 3.0.1` was prepared and never released; it was folded into 3.1.0.
- Runtime target: Skyrim SSE 1.6.1170, Vortex-managed
- Runtime evidence:
  - 2.1.1 remains the last user-confirmed known-good release;
  - 3.0.0 was shipped for the user and testers to play;
  - the four scripted features (evolution, transformation, random spawn, enemy-to-ally) are
    unchanged since 3.0.0 and remain runtime-unconfirmed.
- Reported defects (3.0.0, from the user's own build and use):
  - selecting any book / misc item / potion / ingredient failed the build outright;
  - a High Poly Head follower from an extended-slider preset had a skin-tone neck seam;
  - a marriageable follower reported MARRIAGE_UNKNOWN and pointed at a CLI command;
  - "SOSVoicePack voices are lost within weird creature voices"; the wizard rated 6.5/10.
- Implementation:
  - inventory references accept every type the engine takes in an NPC container list;
  - the jslot's `actor.headTexture` is resolved and written to the NPC record's FTST;
  - voice-pack file verification moved to where the asset index actually exists, so the 17 SOS
    voices now report "voice files installed" instead of "not confirmed on disk";
  - the voice list is ordered vanilla -> voice pack -> mod -> no-follower-lines, with a filter
    that hides the 598 creature/unique voices by default;
  - one row template across the whole wizard: name, muted detail line, colour chip;
  - Vortex download bookkeeping is stripped from mod names in the UI (kept in the build report);
  - step 1 and step 2 are two-column and no longer overflow the window.
- Cleared by measurement (a ruled-out hypothesis, not a fix):
  - the FaceGen dirty swap was suspected of degrading high-poly sculpt/EFM geometry;
  - `FaceGenRoundTripTests` runs the real swap over every CharGen export on this machine and
    compares every vertex - bit-exact, High Poly Head export included.
- Current validation:
  - Release build: 0 warnings, 0 errors;
  - complete xUnit suite: 275 passed, 0 failed, 0 skipped;
  - self-contained publish and hidden boot check passed;
  - end-to-end CLI build carrying the exact five records that failed for the user: BUILD OK;
  - FTST read back from shipped plugin bytes: `00051648` -> `051648:Skyrim.esm`;
  - the wizard was driven and screenshotted against the real 2,910-plugin load order: tiering,
    chips, SOS "voice files installed", and both new page layouts confirmed on screen;
  - final ZIP SHA-256: `B509CB50976D6CFECC12D1E7B8392F729F891785C39BD596CE1618BEEFC0E530`.
- Rollback point: `Follower Forge 3.0.0` is untouched and matches the published v3.0.0 tag.
- Not yet done: 3.1.0 is committed locally but NOT pushed and has no GitHub release.
- SSEEdit/Creation Kit GUI: not launched.
