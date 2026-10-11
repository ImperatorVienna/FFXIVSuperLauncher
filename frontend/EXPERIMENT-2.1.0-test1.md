# 2.1.0-test1: release Electron after game launch

This local experimental build is on `dev/controller-steamdeck`. It does not
include controller navigation, Steam Deck layout changes or configuration migration.

After a completed launch, the main process requests backend confirmation of a
live game process in the selected WINEPREFIX (up to ten seconds). Without that
confirmation, it retains the interface and minimizes it. Failed/cancelled launches
and update-only operations do not trigger automatic exit.

On confirmation, owned official-browser windows and the verification view close,
window placement is saved, and the backend's PID/start time is handed to a private
shell supervisor. Electron exits; the existing .NET backend closes UI services
and waits for the Proton prefix to exit. The shell keeps Steam's tracked process
and the AppImage mount alive, then exits. Secondary Steam invocations also transfer
their wait to the shell, rather than retaining another Electron instance.

The remaining process cost includes the .NET backend and a waiting shell; this is
not a claim of zero memory usage. No Steam Deck performance measurements or live
game acceptance tests have been performed. An unrecognized/hidden WINEPREFIX
causes conservative fallback to the existing minimized-interface behavior.

Validation: 474 offline .NET tests; 66 Node tests. Synthetic process fixtures check
prefix matching, completed-launch gating, process exit and shell lifetime. Tests
use isolated data and do not authenticate real accounts.

Manual acceptance: test direct and Steam launches, with and without Dalamud;
verify gameplay continues after Electron exits, Steam remains in the running
state, and the backend/supervisor exit after the game closes. Also check failed
login, verification cancellation, immediate game exit and opening the launcher
again while playing. Compare memory/CPU/GPU and frame times against 2.0.0 on the
same device and game scene. The unchanged 2.0.0 release remains available for rollback.
