<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher icon">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja.md">日本語</a> · <strong>English</strong>
</p>

# FFXIV Super Launcher

A community launcher for FINAL FANTASY XIV (hereafter FFXIV) on **Linux**, using **Proton** to run the game. It supports the **China, Traditional Chinese, and Global** game clients.

Maintained by **[ImperatorVienna](https://github.com/ImperatorVienna)**, this project continues development based on the source code and experience of [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher), [XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher), and [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher), with reference to [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) for the Traditional Chinese login protocol and feature design. It is an independently maintained community derivative, not an official release of Square Enix, Shengqu Games, or USERJOY, nor maintained or endorsed by those upstream projects. The original authors' copyright and license notices are retained.

> **Supported platform: x86_64 Linux.** Obtain release files and release notes from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases); the checksum files for 1.0.0 are in [release-materials/1.0.0](release-materials/1.0.0).

The built-in browser is shared by official pages and web verification and is independent of each game region's login module.

## Features

- **Three game regions**: a shared graphical frontend and Proton launch workflow, with separate management of client directories, account credentials, Dalamud, and plugin data.
- **Proton support**: scans installed versions in the system and Steam libraries and supports manually specified paths; tested on real hardware with Arch Linux–based CachyOS / proton-cachyos-slr.
- **Account management**: add, select, edit, and delete accounts by game region; credentials can be saved in local plaintext files.
- **Verification codes**: manual code entry and automatic local TOTP generation for Traditional Chinese and Global. Global requires a code only if OTP is enabled on the account.
- **Game updates**: check and install client patches for the corresponding game region, including updating the game without launching it.
- **Dalamud and plugins**: optionally enable Dalamud; manage plugin enablement states and check for Dalamud and plugin updates without entering the game.
- **Cross-region synchronization**: use “Sync Dalamud settings”, “Sync installed plugins”, and “Sync plugin settings” to synchronize the selected categories separately; select a source game region and one or more destinations, then click “Sync to selected game regions”. Only the corresponding content in the selected destinations is overwritten.
- **Steam integration**: supports Global (Steam) account login; manually register as a Steam compatibility tool to open the launcher through Steam and record playtime.
- **Interface and announcements**: supports 简体中文, 繁體中文, 日本語, and English; displays official events and announcements for the corresponding game region and provides links to official pages.
- **China cross-data-center travel**: submit travel requests, query history, and return to the original data center, with an option to launch the game upon completion.

Before using a third-party launcher, Dalamud, or plugins, read the game's applicable terms of service and assess the risks yourself. Dalamud is disabled by default; this project does not guarantee account safety or plugin availability.

## Project highlights

- **Built specifically for Linux**: only x86_64 Linux is maintained; no Windows, macOS, ARM, or 32-bit x86 versions are provided. AppImage distribution facilitates deployment on Linux desktops, including immutable systems; consult release notes for specific compatibility information.
- **Uses Proton for compatibility**: removes the bundled Wine + DXVK approach of XIVLauncher and XIVLauncherCN and calls an installed Proton instead. The launcher automatically discovers installed Proton versions and has been verified on real hardware using CachyOS's proton-cachyos-slr.
- **Unified management of game regions**: switch between China, Traditional Chinese, and Global logins in one launcher, with separate account, client, and plugin data. Each game region's Dalamud application is downloaded on demand; the runtime package may include shared or preinstalled helper components.
- **Plugin management outside the game**: change plugin enablement states, select plugins to update, or synchronize settings between game regions without entering the game.
- **Credential management**: manage account login credentials by game region, with local plaintext storage and local TOTP generation.
- **Reuse an existing Global (Steam) pfx**: retain local game settings and character configuration files from the original pfx when switching from the official launcher to FFXIV Super Launcher (this does not concern character progress stored on the game servers).
- **Traditional Chinese Dalamud injection adaptation**: adjusts the injection workflow for Linux/Proton, using the same entrypoint path as Global and skipping ArgFixer from the original Traditional Chinese injection workflow; entering a character and installing and running plugins have been verified in game on real hardware.
- **China Dalamud branch selection**: uses the Dalamud-DailyRoutines branch and update source used by Soil, rather than adopting the ottercorp branch's plugin blacklist policy; this does not mean every plugin is compatible or exempt from the game's terms of service.

## Installation and first use

### Requirements

- An x86_64 Linux graphical desktop.
- An installed Proton version and the Steam Linux Runtime required by that version; consult the selected Proton version's documentation for specific dependencies.
- A complete client for the corresponding game region. **This project does not download a complete client into an empty directory.**
- Working graphics drivers and the Vulkan support required by the selected Proton version.

