# FFXIV Super Launcher — Linux Usage and Development Guide

**English** | [简体中文](LINUX.zh-CN.md) | [繁體中文](LINUX.zh-TW.md)

The current stable release is 1.0.0. It supports only x86_64 Linux and runs the game through Proton. For installation, account login and features, see the [README](README.md); links to other languages appear at the top of that document.

## Installation and updates

- AppImage is the release format and includes the .NET and Electron runtime components.
- When the AppImage runs directly, the launcher first creates any missing default configuration and refreshes the fixed launch script and application menu entry, then checks for updates.
- The fixed entry defaults to `~/.local/share/xivlauncher-super/appimage-launcher`; the application menu file defaults to `~/.local/share/applications/xivlauncher-super.desktop`. Both respect `XDG_DATA_HOME`.
- After moving or renaming the AppImage, run it directly once to refresh the recorded path. “Add to application menu” in “About” manually recreates the entry.
- The launcher automatically checks for stable releases. After the user confirms, it downloads and verifies the signature, size and SHA256, then installs the new version and reopens the interface. The old package is retained as `.previous`.
- The Release contains the AppImage, `appimage-update.json` and `appimage-update.sig`. The corresponding source archive, dependency sources and checksum files for 1.0.0 are in [release-materials/1.0.0](release-materials/1.0.0).

## Features and data separation

The internal identifiers for the China, Traditional Chinese and Global regions are `ffxiv_cn`, `ffxiv_tc` and `ffxiv`, respectively. The default data root is `~/.local/share/xivlauncher-super/`, respecting `XDG_DATA_HOME`.

Steam, Proton and the default pfx configuration are shared; each game region manages its own client, accounts, Dalamud and plugin data. Global can optionally use an existing Global (Steam) pfx. See “Settings and credentials” in the README for credential storage and paths.

For all three regions, successful login is followed by game update checks and, if Dalamud is enabled, a Dalamud update check before starting the game. The separate “Check and update game” action does not update Dalamud or plugins: China and Traditional Chinese do not require login first, while Global requires authentication. Dalamud and plugins can also be checked for updates manually on the “Dalamud and plugins” page.

China supports QR-code and quick login, as well as cross-data-center travel; Traditional Chinese and Global support manually entered codes and local TOTP generation. Global requires a code only if OTP is enabled on the account.

## Building from source

Requires the .NET 10 SDK, a C compiler, Python 3, Git, and `7z`. Building and packaging may require downloading NuGet dependencies, pinned helper components, and corresponding source code.

The complete 1.0.0 launcher source is in this repository. Original source archives, corresponding sources for bundled dependencies, and checksum files are retained in [release-materials/1.0.0](release-materials/1.0.0). Release downloads contain only the AppImage and the two small files required for signed updates.

Run from the repository root:

```bash
# Build the Linux launcher
dotnet build src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release -m:1

# Run offline tests
dotnet test src/XIVLauncher.Linux.Tests/XIVLauncher.Linux.Tests.csproj \
  -c Release -m:1 --filter "Category!=Network"

# Check recorded licensing materials
python3 scripts/check-compliance-materials.py

# Generate the runtime package and corresponding-source attachments
bash scripts/package-linux.sh
```

The default output directory is `artifacts/linux/`. Use a fresh output directory when packaging again, for example:

```bash
bash scripts/package-linux.sh artifacts/local-build
```

`package-linux.sh` generates a self-contained runtime directory, a `.tar.gz` runtime package, and corresponding source materials; **it does not directly generate an AppImage**. Creating an AppImage also requires `mksquashfs` (squashfs-tools) and an AppImage type-2 runtime with the SHA256 specified by the script:

```bash
python3 scripts/package-appimage.py \
  artifacts/linux/xivlauncher-super \
  artifacts/linux/xivlauncher-super-1.0.0-x86_64.AppImage \
  --runtime /path/to/runtime-x86_64 \
  --public-key packaging/linux/appimage-public.pem
```

See the [AppImage provenance record](compliance/notices/appimage/SOURCES.txt) for the pinned runtime commit and SHA256. Building your own runtime package does not require the project's update-signing private key; when officially publishing automatic updates, the maintainer must also sign the update manifest using OpenSSL and `scripts/sign-appimage-update.py`. The signing private key is not in the repository. See the [AppImage packaging and update guide](packaging/linux/APPIMAGE-UPDATES.md) for the complete workflow.

Full packaging also generates licenses, dependency notices, corresponding-source attachments, `SOURCE-DELIVERY.json`, and checksum files. When redistributing, retain the notices and provide corresponding source as required by the applicable licenses. See the [source delivery guide](compliance/SOURCE-DELIVERY.txt) for details.

The complete runtime directory is `artifacts/linux/xivlauncher-super/`. A normal `dotnet build` does not prepare all bundled helper components; use the directory produced by the packaging script to verify the complete runtime flow. The script prepares pinned versions of the China launch helper, Traditional Chinese entrypoint component and Electron browser, and generates license and corresponding-source materials.

Set `DOTNET=/path/to/dotnet` to select the SDK and `VERSION` to specify the build version.

The source tree and build scripts do not depend on the four deleted reference project directories. For provenance, licenses and dependency records, see [SOURCES.txt](SOURCES.txt), [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and the [source delivery notes](compliance/SOURCE-DELIVERY.txt). Preserve these materials and their relationships.
