<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher icon">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja.md">日本語</a> · <strong>English</strong>
</p>

# FFXIV Super Launcher

A community launcher for FINAL FANTASY XIV (FFXIV) on **Linux**, using **Proton** to run the game. It supports the **China, Traditional Chinese, and Global** game clients.

Maintained by **[ImperatorVienna](https://github.com/ImperatorVienna)**, this project builds on the source code and experience of [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher), [XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher), and [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher), with reference to [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) for the Traditional Chinese login protocol and feature design. It is an independently maintained community derivative, not an official release of Square Enix, Shengqu Games, or USERJOY, nor maintained or endorsed by those upstream projects. Original copyright and license notices are retained.

> **Supported platform: x86_64 Linux.** Obtain release files, release notes, and checksums from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases).

The bundled browser is shared by official pages and web verification; it is independent of the regional sign-in backends.

## Features

- **Three game regions**: a shared graphical interface and Proton launch workflow, with separate client directories, account credentials, Dalamud, and plugin data.
- **Proton support**: discovers installed versions in system locations and Steam libraries, with manual path selection. Tested on Arch Linux–based CachyOS with proton-cachyos-slr.
- **Account management**: add, select, edit, and delete accounts by game region; store credentials in a Linux desktop password manager or ordinary files.
- **Verification codes**: manual code entry and local TOTP generation for Traditional Chinese and Global accounts. Global accounts require OTP only if it is enabled on the account.
- **Game updates**: check and install patches for the selected game region, including updating without launching the game.
- **Dalamud and plugins**: optional Dalamud support; manage plugin enablement and check for Dalamud and plugin updates without entering the game.
- **Cross-region synchronization**: use “Sync Dalamud settings”, “Sync installed plugins”, and “Sync plugin settings” to sync individual categories. Select a source region and one or more destinations, then click “Sync to selected game regions”. Only the selected destinations are overwritten.
- **Steam integration**: supports Global (Steam) accounts; optionally register as a Steam compatibility tool to open the launcher through Steam and track playtime.
- **Interface and news**: supports 简体中文, 繁體中文, 日本語, and English; displays official events and announcements for the selected game region and provides links to official pages.
- **China cross-data-center travel**: submit travel requests, view history, and return to the original data center, with an option to launch the game when finished.

Before using a third-party launcher, Dalamud, or plugins, read the applicable game terms of service and assess the risks. Dalamud is disabled by default. This project does not guarantee account safety or plugin availability.

## Project highlights

- **Built for Linux**: only x86_64 Linux is maintained. Windows, macOS, ARM, and 32-bit x86 are not supported. AppImage distribution simplifies deployment on Linux desktops, including immutable systems; consult release notes for compatibility details.
- **Uses Proton**: replaces the bundled Wine + DXVK approach of XIVLauncher and XIVLauncherCN with an installed Proton version. Installed versions are discovered automatically, and CachyOS's proton-cachyos-slr has been tested on real hardware.
- **Unified game-region management**: switch between China, Traditional Chinese, and Global in one launcher, with separate accounts, clients, and plugin data. Each region's Dalamud application is downloaded on demand; the package may include shared or preinstalled helper components.
- **Plugin management outside the game**: change plugin enablement, select plugin updates, and synchronize settings between game regions without entering the game.
- **Credential management**: manage credentials by game region, with desktop password manager or local plaintext storage and local TOTP generation.
- **Reuse an existing Global (Steam) pfx**: retain local game settings and character configuration files when switching from the official launcher to FFXIV Super Launcher. This does not concern character progress stored on the game servers.
- **Traditional Chinese Dalamud injection adaptation**: adjusts injection for Linux/Proton, using the same entrypoint path as Global and skipping ArgFixer from the original Traditional Chinese injection flow. Entering a character, installing plugins, and running them have been verified in game.
- **China Dalamud branch selection**: uses the Dalamud-DailyRoutines branch and update source used by Soil, rather than the plugin blacklist policy of the ottercorp branch. This does not imply that every plugin is compatible or exempt from the game's terms of service.

## Installation and first use

### Requirements

- An x86_64 Linux graphical desktop.
- An installed Proton version and the Steam Linux Runtime it requires. Consult that Proton version's documentation for its dependencies.
- A complete client for the desired game region. **This project does not download a complete client into an empty directory.**
- Working graphics drivers and the Vulkan support required by the selected Proton version.
- For desktop password manager storage: an available, unlocked Secret Service–compatible keyring and `secret-tool`.

