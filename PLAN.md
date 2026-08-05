# FollowerForge 2.1.3 plan

1. Preserve 2.1.2 unchanged and establish 2.1.3 as a full-copy successor.
2. Reproduce the supplied screenshot symptom in the custom Skills & stats editor.
3. Reserve enough horizontal space to show each numeric skill value and both spinner buttons.
4. Add a focused regression invariant for the minimum readable editor width.
5. Keep all 2.1.2 stat serialization, presets, and AutoCalcStats behavior unchanged.
6. Run the exact clean Release build and complete test suite.
7. Publish the self-contained app and CLI, run the boot check, and inspect the final archive.
8. Promote 2.1.3 only after all tool-level gates pass.
9. Leave visual confirmation in the user's desktop environment and in-game custom-stat behavior
   as explicit runtime checks.
