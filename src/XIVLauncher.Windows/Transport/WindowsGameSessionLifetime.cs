using XIVLauncher.Linux.Platforms;

namespace XIVLauncher.Linux.Platforms.Windows;

// Windows-only policy: closing Electron must not release a live game's services.
internal static class WindowsGameSessionLifetime
{
    internal static async Task VerifyAsync(string root, IEnumerable<int> ports, string phase)
    {
        var log = new SessionDiagnostics(root);
        foreach (var port in ports.Distinct())
        {
            try { await WindowsLocalHttpListener.VerifyAsync(port); }
            catch (Exception ex)
            {
                log.Write($"Windows game service check failed ({phase}); PID={System.Environment.ProcessId}; port={port}; error={ex.GetType().Name}.", "ffxiv_cn", "ERROR");
                throw;
            }
            log.Write($"Windows game service ready ({phase}); PID={System.Environment.ProcessId}; port={port}.", "ffxiv_cn", "INFO");
        }
    }

    internal static async Task WaitAsync(string root, IGameRuntime game, IEnumerable<int> ports)
    {
        // A failed health check must never dispose services while the game is alive.
        try { await VerifyAsync(root, ports, "after frontend disconnect"); }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException) { }
        try { await game.WaitForExitAsync(); }
        catch (Exception ex)
        {
            new SessionDiagnostics(root).Write("Windows game wait failed; retaining services while the game is running. " + ex.GetType().Name, "shared", "WARN");
        }
        // Guard against an early-completing platform wait as well as exceptions.
        while (game.ObserveGame()) await Task.Delay(1000);
        new SessionDiagnostics(root).Write("Windows game exited; releasing local game services.", "shared", "INFO");
    }
}
