# SmartCopy — Application Memory (Modification Log)

This file is the modification memory for the SmartCopy application. Every change bumps a mod number and adds a new entry. Versioning starts at 1.0.0.

## Mod 1.0.7 — Manual "Check for updates" button next to version (v1.0.7)

**Date:** 2026-08-09

### What was changed
- **Header update button**: a small "Check for updates" ghost button now sits next to the version number in the window header (next to `SmartCopy 1.0.7`). Clicking it checks `git ls-remote --tags` against the configured repo (`SettingsService.UpdateRepository`), and if a newer release exists it downloads `SmartCopy.exe` and restarts to apply it. Button text reflects state (`Checking...` / `Up to date` / `Downloading {version}...` / `Restarting...` / `Check failed`) and resets after 3s. Works regardless of the "Automatically download and apply new versions" setting (manual action). Auto-update on startup is unchanged.

### Verified
- 11/11 xUnit tests pass. Full pipeline via `tools/build.ps1`: build clean (0 warnings), publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.7.exe`). `medial_support.txt` regenerated.
- **Released via git**: commit + tag `v1.0.7` pushed + GitHub Release `v1.0.7` created with `publish/SmartCopy.exe` asset.

## Mod 1.0.6 — Rename format: name_folder_number (v1.0.6)

**Date:** 2026-08-09

### What was changed
- **New auto-rename format** (`IntelligentRenamer.GenerateSmartFilePath`): both schemes (FolderBased and Sequential) now produce `<original file name>_<folder name>_<number><ext>` — e.g. dropping `photo.jpg` into the `Vacation` folder yields `photo_Vacation_3.jpg` (was `Vacation_3.jpg` / `image_3.jpg`). The `<original file name>` is the source stem, `<folder name>` the destination folder name, `<number>` continues from the count of existing files with the same extension in the destination (collision-safe as before).
- **Settings**: the `RenameScheme` setting/combo remains for compatibility, but both options now produce the same combined format. Combo example texts updated (`Folder-based — photo_vacation_3.jpg`, `Sequential — photo_vacation_3.jpg`). The `_scheme` field was removed from `IntelligentRenamer` (constructor parameter kept, intentionally unused).

### Verified
- 11/11 xUnit tests pass (3 renamer tests updated to the new format). Full pipeline via `tools/build.ps1`: build clean (0 warnings), publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.6.exe`). `medial_support.txt` regenerated.

## Mod 1.0.5 — Update on startup only; About update section removed (v1.0.5)

**Date:** 2026-08-09

### What was changed
- Update check now runs **once at every app start** (≈5s after launch) instead of startup + every 6 hours. `App.StartAutoUpdateLoop` replaced by `App.StartUpdateCheck` (one-shot `DispatcherTimer`); the periodic 6h timer and `_updateTimer` field removed. Download/apply + idle-deferral logic unchanged.
- **About tab**: the whole "Updates (Git-based)" card is gone (description, manual "Check for updates" button, `updateBar`, `txtUpdateStatus`). About now shows just logo, name, version, and a one-line description that mentions updates install automatically. `MainWindow.OnCheckUpdate` handler removed; unused row definitions collapsed.

### Verified
- 11/11 xUnit tests pass. Full pipeline via `tools/build.ps1`: build clean, publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.5.exe`).
- **Released via git**: tag `v1.0.5` pushed + GitHub Release `v1.0.5` created with `publish/SmartCopy.exe` asset so installed 1.0.4 copies update on their next start.

## Mod 1.0.4 — About tab: repo box removed (v1.0.4)

**Date:** 2026-08-09

### What was changed
- The About tab no longer asks the user to paste an `owner/repo` link. The update card now just explains that updates run automatically (startup + every 6h) and keeps the manual "Check for updates" button. `OnCheckUpdate` reads the repository from `SettingsService.UpdateRepository` (default `chamarawickramarathne-spec/Copy-tracker`) instead of the removed `txtRepo` textbox; `LoadSettings`/`OnSaveSettings` updated accordingly.

### Verified
- 11/11 xUnit tests pass. Full pipeline via `tools/build.ps1`: build clean, publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.4.exe`).
- **Released via git**: tag `v1.0.4` pushed + GitHub Release `v1.0.4` created with `publish/SmartCopy.exe` asset so installed 1.0.3 copies auto-update.

## Mod 1.0.3 — Auto-update + version in header (v1.0.3)

**Date:** 2026-08-09

