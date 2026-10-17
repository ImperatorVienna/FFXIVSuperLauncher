<h3 align="center">Sound familiar?</h3>
<p align="center">Moving to <strong>Linux</strong>, by choice or necessity, but worried you won’t be able to enjoy FFXIV?</p>
<p align="center">Playing in <strong>multiple game regions</strong>, but each launcher only supports one, leaving you with two or three launchers that all look and feel different?</p>
<p align="center">One launcher restricts more adventurous plugins, while the version without those restrictions <strong>doesn’t support Linux</strong>?</p>
<p align="center">The game requires <strong>2FA</strong>, so you have to type a code every time you log in?</p>
<p align="center">Your launcher’s bundled Wine performs poorly, but <strong>switching to Proton</strong> is a struggle?</p>
<p align="center">Your launcher’s UI looks <strong>far worse</strong> than the official one?</p>
<p align="center">A plugin update crashes the game, forcing you to <strong>disable every plugin</strong> just to log in, then re-enable the others one by one?</p>
<p align="center">……</p>
<p align="center"><strong>Download FFXIV Super Launcher and tackle all of these problems in one place!</strong></p>
<p align="center"><a href="https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases"><strong>Download FFXIV Super Launcher →</strong></a></p>

<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher icon">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja.md">日本語</a> · <strong>English</strong>
</p>

# FFXIV Super Launcher

## Version 2.2.0

Windows x64 and Linux x86_64 are now released together. Version 2.2.0 adds native Windows launching and desktop shortcuts, improves Dalamud update recovery and China QR login, and refines the interface. See the [2.2.0 release notes](docs/releases/2.2.0.en.md).

A community launcher for FINAL FANTASY XIV (FFXIV) on **Windows and Linux**, supporting the **China, Traditional Chinese, and Global** clients. Windows launches the game natively; Linux uses an installed Proton.

Maintained by **[ImperatorVienna](https://github.com/ImperatorVienna)**, this project continues development based on the source code and experience of [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher), [XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher), and [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher), with reference to [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) for the Traditional Chinese login protocol and feature design. It is an independently maintained community derivative, not an official release of Square Enix, Shengqu Games, or USERJOY, nor maintained or endorsed by those upstream projects. The original authors' copyright and license notices are retained.

> **Platforms: Windows x64 and Linux x86_64.** Download the ZIP for Windows or AppImage for Linux from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases).

The built-in browser is shared by official pages and web verification and is independent of each game region's login module.

## Features

- **Three game regions**: one graphical interface, with separate client directories, credentials, Dalamud and plugin data. Platform-specific adapters handle native Windows or Linux/Proton launching.
- **Linux Proton support**: discovers installed Proton versions in system and Steam libraries; manual paths are supported. Tested on CachyOS with proton-cachyos-slr.
- **Account management**: add, select, edit, and delete accounts by game region; credentials are saved in local plaintext files.
- **Verification codes**: manual code entry and automatic local TOTP generation for Traditional Chinese and Global. Global requires a code only if OTP is enabled on the account.
- **Game updates**: check and install client patches for the corresponding game region, including updating the game without launching it.
- **Dalamud and plugins**: optionally enable Dalamud; manage plugin enablement states and check for Dalamud and plugin updates without entering the game.
- **Cross-region synchronization**: use “Sync third-party repositories”, “Sync installed plugins”, and “Sync plugin settings” to synchronize each category after selecting the source and destination game regions. Repository synchronization replaces only the repository list and enablement states, always preserving the destination main repository, preset repository switch, and other Dalamud settings. Original data is backed up.
- **Steam integration**: both platforms support Global (Steam Version) account authentication with Steam running. Linux additionally offers Steam compatibility-tool registration, Steam-launch fullscreen and the Steam virtual keyboard; Windows does not display these Linux-specific settings.
- **Interface and announcements**: the Electron graphical interface supports 简体中文, 繁體中文, 日本語, and English, with instant switching and no restart required; it displays official events and announcements for the corresponding game region and provides links to the official game website, server status, and other official pages.
- **Third-party plugin repositories**: add, edit, enable/disable, delete repositories, or check their connections on the right of plugin management; China region plugin updates also recognize enabled preset repositories.
- **China cross-data-center travel**: submit travel requests, query history, and return to the original data center, with an option to launch the game upon completion. Supports in-game travel and authorization recovery through DCTravelerX ([Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)).
- **Storage cleanup**: clear download caches, logs, and built-in browser caches from About; startup reclaims session records confirmed to be stale.

