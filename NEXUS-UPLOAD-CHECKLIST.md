# FollowerForge 3.7.0 — Nexus upload

1. File: `NEXUS-UPLOAD\FollowerForge-3.7.0-win-x64.zip`
2. Size: 99252778
3. SHA-256: `5B8FF57D365057C304FA1FFBE99F40C0A8332039DA8646EA4574FD4AFB1D0FA1`
4. Version: 3.7.0 | Category: Utilities
5. Changelog: `NEXUS-UPLOAD\NEXUS-CHANGELOG-3.7.0.txt` (BBCode, paste as-is)
6. GitHub release (for reference): https://github.com/ShugokiFable/FollowerForge/releases/tag/v3.7.0

The Nexus page is still on 3.6.0 (updated 2026-08-19). **3.6.1 was never uploaded there**, so
3.7.0 is the first Nexus release carrying the "you can unselect things" fixes as well as the
3.7.0 work. The changelog text covers both. The page description does not need changing.

Same-hash check before uploading, so the file that ships is the file that was tested:

```powershell
(Get-FileHash 'NEXUS-UPLOAD\FollowerForge-3.7.0-win-x64.zip' -Algorithm SHA256).Hash
# expect 5B8FF57D365057C304FA1FFBE99F40C0A8332039DA8646EA4574FD4AFB1D0FA1
```