### What was added
- **Automatic updates** (`App.CheckAndApplyUpdateAsync`): on startup (~10s in) and then every 6 hours, SmartCopy silently checks `git ls-remote --tags` against the configured repo. If a newer release exists it auto-downloads the published exe and restarts to apply it (tray balloon shows progress). Controlled by a new **"Automatically download and apply new versions"** setting (`SettingsService.AutoUpdate`, default on). The update runs only when idle — if a transfer is in progress the apply is deferred until it finishes.
- **Version shown next to app name**: header now shows `SmartCopy 1.0.3` next to the logo (was only in the About tab); window title includes the version too.
- **Robust apply script** (`UpdateService.ScheduleApplyUpdate`): the swap `.cmd` now waits in a loop until the running exe can be deleted (previously `del` could fail while the app was still running and silently break the update). Split `ApplyUpdateAsync` into `DownloadUpdateAsync` + `ScheduleApplyUpdate`; manual "Check for updates" now downloads then calls `App.RestartForUpdate()` so the swap actually happens.
- **Default repository** (`SettingsService.UpdateRepository`): defaults to `chamarawickramarathne-spec/Copy-tracker` so auto-update works out of the box.

### Verified
- 11/11 xUnit tests pass (unchanged suite; feature is UI/update layer). Full pipeline via `tools/build.ps1`: build clean, publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.3.exe`).
- **Released via git**: tag `v1.0.3` pushed + GitHub Release `v1.0.3` created with `publish/SmartCopy.exe` asset so the new auto-update can fetch it.

## Mod 1.0.2 — Multi-file paste fix: duplicate destination names (v1.0.0)

**Date:** 2026-08-09

### What was fixed
- **Root cause of "fail" on multiple files** (`IntelligentRenamer.BuildItems`): each source was named independently, so with the `FolderBased` scheme every file of the same extension got the same destination (`vacation_1.jpg`, `vacation_1.jpg`, …). The `File.Exists` collision check saw nothing yet, so the engine received duplicate destination paths and parallel copies hit a `FileShare.None` sharing violation (`IOException`) → `AggregateException` → "fail" state.
- **Batch-scoped name reservation** (`IntelligentRenamer.BuildItems`): now keeps a case-insensitive `HashSet<string>` of destinations reserved within the batch and reserves each generated path as it goes. `GenerateSmartFilePath` gained an optional `ISet<string>? reserved` parameter; the collision loop also rejects reserved names. Single-file behavior and existing tests unchanged.
- **Defense-in-depth** (`TransferEngine.CopyAsync`): early guard detects duplicate `DestinationPath` entries and throws a clear `ArgumentException` instead of corrupting/overwriting data.
- **Clearer failure message** (`App.StartTransfer`): the fail widget now lists each failed source → destination line (first 5 + "… and N more") instead of only the first inner exception. Failures now carry the failing item (`ConcurrentQueue<(TransferItem, Exception)>`).
- **Git self-update now works via GitHub Releases** (`UpdateService.ApplyUpdateAsync`): previously downloaded the codeload tag zip and extracted `SmartCopy.exe` from it, but no exe is committed to the repo so the update could never be delivered. Now downloads the published binary directly from `https://github.com/{repo}/releases/download/v{version}/SmartCopy.exe`. `CheckForUpdatesAsync` (git tag compare) unchanged. Version bumped to 1.0.2 (csproj + installer) so the tag triggers an update on 1.0.0 installs.

### Verified
- 11/11 xUnit tests pass (added 4: multi-file unique destinations for FolderBased, same-stem uniqueness for Sequential, engine E2E copy of 3 same-extension files, duplicate-destination guard).
- Full pipeline via `tools/build.ps1`: build clean, tests green, publish OK, installer rebuilt (`installer/out/SmartCopySetup_1.0.2.exe`).
- **Released via git**: changes committed + pushed to `origin/main`, tag `v1.0.2` pushed, GitHub Release `v1.0.2` created with `publish/SmartCopy.exe` as asset (asset name exactly `SmartCopy.exe` so the update URL resolves). Repo switched to **public** — the anonymous `HttpClient` download used by `UpdateService` requires public (or authenticated) access; anonymous GET of the asset verified `200`.

## Mod 1.0.1 — Paste interception fixes (v1.0.0)

**Date:** 2026-08-09