Before using a third-party launcher, Dalamud, or plugins, read the game's applicable terms of service and assess the risks yourself. Dalamud is disabled by default; this project does not guarantee account safety or plugin availability.

## Project highlights

- **Two distribution formats**: Windows x64 ZIP and Linux x86_64 AppImage. No macOS, ARM or 32-bit x86 build is provided. Keep the Windows folder intact; do not copy only its EXE.
- **Linux uses installed Proton**: no bundled Wine environment is required. Windows uses native game launching and does not need Proton.
- **Unified management of game regions**: switch between China, Traditional Chinese, and Global logins in one launcher, with separate account, client, and plugin data. Each game region's Dalamud application is downloaded on demand; the runtime package may include shared or preinstalled helper components.
- **Plugin management outside the game**: change plugin enablement states, select plugins to update, or synchronize settings between game regions without entering the game.
- **Credential management**: manage account login credentials by game region, with local plaintext storage and local TOTP generation.
- **Reuse an existing Global (Steam Version) pfx**: retain local game settings and character configuration files from the original pfx when switching from the official launcher to FFXIV Super Launcher (this does not concern character progress stored on the game servers).
- **Traditional Chinese Dalamud injection adaptation**: adjusts the injection workflow for Linux/Proton, using the same entrypoint path as Global and skipping ArgFixer from the original Traditional Chinese injection workflow; entering a character and installing and running plugins have been verified in game on real hardware.
- **China Dalamud branch selection**: uses the Dalamud-DailyRoutines branch and update source, rather than adopting the ottercorp branch's plugin blacklist policy; this does not mean every plugin is compatible or exempt from the game's terms of service.

## Installation and first use

### Requirements

- Windows x64 or an x86_64 Linux graphical desktop, with the game’s required graphics drivers and dependencies.
- On Linux: an installed Proton and its required Steam Linux Runtime.
- A complete client for the corresponding game region. **This project does not download a complete client into an empty directory.**
- On Linux: the Vulkan support required by the selected Proton.

The runtime package includes .NET and Electron runtime components (including Chromium and Node.js); no separate installation of the .NET SDK, Electron, or a browser is required. Consult the specific release notes for dependencies and validation on other distributions; working on CachyOS does not mean every distribution or sandbox environment has been tested.

### Linux AppImage

Download the Linux AppImage from [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases), make it executable, and run it from a writable directory.

### Windows ZIP

Download and fully extract `FFXIVSuperLauncher-2.2.0-win-x64.zip`. Keep the `FFXIV Super Launcher` folder in a writable location and run `FFXIVSuperLauncher.exe`. Do not run it from inside the ZIP or move the EXE without the other files. .NET and Electron are included.

The launcher refreshes its fixed launch scripts and desktop shortcut, including the icon. After moving or renaming the launcher, run the EXE directly once to refresh its path. No Start-menu registration is performed. Windows initial setup skips Steam / Proton discovery. Steam authentication still requires the Steam client to be running; a Steam installation path is not required for that authentication.

### Shared initial setup

The first-run wizard starts in English and separates launcher language and game region into steps, followed by Steam / Proton setup on Linux. For Global, you can also select the client language, CDKey version, and preferred store language for EU accounts. Initial defaults are NA, an English client, and English (UK) for the store preference. Individual selections are saved immediately; automatic launcher update checks start only after setup is complete.

Next, configure client directories in “Language and region”. Select the game installation directory containing the `game` subdirectory; Global also requires a complete `boot` subdirectory.

In “Login credential management”, use “Add account” to enter credentials and click “Save credentials”. Return to “Game”, choose an account with “Select login name”, and click “Log in and launch”. A new China account needs only a login name: scan the QR code on first login, and quick-login credentials are automatically saved after successful authentication.

### Interface and Global client language settings

On the “Language and region” page:

- **Launcher interface language**: the selection is saved immediately and applied to every page without restarting the launcher.
- **Global client language**: shown only when Global is selected, with 日本語, English, Français, and Deutsch available. The selection is saved immediately and takes effect on the next game launch; changes are blocked while the game is running, and switching to another game region does not clear the selection.
- **Account CDKey version**: choose JP, NA, or EU according to the account's actual license; the default is NA. This setting selects the corresponding official website, store, Mog Station, and event announcement sources; it does not change the account's license.
- **Preferred language for the official website and store**: shown only when the CDKey version is EU, with English(UK), Français, and Deutsch available. It selects the corresponding language for the official website, store, and announcements. Changes to these web preferences are saved automatically and are independent of the client language.

## Steam and Proton

### Steam-authorized accounts

If your Global account's CDKey was purchased on Steam, check “My CDKey was purchased on Steam” and ensure that the Steam client is signed in and running before logging in.

The launcher does not request or store your Steam account password (Steam is used only to verify CDKey ownership; the game login account remains your Square Enix account).

### Launching this launcher through Steam — Linux

In “Steam and Proton settings”, enable “Enable Steam launch integration” to register **FFXIV Super Launcher** as a Steam compatibility tool. Restart Steam, then select this compatibility tool in FFXIV’s properties in your Steam library. Disable the option to remove the registration. When enabled, launching through Steam automatically enters fullscreen, and clicking an input field requests the Steam virtual keyboard. If unavailable, the launcher asks you to use a keyboard; it never starts Steam just to open the keyboard. Desktop window dimensions and maximized state are retained separately.

Use **Ctrl+Q** or **Alt+F4** to quit the launcher normally; Esc dismisses cancellable dialogs. In some Gamescope sessions that pass only `dxgi=n`, directly launching the game can mix incompatible graphics DLL implementations. The launcher conditionally adds the missing D3D11 loading setting while preserving explicit user configuration. You can remove the Steam launch option `env WINEDLLOVERRIDES="dxgi,d3d11=n" %command%` previously added for this issue; retain any other custom launch options.

This feature provides a Steam launch entry; **it does not enable Steam account authentication**. Account authentication is still controlled by “My CDKey was purchased on Steam”.

This feature uses a fixed launcher script. After manually moving or renaming the AppImage, run it once to refresh the path. After an automatic update, the launcher restarts and refreshes the path automatically; no manual action is required.

### Compatibility environment / pfx — Linux

By default, the launcher uses “Proton data directory (pfx is created here automatically; a dedicated directory is recommended)” in “Steam and Proton settings”. For Global, check “Use an existing Steam pfx for Global (Steam Version)” to reuse the existing environment of an installed Global (Steam Version) client and its local game settings.

When this option is disabled, Global uses the shared environment settings used by the other two game regions. Selecting an existing Steam environment does not migrate or synchronize plugin configurations.

## Updates

### Windows folder updates

Update checks and downloads are shared with Linux, but Windows selects its own ZIP and signed update manifest. After confirmation, the launcher verifies the signature, archive hash and file inventory. A separate helper waits for the launcher to exit, replaces only program files and restarts it. Accounts, settings, plugins and logs remain intact. Previous program files are retained under `update-backups`; installation failures attempt rollback. Exit the game before updating. Native Windows end-to-end online update/restart still needs further field verification; automated replacement and rollback tests have passed.

### Linux AppImage updates

Automatic update checks are enabled by default. The launcher checks for stable releases after initial setup is complete and on each subsequent launch. You can turn off “Automatically check for updates” to the right of “Add to applications menu” in “About”; regardless of the toggle state, you can click “Check for updates” beside the version in “About”. There is no update-channel selector. AppImage updates require user confirmation. After the complete download, the manifest signature and SHA256 are verified before the original file is replaced. The updated file is saved in the same directory with the new version number in its filename, and the old version is retained as `.previous`. Steam and application-menu entries launch through `<launcher directory>/appimage-launcher`. After manually moving or renaming the AppImage, double-click it once to refresh the path automatically; Steam and application-menu entries will then continue to work. If there are multiple copies, the last one launched is used. Exit the game before updating. After installation, the interface opens automatically using the new AppImage’s absolute path, independently of application-menu registration. Accounts and settings are stored separately, and failed background update checks do not block game login.

Run an older AppImage to roll back the launcher, then turn off automatic update checks. Whether an older version supports this toggle depends on that version. When multiple AppImages share the same launch entry, the last file launched is used.

