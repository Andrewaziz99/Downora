# Releasing Downora

## What the installer is

`installer\Downora.iss` (Inno Setup 6) builds `Downora-Setup-<version>.exe`:

- **Self-contained:** the .NET runtime is inside, so nothing else has to be installed. About 180 MB.
- **Tools bundled:** yt-dlp, FFmpeg, FFprobe and Deno sit in the app's `tools` folder. On first start Downora copies them to `%LocalAppData%\Downora\tools` (it never overwrites a copy that is already there, because yt-dlp updates itself). So a fresh install works offline.
- **No administrator rights:** it installs for the current user in `%LocalAppData%\Programs\Downora`. In the setup dialog an administrator can choose "for all users" (Program Files) instead.
- **Upgrades in place:** the same `AppId` means a newer installer replaces the old version, keeps settings, history and the tools copy, and closes a running Downora first.
- **Languages:** English and Arabic, chosen from the Windows language.
- **Uninstall:** removes the program, the copied tools, logs and thumbnails, and the "start when I sign in" entry. It asks before deleting settings and history (default: keep). Downloaded files are never touched.

## Building it on your machine

Needs the .NET SDK from `global.json` and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```
installer\build-installer.ps1 -FetchTools
```

- `-FetchTools` downloads the tools from their official releases and checks each file's published SHA-256 (the same way Downora's own "Install" button does). Without it, point `-ToolsDir` at a folder that already holds `yt-dlp.exe`, `ffmpeg.exe`, `ffprobe.exe` and `deno.exe` (for example `%LocalAppData%\Downora\tools`).
- `-Version 1.2.3` stamps a version; the default is `<Version>` in `Directory.Build.props`.
- The result is `artifacts\installer\Downora-Setup-<version>.exe` with a `.sha256` file beside it. Building takes about four minutes, most of it compression.

### Trying it without disturbing your own Downora

The real installer closes a running Downora. For a quick test use `-TestInstall` (fast, no running-app check, named `...-test.exe`) and run the installed copy with its own data:

```
installer\build-installer.ps1 -ToolsDir "$env:LOCALAPPDATA\Downora\tools" -TestInstall
Downora-Setup-0.1.0-test.exe /VERYSILENT /CURRENTUSER /NOICONS /DIR="C:\Temp\DownoraTest"
$env:DOWNORA_DATA_DIR = "C:\Temp\DownoraData"; $env:DOWNORA_ALLOW_MULTIPLE_INSTANCES = "1"
C:\Temp\DownoraTest\Downora.exe
```

Do not run that copy's uninstaller on a machine where you use Downora: it removes the shared `%LocalAppData%\Downora\tools`, `logs` and `thumbnails`. Delete the test folder and the `Downora` entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall` instead. Never publish a `-test` installer.

## Code signing

An unsigned installer works, but Windows SmartScreen warns "unknown publisher" until it has built reputation. Signing needs a code-signing certificate, which has to be bought and is tied to a name:

- an OV or EV certificate from a certificate authority (EV removes the SmartScreen warning immediately), or
- Azure Trusted Signing, if you are eligible.

Locally, import the certificate into your certificate store and pass its thumbprint:

```
installer\build-installer.ps1 -FetchTools -CertThumbprint <40-character thumbprint>
```

This signs Downora's own files (`Downora*.exe`, `Downora*.dll`) and the installer, with a timestamp so the signature outlives the certificate. The bundled tools are not re-signed: they belong to their publishers. (The small uninstaller that Inno Setup generates is not signed either.)

In GitHub, add two repository secrets, `SIGNING_PFX_BASE64` (the `.pfx` file, base64-encoded) and `SIGNING_PFX_PASSWORD`. The release workflow then signs automatically; without them it builds unsigned. Never commit a certificate or its password.

## Cutting a release

1. Make sure `main` is green and the roadmap items for the release are done.
2. Decide the version (semantic: `0.2.0`). Update `<Version>` in `Directory.Build.props` and commit.
3. Tag and push: `git tag v0.2.0` then `git push origin v0.2.0`.
4. The **Release** workflow runs the tests, builds the installer, and publishes a GitHub release with `Downora-Setup-0.2.0.exe` and its `.sha256` file. (Running the workflow by hand only produces a build artifact.)
5. Download the installer from the release and install it on a clean machine or account before announcing it.

To check a download: `Get-FileHash Downora-Setup-0.2.0.exe -Algorithm SHA256` should print the hash in the `.sha256` file.

## Licenses

`installer\THIRD-PARTY-NOTICES.txt` ships with the app and lists the license of each bundled program. FFmpeg is the LGPL build, used only as a separate program, and can be replaced by the user (Settings > Tools > Use my own FFmpeg). Downora has no `LICENSE` file yet; if you add one at the repository root the installer shows it automatically.