### What was fixed
- **Explorer folder resolution** (`ExplorerFolderService.GetFolderPathForWindow`): was using `Uri.AbsolutePath` which returned `/C:/...` with a leading slash; switched to `uri.LocalPath` so the correct Windows path is returned.
- **Ctrl+V interception** (`App.OnInterceptPaste`): COM (`IShellWindows` via `CoCreateInstance`) cannot be called from inside the low-level keyboard hook callback — it throws `RPC_E_CANTCALLOUT_ININPUTSYNCCALL` (0x8001010D). Now the hook does only cheap non-COM checks (clipboard file-drop list, foreground window class) and returns `true` to suppress the paste, then resolves the folder on the UI thread (`ResolveAndTransfer`).
- **Safe fallback** (`ReplayPaste`): if the Explorer folder cannot be resolved on the UI thread, the suppressed `Ctrl+V` is replayed via `keybd_event` so the user's normal paste still happens. A `_replayingPaste` guard flag prevents the replayed keys from re-triggering the hook.
- **NativeMethods**: added `keybd_event`, `VkControl`, `VkV`, `KeyeventfKeyUp` for the replay fallback.
- **`.gitignore`**: excluded `charitha/` (user's test photos).

### Verified
- E2E diagnostic (temp console app `SmartCopyDiag`): hook fires on Ctrl+V in Explorer, folder resolves on worker thread, transfer completes → `vacation_3.jpg` created in the destination. 7/7 xUnit tests pass. Installer rebuilt (`installer/out/SmartCopySetup_1.0.0.exe`).

## Mod 1.0.0 — Initial Release (v1.0.0)

**Date:** 2026-08-09

**Stack:** WPF (.NET 8) · C# 12 · self-contained single-file publish · Inno Setup installer · git-based updates.

### What was built
- **SmartCopy.Core** (`src/SmartCopy.Core`)
  - `TransferEngine` — async copy engine: `FileOptions.Asynchronous` streams, 1MB pooled buffers via `ArrayPool<byte>.Shared`, capped parallel file copies, live progress/speed/ETA, cancellation.
  - `IntelligentRenamer` — auto-renaming (`Vacation_3.jpg` style). Two schemes: FolderBased and Sequential; collision-safe.
  - `ClipboardService` — reads the OS file-drop list (`CF_HDROP`).
  - `ExplorerFolderService` — resolves the active Explorer window's folder via `IShellWindows` COM.
  - `SmartCopyOrchestrator` — glue: sources + destination → rename → copy → result.
- **SmartCopy.App** (`src/SmartCopy.App`) — WPF application
  - `GlobalKeyboardHook` — `WH_KEYBOARD_LL` on a dedicated STA thread; intercepts Ctrl+V only when Explorer is focused AND the clipboard holds files; otherwise passes through.
  - Mini-player progress widget (borderless, topmost, 60fps eased progress bar, expandable detail list, cancel/open-folder).
  - Fluent dark theme (custom templates: buttons, text boxes, combo/slider, tabs, progress, list view) + dark title bar/rounded corners via DWM.
  - System tray icon (WinForms interop, no external NuGet), single-instance mutex, auto-start via HKCU Run.
  - Settings persisted to `%AppData%\SmartCopy\settings.json`.
  - Git-based self-update (`git ls-remote` tag check + codeload zip apply + batch self-replace).
- **Tests** (`tests/SmartCopy.Tests`) — 7 xUnit tests covering renamer naming/collisions and engine copy/progress/cancel. All pass.
- **Packaging** — `tools/build.ps1` (test → publish → installer), `start.bat` (dev launch), `installer/smartcopy.iss` (Inno Setup, per-user install, no admin).
- **Media** — `media/smartcopy.ico` (multi-res, generated from `media/logo.png`), `media/logo.png`.

### Build / run
- Publish: `dotnet publish src/SmartCopy.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish`
- Installer: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\smartcopy.iss`
- Full pipeline: `powershell -ExecutionPolicy Bypass -File tools\build.ps1`

### Known limitations (v1.0.0)
- Ctrl+V interception only works when a real Explorer window is focused (not desktop, not dialogs over Explorer). Otherwise paste falls through to normal behavior.
- Elevated Explorer windows cannot be read by a non-elevated SmartCopy (paste falls through).
- Git self-update requires a configured repository (`owner/repo`) and git on the target machine; inert until set.

### App rules followed
- Clean code, every file under 300 lines.
- Version 1.0.0. No database → no `sql/` folder.
- Git repo `Copy-tracker` holds only application-related files (`bin/`, `obj/`, `publish/`, `installer/out/`, `sell/` excluded).
- Windows app → exe + installer produced. CLI scripts → `start.bat`.
- `medial_support.txt` maintained in root.