The runtime package includes .NET and Electron runtime components (including Chromium and Node.js); no separate installation of the .NET SDK, Electron, or a browser is required. Consult the specific release notes for dependencies and validation on other distributions; working on CachyOS does not mean every distribution or sandbox environment has been tested.

### AppImage

Download the x86_64 AppImage from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases), make it executable, and run it. You may place it in a writable directory of your choice. AppImage is the only release package format.

The first-run wizard guides you through interface language, game region, Steam / Proton directories, and whether to enable Dalamud.

Next, configure client directories in “Language and region”. Select the game installation directory containing the `game` subdirectory; Global also requires a complete `boot` subdirectory.

In “Login credential management”, use “Add account” to enter credentials and click “Save”. Return to “Game”, choose an account with “Select login name”, and click “Log in and launch”. A new China account needs only a login name: scan the QR code on first login, and quick-login credentials are automatically saved after successful authentication.

### Interface and Global client language settings

On the “Language and region” page:

- **Launcher interface language**: after choosing a language, click the adjacent “Apply” button and restart the launcher as prompted to apply it to all pages.
- **Global client language**: shown only when Global is selected, with 日本語, English, Français, and Deutsch available. Click the adjacent “Apply” button to use it on the next game launch; switching to another game region does not clear this selection.
- **Account CDKey version**: choose JP, NA, or EU according to the account's actual license; the default is NA. This setting selects the corresponding official website, store, Mog Station, and event announcement sources; it does not change the account's license.
- **Preferred language for the official website and store**: shown only when the CDKey version is EU, with English(UK), Français, and Deutsch available. It selects the corresponding language for the official website, store, and announcements. Changes to these web preferences are saved automatically and are independent of the client language.

## Steam and Proton

### Steam-authorized accounts

If your Global account's CDKey was purchased on Steam, check “My CDKey was purchased on Steam” and ensure that the Steam client is signed in and running before logging in.

The launcher does not request or store your Steam account password (Steam is used only to verify CDKey ownership; the game login account remains your Square Enix account).

### Launching this launcher through Steam

In “Compatibility tools settings”, check “Register as a Steam compatibility tool (can be disabled at any time)” to register **FFXIV Super Launcher**. Restart Steam, then select this compatibility tool in FFXIV's properties in your Steam library. Uncheck the option to remove the registration.

This feature provides a Steam launch entry; **it does not enable Steam account authentication**. Account authentication is still controlled by “My CDKey was purchased on Steam”.

This feature uses a fixed launcher script. After manually moving or renaming the AppImage, run it once to refresh the path. After an automatic update, the launcher restarts and refreshes the path automatically; no manual action is required.

### compatibility environment / pfx

By default, the launcher uses “Proton data directory (pfx is created here automatically; a dedicated directory is recommended)” in “Compatibility tools settings”. For Global, check “Use an existing Steam pfx for Global (Steam)” to reuse the existing environment of an installed Global (Steam) client and its local game settings.

When this option is disabled, Global uses the shared environment settings used by the other two game regions. Selecting an existing Steam environment does not migrate or synchronize plugin configurations.

## Updates

The launcher checks for stable releases in the background whenever it opens. You can also click “Check for updates” beside the version in “About”; there is no update-channel selector. AppImage updates require user confirmation. After the complete download, the manifest signature and SHA256 are verified before the original file is replaced. The updated file is saved in the same directory with the new version number in its filename, and the old version is retained as `.previous`. Steam and application-menu entries launch through `~/.local/share/xivlauncher-super/appimage-launcher`. After manually moving or renaming the AppImage, double-click it once to refresh the path automatically; Steam and application-menu entries will then continue to work. If there are multiple copies, the last one launched is used. Exit the game before updating. After installation, the new interface opens directly using the new AppImage's absolute path, independently of application-menu registration. Accounts and settings are stored separately, and failed background update checks do not block game login.

The application-menu entry is created automatically when the AppImage starts. “Add to applications menu” in “About” rebuilds the entry manually; normally, no extra action is needed. The default entry file is `~/.local/share/applications/xivlauncher-super.desktop`, honoring `XDG_DATA_HOME`. After manually moving or renaming the AppImage, run it directly once to refresh the record.

Before checking for updates, AppImage startup creates any missing default configuration, writes the fixed launcher script, and creates the application-menu entry, in that order. The entry can also be rebuilt manually in “About”. After setup or adding the entry, available application-menu caches are refreshed in the background without opening a terminal; a refresh failure does not prevent use of the launcher. The menu entry uses the absolute path to a persistent icon file, so the icon remains readable after the AppImage is unmounted.

