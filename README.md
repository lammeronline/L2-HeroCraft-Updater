# L2 Modern Updater

Modern Windows launcher and patching toolkit for Lineage II client distribution.

The repository contains three desktop applications:

- `Launcher` - player-facing launcher with file check, repair, update, self-update, settings and optional AutoLogin.
- `PatchBuilder` - operator tool for generating `manifest.json` and optional compressed `.gz` patch files.
- `ConfigBuilder` - operator tool for generating server-controlled `config.json`.

Shared patching logic lives in `Updater.Core`.

## Features

- Local or remote `config.json` bootstrap.
- Server-controlled `manifestUrl`, `newsUrl`, AutoLogin visibility and client settings visibility.
- Fast check by file size and full repair by SHA256.
- Current-file and total progress bars.
- Safe manifest paths through root-directory validation.
- Optional `.gz` patch payloads with compressed and decompressed SHA256 checks.
- Extra-file reporting with ignore rules.
- Launcher self-update through manifest metadata.
- Remote HTML news page inside the launcher.
- Optional AutoLogin account picker, launched as:

```text
l2.exe account=LOGIN password=PASSWORD
```

When AutoLogin is used, the launcher also enables `CmdLineLogin=true` in `system/l2.ini`.

## Repository Layout

```text
Launcher/            Player launcher UI and local settings.
PatchBuilder/        Manifest generator and URL validator.
ConfigBuilder/       Server config generator.
Updater.Core/        Manifest/config IO, hashing, verification and downloads.
SettingsSamples/     Local INI samples used during manual testing.
docs/                Release examples: config.example.json and news.html.
Tools/               Build and publish scripts.
build/               Local build output, not required in source releases.
```

## Requirements

- Windows 10/11 x64.
- .NET 8 SDK for building.
- HTTPS hosting for production patch/config/news files.

Published single-file builds are self-contained and do not require a separate .NET runtime.

## Build

Release build for all three tools into one folder:

```powershell
.\Tools\build_updater_single.bat
```

Output:

```text
build\updater\Launcher.exe
build\updater\PatchBuilder.exe
build\updater\ConfigBuilder.exe
```

Regular framework-dependent publish:

```powershell
.\Tools\build_all.bat
```

## Server Layout

Recommended production layout:

```text
/updater/config.json
/updater/manifest.json
/updater/news.html
/updater/Launcher.exe
/updater/patch/<client files or .gz files>
```

Default URLs used by the tools:

```text
https://yoursite.com/updater/config.json
https://yoursite.com/updater/manifest.json
https://yoursite.com/updater/news.html
https://yoursite.com/updater/Launcher.exe
https://yoursite.com/updater/patch/
```

## Release Workflow

1. Build all executables:

```powershell
.\Tools\build_updater_single.bat
```

2. Open `ConfigBuilder.exe`, configure server options and save `config.json`.

3. Upload `config.json` to:

```text
/updater/config.json
```

4. Upload `docs/news.html` or your edited HTML page to:

```text
/updater/news.html
```

5. Open `PatchBuilder.exe`, select the client/patch folder and generate `manifest.json`.

6. Upload patch files preserving relative folders to:

```text
/updater/patch/
```

7. Upload generated `manifest.json` to:

```text
/updater/manifest.json
```

8. Upload the latest published launcher to:

```text
/updater/Launcher.exe
```

9. On a clean client folder, run `Launcher.exe` and test `CHECK`, `UPDATE`, `REPAIR`, `PLAY` and AutoLogin if enabled.

## Config Example

See [docs/config.example.json](docs/config.example.json).

Important fields:

- `manifestUrl` - manifest location. Can be absolute or relative to `config.json`.
- `newsUrl` - remote HTML page shown in the news panel. Leave empty to show a blank panel.
- `showNews` - shows or hides the remote news panel. Defaults to `false`.
- `showClientSettings` - hides the settings section that edits client INI files.
- `requireUpdateBeforePlay` - disables Play until fast check passes.
- `autoLoginEnabled` - shows or hides the AutoLogin button.
- `gameExecutables` - executable candidates checked in order.

## Manifest Notes

`PatchBuilder` generates entries like:

```json
{
  "path": "system/l2.exe",
  "sha256": "HASH",
  "size": 123456,
  "url": "https://yoursite.com/updater/patch/system/l2.exe",
  "compressed": false,
  "compressedSize": 0,
  "compressedSha256": ""
}
```

When compression is enabled, `url` points to `*.gz`, while `sha256` and `size` still describe the final decompressed client file.

## Security Notes

- Client-side feature flags are convenience controls, not real security boundaries.
- Enforce AutoLogin permissions on the game/auth server.
- Use HTTPS in production.
- Manifest file writes are protected from directory traversal by `SafePath`.
- File integrity is verified by SHA256 after download/copy.
- For stronger tamper resistance, add signed `config.json`/`manifest.json` verification before public release.

## Local Files Created By Launcher

```text
launcher.settings.json       Local player preferences.
autologin.accounts.json      Local AutoLogin accounts.
launcher.log                 Launcher events.
updater.log                  Patch/update events.
*.download                   Temporary download files.
```

Do not upload these files as part of the patch.

## Release Checklist

- [ ] `build\updater` contains all three executables.
- [ ] Application icons are embedded.
- [ ] `config.json` contains the production `manifestUrl` and `newsUrl`.
- [ ] `news.html` opens correctly inside the launcher frame.
- [ ] `manifest.json` was generated from the intended patch folder.
- [ ] Patch files were uploaded with the same relative paths as the manifest.
- [ ] `PatchBuilder` `VALIDATE` succeeds for the production patch URL.
- [ ] A clean client can update and launch.
- [ ] Self-update works after uploading a newer `Launcher.exe`.
