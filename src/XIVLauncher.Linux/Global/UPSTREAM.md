# International service adapter

Protocol reference: `xl`, goatcorp/FFXIVQuickLauncher, commit `40ed6e93e7eb73e1c18f4d4871e05f32ab5fd2c6`.

- `GlobalLogin`: OAuth fields, Steam-bound account checks, response indices, launcher identity checksum and game arguments follow `Common/Game/Launcher.cs`. It uses cancellation, bounded HTTP timeouts and sanitized errors instead of logging response bodies. Public launcher configuration supplies the frontier referer with an official-host fallback.
- `GlobalGameUpdater`: boot manifests, boot SHA1 version report, authenticated game patch request and returned launch UID follow `Launcher.cs`. Downloads use the original patch URL, matching the upstream `PatchManager` where token URL generation is disabled. The existing shared ZiPatch installer handles both ffxiv and ffxiv_tc.
- `SteamAuthentication`: native Linux setup, official/trial app IDs and backend-confirmed ticket acquisition follow `Common.Unix/UnixSteam.cs` and `Launcher.cs`. No Steam password is requested or persisted.
- `Encryption/*`: copied upstream Ticket, CrtRand and block cipher implementation. Removed the ISteam wrapper method and verbose ticket-derived logging; used the same inline mangled Base64 transform. Source retains the repository license.
- `libsteam_api64.so`: unchanged Linux Steam API library from the same upstream tree. `goaaats.Steamworks` uses the upstream pinned 2.3.4 package, with dependency metadata included by the packaging script. No Windows launcher is restored.

OTP input/generation, account and credential storage, update downloads, ZiPatch application, Dalamud install/runtime preparation, plugin management and Proton launch remain shared. Public boot/Dalamud preparation is separate from password/OTP submission. International game update-only authenticates and installs patches without constructing a Proton runner or invoking Dalamud.
