# L2 Modern Updater

MVP implementation based on `l2_modern_updater_tz.md`.

## Projects

- `Launcher` - WPF launcher UI with manifest loading, file verification, update download, and game launch.
- `Updater.Core` - shared manifest, SHA256 verification, safe path handling, and download services.
- `PatchBuilder` - WPF utility that scans a client directory and generates `manifest.json`.
- `ConfigBuilder` - WPF utility that generates launcher `config.json`.

`Updater.Core` also includes a managed `Lineage2Ver413` codec for `l2.ini` style files.
It can decode legacy/modern 413 files and encode modern 413 files without shipping `l2encdec.exe`.

## Build

```powershell
.\Tools\build_all.bat
```

Build artifacts are published to `build\Launcher`, `build\PatchBuilder`, and `build\ConfigBuilder`.
Projects target `.NET 8`.

Single-file client build:

```powershell
.\Tools\build_launcher_single.bat
```

Output: `build\LauncherSingle\Launcher.exe`

## Generate Manifest

```powershell
.\build\PatchBuilder\PatchBuilder.exe
```

The launcher can load either a local manifest path or an HTTPS manifest URL.
For local tests, use the `Local` button in PatchBuilder so manifest file URLs point to the selected source folder.
PatchBuilder also writes manifest `ignore` rules; Launcher uses them when reporting extra files.

## Server Layout

- Manifest URL: `https://l2.lammeronline.com/updater/manifest.json`
- Config URL: `https://l2.lammeronline.com/updater/config.json`
- Patch files base URL: `https://l2.lammeronline.com/updater/patch/`
- Launcher URL: `https://l2.lammeronline.com/updater/Launcher.exe`

Upload generated `manifest.json` to `/updater/`.
Upload generated `config.json` to `/updater/`.
Upload patch files preserving folders to `/updater/patch/`.