| Action | Behavior |
| --- | --- |
| Log in and launch | After successful account authentication, checks game updates and, if Dalamud is enabled, its updates, then launches the game |
| Check for updates and update the game | Updates only the selected game region's client; China and Traditional Chinese do not require prior login, while Global requires account authentication without launching the game |
| Check for updates and update Dalamud | Manually checks Dalamud for the selected game region |
| Check for updates and update plugins | Checks for updates using each plugin's recorded source repository and lets the user select which updates to install |

If a Dalamud update connection times out, you can continue launching with an existing usable version or disable Dalamud and launch. Disabling is saved, so you must enable it manually later; if the existing version is missing or incompatible, skipping the update does not guarantee a successful launch.

The two Dalamud and plugin update buttons above are on “Dalamud and plugins”. This page also provides “Refresh plugin enablement states” and “Apply plugin enablement states”; after changing the checkboxes, click the latter to save. Changes take effect on the next game launch.

Before synchronizing plugins between game regions, check compatibility with the destination's Dalamud API and plugin versions. Synchronization overwrites the selected destinations' data. For now, do not directly mix Traditional Chinese plugin DLLs with versions from the other game regions.

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

The application honors `XDG_DATA_HOME`. Shared Steam and Proton settings are stored in the launcher data directory; account and Dalamud data are isolated by game region.

Credentials are stored only as plaintext in `credentials.json` inside each game-region directory. Passwords, 2FA secrets, and quick-login credentials are not encrypted. Credential files created by the launcher allow only the current user to read and write them; do not upload or share these files.

A 2FA secret is different from a six-digit verification code: the secret generates codes, while a code is entered only for the current login. Do not enter a six-digit verification code in the secret field.

## Feedback

Submit issues and feature suggestions through [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues). “About” includes a reserved “Join Discord” button, currently disabled; it will be enabled in a later update after the community is established.

When reporting an issue, please provide as much of the following as possible:

- Launcher version, Linux distribution, desktop environment, and Proton version.
- Current game region and whether you launch directly or through the Steam compatibility tool.
- Reproduction steps, expected behavior, actual behavior, and the time of the error.
- Necessary screenshots or relevant diagnostic logs.

The on-screen log remains concise; the full diagnostic log path is shown in “About”. Before submitting logs or screenshots, review them and remove sensitive content such as account information, login tickets, passwords, 2FA secrets, and QR codes.

## Building from source

To build from source or contribute to development, see the [development guide](LINUX.en.md).

## About page

“About” displays the launcher version, maintainer, and upstream acknowledgements, with links for “GitHub repository”, “Report an issue”, “Maintainer profile”, “View license”, “Third-party notices”, “Source and provenance”, and “Icon source”. Web links open in the built-in browser, which retains an “Open in system browser” button; licenses and third-party notices can be viewed offline within the launcher. The full diagnostic log path appears only on this tab, immediately above the bottom log panel.

## Upstream projects and acknowledgements

This project builds on the work of the following projects and their contributors:

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher): the original XIVLauncher, including Global authentication, patching, and related implementations.
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher): China adaptation and related implementations.
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher): the China Soil branch, the primary reference when this project began.
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher): reference for the Traditional Chinese login protocol and feature design; this does not mean its entire repository is licensed under this project's license.
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud), [Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud), and [yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud): Dalamud and injection components for the corresponding game regions.
- Avalonia, .NET, Proton, Steamworks, Electron (including Chromium and Node.js), xdelta3, and the other third-party components used by this project and their contributors.

See [SOURCES.txt](SOURCES.txt) for detailed inheritance relationships, modification scope, and provenance evidence, and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for third-party notices.

## License

Launcher code is distributed under the [GNU GPL version 3](LICENSE). Third-party components and assets retain their own licenses and notices; they must not all be treated as GPL-licensed.

The current icon was supplied by the maintainer and comes from [PNGAAA](https://www.pngaaa.com/detail/6354760), whose page is marked “Non-commercial Use”. This is not a GPL license granted by this project for the icon, nor independent confirmation that the original copyright holder permits redistribution; see the [artwork provenance record](compliance/provenance/artwork.json).

See the [release checklist](compliance/RELEASE-CHECKLIST.txt) for distribution materials and licensing review records.

If you believe that code, dependencies, or assets in this project infringe your rights, please contact the maintainer through GitHub Issues and provide the location of the relevant content and supporting rights information. The maintainer will investigate and discuss a resolution, removing or replacing the content where necessary. Do not submit sensitive personal information in public issues.
