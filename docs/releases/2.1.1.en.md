# FFXIV Super Launcher 2.1.1

This update focuses on launch compatibility in Steam gaming mode, in-game data center travel status queries, and first-run setup issues.

## Fixes and functional improvements

- Fixed a game crash in some Gamescope sessions caused by inconsistent D3D11 and DXGI loading settings, with or without Dalamud. The launcher adds the missing setting only when it inherits `dxgi=n`, no explicit D3D11 setting exists, and no conflicting renderer configuration is selected. Existing D3D11 or WineD3D settings are not overwritten.
- Improved order status queries for China region in-game travel through DCTravelerX: reuse the established travel session instead of reinitializing it before each query. Authorization recovery is retained, and travel orders are not automatically resubmitted. Added diagnostic logs to distinguish order submission, status queries, and authorization issues.
- Combined Steam compatibility tool registration into “Enable Steam launch integration”. When enabled, launching through Steam (on the desktop or in Gamescope) automatically enters fullscreen, and input fields can request the Steam virtual keyboard. If unavailable, the launcher asks you to use a keyboard. Steam fullscreen and desktop window dimensions are stored separately. Renamed “Compatibility tools settings” to “Steam and Proton settings”.
- Added Ctrl+Q / Alt+F4 support to quit normally and clean up sessions, including in fullscreen. Esc remains the dialog cancellation key.
- Improved first-run setup: English is the default, with separate steps for launcher language, game region, and Steam / Proton settings. Added Global client language, CDKey version, and EU account store language preferences. Selections save immediately; a save failure preserves input and blocks proceeding. Launcher updates are checked after first-run setup is complete.
- Improved automatic update dialog failure handling: prevent duplicate installation requests, retain error messages on installation failure, and allow retries. Fixed input and focus being reset during some refresh operations.
- Optimized parts of the user interface.

## Upgrade notes

- You can remove the Steam launch option `env WINEDLLOVERRIDES="dxgi,d3d11=n" %command%` previously added for this graphics DLL issue. Retain any other custom options.
- Restart Steam after changing compatibility tool registration. Existing registration settings are retained.
- Management of Proton, Dalamud, and bundled third-party components is unchanged. Pinned third-party helper components have not been replaced, and no custom controller navigation has been added. Steam Deck users can operate the launcher with the trackpads.
- The supported platform remains x86_64 Linux. Gaming mode changes have been tested on CachyOS; this does not mean all SteamOS devices, distributions, or Proton versions have been verified.
