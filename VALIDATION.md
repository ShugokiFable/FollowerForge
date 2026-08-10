# VALIDATION — FollowerForge 3.2.8

## Commands run

```text
dotnet test Tests\FollowerForge.Tests.csproj
  → 353 passed, 0 failed

Publish-FollowerForge.ps1 -Version 3.2.8 -SkipTests
  → PUBLISH SUCCEEDED; boot window stayed up
```

## Targeted regression

| Test | Intent | Result |
|---|---|---|
| PluginIsInstalled_FindsModOnlyMasterOutsideSteamData | MO2-only master not in Steam Data | PASS |
| EnsurePluginReadRoot_LinksModOnlyMasterIntoPluginView | hardlink view exposes SOSVoices.esm | PASS |
| PluginWriter_ExpandMasterChain_SucceedsFromMo2PluginView | Steam root fails; view root expands masters | PASS |
| PluginReadRoot_Property_IsPluginDataPathForMo2 | property semantics Vortex vs MO2 | PASS |

## Package

See STATE.md for SHA-256 after hash step in this session.
