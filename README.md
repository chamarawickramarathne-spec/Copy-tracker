# SmartCopy

**Intelligent file transfer & management for Windows.**

SmartCopy intercepts the default copy/paste operation in Explorer and upgrades it: copy files with `Ctrl+C`, press `Ctrl+V` in the destination folder, and SmartCopy starts a fast, asynchronous transfer that **auto-renames every file** using the folder name — dropping `photo.jpg` into `Vacation` becomes `Vacation_3.jpg`.

- Asynchronous streaming with pooled buffers (`ArrayPool<byte>`, 1 MB default) — no UI freezes.
- Parallel file copies (configurable concurrency limit).
- 60fps animated mini-player widget with live speed / ETA / cancel / detail view.
- Fluent dark theme, dark title bar, rounded corners (Windows 11).
- System tray app with auto-start on login and single-instance guard.
- Settings stored in `%AppData%\SmartCopy\settings.json`.
- Git-based self-update (checks release tags, installs via zip).

## How it works

1. Copy files in Explorer (`Ctrl+C`).
2. Focus the destination folder and press `Ctrl+V`.
3. SmartCopy resolves the active Explorer folder, renames each file (`Vacation_3.jpg`), and streams the copy at full speed while showing the mini-player widget.

Text clipboard content and pastes outside a real Explorer window are never intercepted.

## Build

Requires the .NET 8 SDK and Inno Setup 6.

```powershell
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

This runs the tests, publishes the self-contained single-file `SmartCopy.exe` into `publish/`, and compiles the installer into `installer/out/`.

### Manual steps

```powershell
dotnet test SmartCopy.sln
dotnet publish src/SmartCopy.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\smartcopy.iss
```

## Development

```powershell
.\start.bat
```

## Updates

Set a GitHub repository (`owner/repo`) in the About → Updates tab. SmartCopy checks release tags via `git ls-remote` and installs newer versions from the tagged zip automatically.

## Repository layout

```
src/SmartCopy.Core/   engine, renamer, clipboard, shell services
src/SmartCopy.App/    WPF app: tray, hook, mini-player, main window, theme
tests/SmartCopy.Tests/xUnit tests
tools/                build scripts, icon generation
installer/            Inno Setup script
media/                logos and icons
```

All application files are tracked in git; build artifacts (`bin/`, `obj/`, `publish/`, `installer/out/`) are excluded. See `AGENTS.md` for the modification log.