The runtime package includes .NET; users do not need to install the .NET SDK separately. Consult release notes for dependencies and validation on other distributions. Working on CachyOS does not mean all distributions or sandbox environments have been tested.

### AppImage

Download the x86_64 AppImage from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases), make it executable, and run it from a writable directory of your choice. AppImage is the only release format.


The first-run wizard guides you through interface language, game region, Steam / Proton directories, credential storage, and whether to enable Dalamud.

Next, configure client directories in “Language and region”. Select the game installation directory containing the `game` subdirectory; Global also requires a complete `boot` subdirectory.

In “Login credential management”, use “Add account” to enter credentials and click “Save”. Return to “Game”, choose an account with “Select login name”, and click “Log in and launch”. A new China account only needs a login name: scan the QR code on first login, and quick-login credentials will be saved using the chosen storage method after authentication succeeds.

## Steam and Proton

### Steam-authorized accounts

If your Global account's CDKey was purchased on Steam, check “My CDKey was purchased on Steam” and ensure that the Steam client is signed in and running before logging in.

The launcher does not request or store your Steam password. Steam is used to verify CDKey ownership; the game login account remains your Square Enix account.

### Launching this launcher through Steam

In “Compatibility tools settings”, check “Register as a Steam compatibility tool (can be disabled at any time)” to register **FFXIV Super Launcher**. Restart Steam, then select this compatibility tool in FFXIV's properties in your Steam library. Uncheck the option to remove the registration.

This provides a Steam launch entry; **it does not enable Steam account authentication**. Authentication is still controlled by “My CDKey was purchased on Steam”.

Steam uses the stable launcher script. After moving or renaming the AppImage, run it once to refresh its location. When upgrading from an older direct-path release, re-register Steam once.

### Compatibility environment / pfx

By default, the launcher uses “Proton data directory (pfx is created here automatically; a dedicated directory is recommended)” in “Compatibility tools settings”. For Global, check “Use an existing Steam pfx for Global (Steam)” to reuse an existing Global (Steam) environment and its local game settings.

When unchecked, Global uses the same shared environment settings as the other two game regions. Selecting an existing Steam environment does not migrate or synchronize plugin configurations.

## Updates

The launcher checks for stable releases in the background at startup. You can also use **Check for updates** beside the version in About; there is no update-channel selector. AppImage updates require confirmation, download the complete file and verify a signed manifest and SHA256 before replacement. The new AppImage keeps its new version number in its filename in the same directory; the old version is retained as `.previous`. Steam and application-menu entries use `~/.local/share/xivlauncher-super/appimage-launcher`, which records the current AppImage path. After moving or renaming the AppImage manually, run it once to refresh this path. If multiple copies exist, the last one launched is used. Exit the game before updating. After installation, the new AppImage opens automatically using its absolute path, independently of menu registration. Accounts and settings remain separate. Failed background checks do not block game login.

For AppImage, use **Add to applications menu** in About. After moving or renaming the file, run it once; there is no need to recreate the menu entry. Re-add the entry once when upgrading from an older direct-path release.

Before checking for updates, AppImage startup creates default configuration if missing, refreshes the fixed launch script, and creates the application menu entry in that order. You can recreate it from About. After setup and after adding the menu entry, available desktop menu caches are refreshed in the background without opening a terminal. Refresh failures do not block the launcher.

| Action | Behavior |
| --- | --- |
| Log in and launch | After successful account authentication, checks game updates and, if enabled, Dalamud updates, then launches the game |
| Check for updates and update the game | Updates only the selected region's game client; China and Traditional Chinese do not require login, while Global requires authentication without launching the game |
| Check for updates and update Dalamud | Manually checks Dalamud for the selected game region |
| Check for updates and update plugins | Checks the recorded source repository of each plugin and lets you select updates to install |

If a Dalamud update connection times out, you can continue with an existing usable version or disable Dalamud and launch. Disabling is saved, so you must enable it manually later. Skipping an update does not guarantee a successful launch if the local version is missing or incompatible.

The Dalamud and plugin update buttons are on “Dalamud and plugins”. This page also provides “Refresh plugin enablement states” and “Apply plugin enablement states”. After changing the checkboxes, click the latter to save; changes take effect the next time you launch the game.

