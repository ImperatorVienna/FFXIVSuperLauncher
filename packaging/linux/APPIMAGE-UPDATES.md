# AppImage release and update flow

AppImage is the sole distribution format. Set VERSION when building a new release. Package without --test for the stable channel; upload as regular GitHub Releases, not prereleases. There is no channel selector in the UI.

1. Build the complete Linux bundle with scripts/package-linux.sh. This intermediate bundle also supplies corresponding source and license materials.
2. Wrap it with scripts/package-appimage.py, the pinned type-2 runtime, and packaging/linux/appimage-public.pem. The icon, .DirIcon and desktop metadata are included.
3. Sign the exact manifest bytes with scripts/sign-appimage-update.py and the matching private key kept outside the repository. Upload appimage-update.json, appimage-update.sig and the versioned AppImage to the Release. Preserve checksums and corresponding source archives in release-materials/<version> in the repository, and link to that directory from the Release notes.
4. Do not change the public key casually: existing installations need to verify the next release. Never upload private keys.

Startup creates missing default configuration, writes the stable launch script, and installs the menu entry before any update request. SetupComplete remains false until the wizard finishes. Existing settings are preserved. Desktop cache refresh is optional, asynchronous and bounded by timeouts.

The fixed entry is $XDG_DATA_HOME/xivlauncher-super/appimage-launcher (normally ~/.local/share/xivlauncher-super/appimage-launcher). Steam and desktop menu entries point to it. Each AppImage launch records its current path; after moving or renaming the file, run it once.

The updater verifies the signed manifest, size, SHA256 and image format before staging in the same directory. The helper waits for the old process to exit, rechecks hashes, keeps a .previous backup, installs the new versioned filename, switches the fixed entry and directly executes the new image after clearing obsolete AppImage environment paths. It does not start the game. Failure is recorded in the diagnostic log. To roll back, rename the backup to an executable .AppImage and run it once.

Preview/loopback support is only for explicitly built --test fixtures. scripts/test-appimage-update.py is a local fixture test and must not be confused with actual GitHub stable-channel verification.

Host graphics/desktop libraries, Steam and Proton remain external. Preserve all component notices and corresponding-source attachments. AppImage packaging does not certify every Linux distribution or resolve recorded licensing qualifications.
