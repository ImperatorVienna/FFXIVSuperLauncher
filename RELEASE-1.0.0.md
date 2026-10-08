# FFXIV Super Launcher 1.0.0

- AppImage-only distribution for x86_64 Linux; stable GitHub update channel and long-term signature verification.
- Startup creates missing default settings, refreshes the fixed entry script and installs the desktop entry before checking updates. Existing preferences and unfinished first-run setup are preserved.
- Updates retain the new versioned filename and a previous-version backup, then directly execute the new AppImage to reopen the launcher. No game auto-launch.
- AppImage contains the project icon, .DirIcon and desktop metadata. File-manager thumbnails depend on the desktop's AppImage support.
- Desktop menu entry uses Game category and no terminal. Cache refresh is asynchronous, optional and timeout-bounded.
- After moving or renaming an AppImage, run it once to refresh the stable entry. Re-register Steam once when migrating from direct-path versions.
- Desktop entries use a persistent absolute icon path.
- Credentials use region-scoped plaintext files only, without desktop password manager integration or storage selection controls.
- Removed AUR support; updated all four READMEs and UI messages.

Retain LICENSE, third-party notices, provenance and corresponding-source attachments when redistributing. The runtime and source attachments retain recorded component licensing qualifications.
