# FollowerForge 3.6.0 plan

1. [x] Inspect 3.5.0 UI and preserve user-owned root changes.
2. [x] Approve Studio → Focus Cards → Expert Deck architecture.
3. [x] Snapshot 3.5.0 → 3.6.0; leave 3.5.0 untouched.
4. [x] Review and approve the written UI design specification.
5. [x] Produce an implementation plan with test-first milestones.
6. [x] Implement theme tokens, shell/navigation, cards, dedicated pickers, and Expert Deck.
6b. [x] Fix Expert Deck single-choice crash (mode-aware selection sync) + 4 regression tests. (Kimi)
6c. [x] UI polish pass: density/clarity rework + live-theme chips and setup windows (theme-leak fix) + ThemeCoverageTests; 454 tests. (Kimi)
6d. [x] UI polish pass 3: per-theme distinct status hues + tinted pill chips (user: "still yellow"); 456 tests. (Kimi)
6e. [x] UI polish pass 4: overlay opacity, Light-theme variant fix, sidebar pills, auto-sizing lists/cards; headless screenshot harness. (Kimi)
7. [x] Run exact Release tests, UI smoke checks, encoding guard, and source-path scan.
8. [x] Build the self-contained Windows release, validate archive/hash, and update docs.
9. [ ] Push and publish the public GitHub release only after explicit publication authorization.

# FollowerForge 3.5.0 plan

1. [x] Prove werewolf non-revert from vanilla WerewolfTransformVisual + our Wait race.
2. [x] Snapshot 3.4.0 → 3.5.0.
3. [x] Failing tests, then stop attaching WerewolfChangeFX, then rewrite FF_Transform.
4. [x] Recompile PEX. 388 tests pass.
5. [x] Publish zip built (boot check passed) and attached to the GitHub v3.5.0 release.

# FollowerForge 3.4.0 plan

1. [x] Prove the three user requests are solvable in 3.3.0 before writing.
2. [x] Snapshot 3.3.0 to 3.4.0; leave 3.3.0 untouched.
3. [x] Persist xVASynth + output paths; scan Steam libraries; allow explicit Vortex/MO2 dest.
4. [x] Gender-matched wizard pronouns from the existing Sex box.
5. [x] Show gear FormIDs (and search them) on armor/weapons/ammo/belongings/skins.
6. [x] Tests for settings, locator, pronouns, FormID display, write-guard Allow, publishRoot.
7. [x] 386 Release tests pass.
8. [x] `Publish-FollowerForge.ps1` zip built (shipped inside the v3.5.0 GitHub release). Nexus upload stays manual.
