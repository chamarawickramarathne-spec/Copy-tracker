# SmartCopy — Implementation Plan

## Completed
- v1.0.15 — Paste latency optimization (Mod 1.0.15):
  - IntelligentRenamer: single directory enumeration per batch via DestinationSnapshot.
  - App: slim WH_KEYBOARD_LL hook; clipboard read moved off-hook to UI thread.
  - TransferEngine: 60ms throttled progress reports; final states always pass.
  - ExplorerFolderService: ShellWindows COM cached + background resolve.
  - Tests: 29/29 pass (3 new). Version bumped to 1.0.15 (csproj + iss).
- v1.0.14 — Cut+paste moves files instead of copying.
- v1.0.13 — Release-process validation bump.
- v1.0.12 — Updater switched to GitHub Releases API.
- v1.0.11 — Empty-repo fallback in SettingsService.
- v1.0.10 — User-selectable rename formats.
- v1.0.9 — Lightweight git tag detection fix.
- v1.0.8 — Count-based consecutive numbering per batch.
- v1.0.7 — Manual "Check for updates" header button.
- v1.0.6 — name_folder_number rename format.
- v1.0.5 — Update check on startup only.
- v1.0.4 — About tab repo box removed.
- v1.0.3 — Auto-update + version in header.
- v1.0.2 — Multi-file duplicate-destination fix.
- v1.0.1 — Paste interception COM/hook fixes.
- v1.0.0 — Initial release.

## Pending
- Full pipeline build (`tools\build.ps1`) for v1.0.15 → installer `SmartCopySetup_1.0.15.exe`.
- Git commit + annotated tag `v1.0.15` + GitHub Release with assets + HEAD verification.

## Notes / Deviations
- None. Naming output verified byte-identical to v1.0.14 by equivalence test.
