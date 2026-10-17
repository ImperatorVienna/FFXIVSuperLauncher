# FFXIV Super Launcher 2.2.0

Windows x64 and Linux x86_64 are now released together. Both share the interface and core features, with platform-specific game launching and system integration.

## Windows release

- Native Windows game launching and Dalamud injection for China, Traditional Chinese and Global clients. Initial setup skips Steam / Proton paths, and Linux compatibility settings are hidden.
- Distributed as a ZIP containing the `FFXIV Super Launcher` folder and its runtime components. Extract everything and run `FFXIVSuperLauncher.exe`; do not move the EXE alone.
- Maintains fixed launch scripts and a desktop shortcut with the launcher icon, without Start-menu registration. Run the EXE directly once after moving or renaming it to refresh the path.
- Windows self-updates download a dedicated ZIP, verify its signature and file hashes, wait for the launcher to exit, replace listed program files and retain user data. Failed installations attempt rollback.
- Windows source code is published with this release.

## Fixes and improvements

- Platform-specific client-path, containment and file-access handling fixes valid Windows client paths and existing plugin/repository data not being recognized correctly.
- Fixed the Windows backend session ending unexpectedly when the frontend closed, breaking in-game cross-data-center communication. Windows loopback listening is separated from Linux/Proton transport.
- Improved Dalamud update waiting: prolonged updates offer continued waiting, usable local components or launching without Dalamud. Background writes stop before switching, preventing update and launch operations from modifying the same files concurrently.
- Runtime and component updates are staged and validated before replacement, preserving or restoring the previous directory on failure. Phase diagnostics help distinguish network waits from installation failures.
- Fixed expiration and cancellation in shared QR login: an expired code is renewed once, and another timeout permits a fresh login attempt without deleting the entire account. This does not resolve every rejection by the official authentication service.
- Improved messages and diagnostics for login failures such as Steam not running.
- Standardized Chinese client-version terminology and improved the user interface.

## Downloads and upgrading

- Linux: `xivlauncher-super-2.2.0-x86_64.AppImage`.
- Windows: `FFXIVSuperLauncher-2.2.0-win-x64.zip`.
- Settings, credentials, plugins and logs are stored beside the launcher on both platforms. Preserve needed data when upgrading or moving. Never upload your `credentials.json`, session files or private logs to a public repository.
- Existing Linux Proton data paths are preserved, not migrated automatically. To relocate one, exit the game and launcher, move the entire `compatdata` folder and select its new path. Changing only the path creates a new environment without migrating personal game settings.
- Linux retains Steam fullscreen, virtual-keyboard and Proton settings. Windows Steam account authentication requires Steam to be running, without Proton or compatibility-tool registration.

## Validation and feedback

Players have verified game launching with earlier test packages on both platforms. QR recovery has simulated-service regression coverage. The full native Windows online update, exit and restart sequence still needs further field verification. Report issues through [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues), including OS, version, reproduction steps and diagnostic logs with sensitive data removed.