Before syncing plugins between game regions, check compatibility with the destination's Dalamud API and plugin versions. Synchronization overwrites the selected destinations. For now, do not mix Traditional Chinese plugin DLLs with those of the other game regions.

## Settings and credentials

The default data directory is:

```text
~/.local/share/xivlauncher-super/
├── ffxiv_cn/     # China data
├── ffxiv_tc/     # Traditional Chinese data
├── ffxiv/        # Global data
├── compatdata/  # Default shared environment, containing pfx
└── logs/        # Diagnostic logs
```

The application honors `XDG_DATA_HOME`. Shared Steam and Proton settings are stored in the launcher data directory; accounts and Dalamud data are separated by game region.

“Use desktop password manager” in “Login credential management” is checked by default. After unchecking it and clicking “Save”, credentials subsequently saved to ordinary files are stored in `credentials.json` in the corresponding game-region directory. They may contain plaintext passwords, 2FA secrets, or quick-login credentials. This preference applies to all game regions; existing credentials are not moved automatically. Do not upload or share these files.

A 2FA secret is different from a six-digit verification code: the secret generates codes, while a code is entered only for the current login. Do not enter a six-digit code in the secret field.

## Feedback

Submit bugs and feature requests through [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues). “About” includes a disabled “Join Discord” button; it will be enabled in a later version after the community is established.

Please include:

- Launcher version, Linux distribution, desktop environment, and Proton version.
- Game region and whether you launch directly or through the Steam compatibility tool.
- Reproduction steps, expected behavior, actual behavior, and the time of the error.
- Relevant screenshots or diagnostic logs.

The on-screen log is concise; “About” shows the full diagnostic log path. Before submitting logs or screenshots, remove sensitive information such as account details, login tickets, passwords, 2FA secrets, and QR codes.

## Building from source

Requires the .NET 10 SDK, a C compiler, Python 3, Git, and `7z`. Building and packaging may download NuGet dependencies, pinned helper components, and corresponding source code.

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

The default output directory is `artifacts/linux/`. Use a fresh directory for subsequent builds, for example:

```bash
bash scripts/package-linux.sh artifacts/local-build
```

Full packaging generates licenses, dependency notices, corresponding-source attachments, `SOURCE-DELIVERY.json`, and checksums. When redistributing, retain the notices and provide corresponding source as required by the applicable licenses. See the [source delivery guide](compliance/SOURCE-DELIVERY.txt).

## About page

“About” displays the launcher version, maintainer, and upstream acknowledgements, with links for “GitHub repository”, “Report an issue”, “Maintainer profile”, “View license”, “Third-party notices”, “Source and provenance”, and “Icon source”. Web links open in the built-in browser, which retains “Open in system browser”; licenses and third-party notices can be viewed offline within the launcher. The full diagnostic log path appears only on this tab, immediately above the bottom log panel.

## Upstream projects and acknowledgements

This project builds on the work of these projects and their contributors:

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher): the original XIVLauncher, including Global authentication and patching implementations.
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher): China adaptation and related implementations.
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher): the China Soil branch, a primary reference when this project began.
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher): reference for the Traditional Chinese login protocol and feature design; this does not imply that its entire repository is licensed under this project's license.
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud), [Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud), and [yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud): Dalamud and injection components for the respective game regions.
- Avalonia, .NET, Proton, Steamworks, Electron (including Chromium and Node.js), xdelta3, and the other third-party components and their contributors.

See [SOURCES.txt](SOURCES.txt) for inheritance, modifications, and provenance, and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for third-party notices.

## License

Launcher code is distributed under the [GNU GPL version 3](LICENSE). Third-party components and assets retain their own licenses and notices; they are not all licensed under the GPL.

The current icon was supplied by the maintainer and comes from [PNGAAA](https://www.pngaaa.com/detail/6354760), whose page is marked “Non-commercial Use”. This is not a GPL license granted by this project for the icon, nor independent confirmation that the original copyright holder permits redistribution. See the [artwork provenance record](compliance/provenance/artwork.json).

See the [release checklist](compliance/RELEASE-CHECKLIST.txt) for distribution materials and licensing review records.

If you believe that code, dependencies or artwork in this project infringes your rights, please contact the maintainer through GitHub Issues with the affected location and supporting rights information. The maintainer will investigate and discuss appropriate removal or replacement. Do not post sensitive personal information in public issues.
