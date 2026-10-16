# FFXIV Super Launcher 2.1.2

This release improves portable storage, initial setup and application-menu integration on Linux, and fixes plugin-management and web-verification interactions.

## Fixes and improvements

- Launcher settings, credentials, plugin data and logs, along with Electron and internal-browser caches, sessions and temporary files, are now stored beside the AppImage. The launch script is also stored there and records the current executable path when the AppImage runs. The application-menu entry remains in the system application-menu directory.
- Separated application-menu entries from portable data storage. Menu entries continue to follow system directory conventions, retaining registration and cache refresh at startup.
- Improved Steam-path discovery during Linux initial setup: a single valid candidate is filled in and saved only when no path has been configured. Manual settings are preserved; multiple candidates require user selection. Global Steam authentication itself does not require a manually configured Steam path; the Linux Proton environment still needs the appropriate path.
- Fixed plugin and repository checkboxes remaining disabled after changing plugin enablement, previously requiring a page change or refresh to recover.
- Fixed the Traditional Chinese login web-verification view extending beyond its verification area. It now follows page scrolling and is clipped to the visible area.
- Separated shared functionality from platform-specific interfaces in preparation for Windows development. Pinned third-party helper components were not replaced.

## Upgrade and data-directory notes — please read

- Keep the AppImage in a writable directory. Old data is not automatically moved or deleted. To retain existing settings after upgrading, exit the game and launcher, then move the settings and required data from the old data directory (normally `~/.local/share/xivlauncher-super/`, honoring `XDG_DATA_HOME` in older versions) beside the new AppImage. Without those settings, the new version opens initial setup.
- **A Proton data directory saved in existing settings keeps its original path; it is not automatically changed to the new default.** You can leave it there. To relocate it, exit the game and launcher, move the entire `compatdata` folder, then select that `compatdata` folder in “Steam and Proton settings”, not the `pfx` subfolder. Changing only the path creates a fresh container; game settings from the original container are not transferred automatically.
- For a fresh setup, the shared Proton data directory defaults to `compatdata` beside the AppImage; the container is created when needed. Manually configured directories and existing Steam prefixes reused for Global are not forcibly relocated.
- When moving the launcher later, move any data you want to keep with it and review existing absolute-path settings. Run the AppImage directly once after moving or renaming it to refresh Steam and application-menu entries. Older launcher versions still read data according to their original rules; rolling back the executable does not automatically roll back or move settings.

## Coming next

The next release is planned to introduce a Windows version, sharing the current interface and core features while using native Windows game launching and system integration. Version 2.1.2 still ships only an x86_64 Linux AppImage; it does not include a usable Windows release package.