The application-menu entry is created automatically when the AppImage starts. “Add to applications menu” in “About” rebuilds the entry manually; normally, no extra action is needed. The default entry file is `~/.local/share/applications/xivlauncher-super.desktop`, honoring `XDG_DATA_HOME`. After manually moving or renaming the AppImage, run it directly once to refresh the record.

At AppImage startup, missing default configuration is created, the fixed launcher script and application-menu entry are written, and then initial setup begins. Automatic update checks run after initial setup is complete. The entry can also be rebuilt manually in “About”. After setup or adding the entry, available application-menu caches are refreshed in the background without opening a terminal window; a refresh failure does not prevent use of the launcher. The menu entry uses the absolute path to a persistently stored icon file, so the icon remains readable after the AppImage is unmounted.

### Game, Dalamud and plugin updates

| Action | Behavior |
| --- | --- |
| Log in and launch | After successful account authentication, checks game updates and, if Dalamud is enabled, its updates, then launches the game |
| Check for updates and update the game | Updates only the selected game region's client; China and Traditional Chinese do not require prior login, while Global requires account authentication without launching the game |
| Check for updates and update Dalamud | Manually checks Dalamud for the selected game region |
| Check for updates and update plugins | Checks for updates using each plugin's recorded source repository and lets the user select which updates to install |

If a Dalamud update connection times out, you can continue launching with an existing usable version or disable Dalamud and launch. Disabling is saved, so you must enable it manually later; if the existing version is missing or incompatible, skipping the update does not guarantee a successful launch.

The two Dalamud and plugin update buttons above are on the “Dalamud and plugins” page. Changes to plugin checkboxes are saved immediately and take effect on the next game launch. “Refresh plugin enablement states” and “Refresh repository status” each reread their corresponding data and clear their own operation messages. Connection-check results appear below the corresponding repository and are also recorded in the log.

Changes to plugins, repositories, client language, client paths, and the compatibility environment are blocked while the game is running; editing becomes available again after the game exits. Login credentials, which require multiple fields, still require clicking “Save credentials”.

Before synchronizing plugins between game regions, check compatibility with the destination's Dalamud API and plugin versions. Synchronization overwrites the selected destinations' data. For now, do not directly mix Traditional Chinese plugin DLLs with versions from the other game regions.

## In-game cross-data-center travel in China

Windows and Linux provide platform-specific launcher communication for **DCTravelerX** ([Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)). Enable China Dalamud, install a compatible version of DCTravelerX, and launch the game through this launcher to use in-game cross-data-center travel. This does not guarantee compatibility with ottercorp's DcTraveler or other plugins.

The frontend can close after the game starts while the backend maintains the game session, travel authorization, and Steam status. Do not terminate that backend process manually. Open the launcher again to access the frontend.

When travel authorization expires, the backend first tries the saved quick-login credentials. If QR verification is still required, open the launcher or use “Reauthorize in-game DC travel” below the travel button, then scan with the **account currently in the game**. You do not need to exit the game. After recovery, retry travel in the game; the launcher does not automatically resubmit requests with uncertain results. Cancellation, failure, or timeout ends the scan attempt; the backend waits at most five minutes.

## Storage cleanup

The About page provides three cleanup actions:

| Action | Scope and retained content |
| --- | --- |
| Clear download cache | Removes launcher-managed patch caches and download/install staging files; retains installed games, runtime components, plugins, and backups, without taking over content managed by Dalamud |
| Clear logs | Clears launcher-managed logs; coordinates clearing the current log with the logging module, skips files in use by other processes or unsafe to handle, and leaves Dalamud logs alone |
| Clear built-in browser cache | Clears HTTP, code, and shader caches for the current Electron and web-verification sessions, preserving cookies and verification state; excludes legacy standalone verification-browser directories |

Cleanup is available while the game is running. Downloads, installation, and download-cache cleanup share a cross-process lock; active related tasks block download-cache cleanup. Browser-cache cleanup is unavailable during an operation or web verification. Results show reclaimed space and skipped items; zero cleared files means there were no eligible targets to clear.

Startup also checks session records and known temporary files, reclaiming records confirmed to be stale. Active sessions and files whose safety cannot be confirmed are retained. This is not a whole-disk cleanup tool.

## Settings and credentials

The default data directory is:

```text
<launcher directory>/
├── ffxiv_cn/     # China data
├── ffxiv_tc/     # Traditional Chinese data
├── ffxiv/        # Global data
├── compatdata/  # Default shared environment, containing pfx
└── logs/        # Diagnostic logs
```

Launcher data is stored beside the AppImage on Linux or beside `FFXIVSuperLauncher.exe` on Windows. Linux application-menu entries follow `XDG_DATA_HOME`; Windows creates a desktop shortcut and does not register a Start-menu entry. Account and Dalamud data remain separate for each game region.

Credentials are stored only as plaintext in `credentials.json` inside each game-region directory. Passwords, 2FA secrets, and quick-login credentials are not encrypted. Credential files created by the launcher allow only the current user to read and write them; do not upload or share these files.

A 2FA secret is different from a six-digit verification code: the secret generates codes, while a code is entered only for the current login. Do not enter a six-digit verification code in the secret field.

## Feedback

Submit issues and feature suggestions through [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues). “About” includes a reserved “Join Discord” button, currently disabled; it will be enabled in a later update after the community is established.

When reporting an issue, please provide as much of the following as possible:

- Launcher version and operating system; on Linux, also distribution, desktop environment and Proton version.
- Current game region and whether you launch directly or through the Steam compatibility tool.
- Reproduction steps, expected behavior, actual behavior, and the time of the error.
- Necessary screenshots or relevant diagnostic logs.

The interface log records operation results in English and supports selection and copying. Users expand the log overlay manually; as entries increase, it scrolls internally without compressing the main interface. The full diagnostic log path is shown above the log bar on every page. Before submitting logs or screenshots, review them and remove sensitive content such as account information, login tickets, passwords, 2FA secrets, and QR codes.

## Building from source

To build from source or contribute to development, see the [development guide](LINUX.en.md).

## About page

“About” displays the launcher version, maintainer, and upstream acknowledgements, with links for “GitHub repository”, “Report an issue”, “Maintainer profile”, “View license”, “Third-party notices”, “Source and provenance”, and “Artwork sources”. The “Artwork sources” dialog lists three source links: the launcher icon, GenRyu, and Cinzel. Web links open in the built-in browser, which retains an “Open in system browser” button; licenses and third-party notices can be viewed offline within the launcher.

## Upstream projects and acknowledgements

This project builds on the work of the following projects and their contributors:

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher): the original XIVLauncher, including Global authentication, patching, and related implementations.
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher): China adaptation and related implementations.
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher): the China Soil branch, the primary reference when this project began.
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher): reference for the Traditional Chinese login protocol and feature design; this does not mean its entire repository is licensed under this project's license.
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud), [Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud), and [yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud): Dalamud and injection components for the corresponding game regions.
- .NET, Proton, Steamworks, Electron (including Chromium and Node.js), xdelta3, and the other third-party components used by this project and their contributors.

See [SOURCES.txt](SOURCES.txt) for detailed inheritance relationships, modification scope, and provenance evidence, and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for third-party notices.

## License

Launcher code is distributed under the [GNU GPL version 3](LICENSE). Third-party components and assets retain their own licenses and notices; they must not all be treated as GPL-licensed.

The current icon was supplied by the maintainer and comes from [PNGAAA](https://www.pngaaa.com/detail/6354760), whose page is marked “Non-commercial Use”. This is not a GPL license granted by this project for the icon, nor independent confirmation that the original copyright holder permits redistribution; see the [artwork provenance record](compliance/provenance/artwork.json).

The interface uses [GenRyu](https://github.com/ButTaiwan/genryu-font) (ButTaiwan; v2 TC/PJP) and [Cinzel](https://github.com/NDISCOVER/Cinzel) (The Cinzel Project Authors), both supplied with the SIL Open Font License 1.1. Font licenses, sources and file checksums accompany the distribution; see the [font source manifest](prototypes/electron-ui/assets/font-sources.json) for details. Open About → Artwork sources to visit the source pages for the icon and both fonts.

See the [release checklist](compliance/RELEASE-CHECKLIST.txt) for distribution materials and licensing review records.

If you believe that code, dependencies, or assets in this project infringe your rights, please contact the maintainer through GitHub Issues and provide the location of the relevant content and supporting rights information. The maintainer will investigate and discuss a resolution, removing or replacing the content where necessary. Do not submit sensitive personal information in public issues.
