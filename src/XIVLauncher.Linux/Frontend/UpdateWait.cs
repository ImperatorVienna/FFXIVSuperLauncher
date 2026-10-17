namespace XIVLauncher.Linux.Frontend;

// Owns update cancellation: a skip result is returned only after all writes stop.
internal static class UpdateWait
{
    internal sealed record Result<T>(T? Value, string? Choice);
    internal static async Task<Result<T>> RunAsync<T>(Func<CancellationToken, Task<T>> run,
        Func<bool, CancellationToken, Task<string>> choose, Action<string> log,
        CancellationToken token, TimeSpan? interval = null)
    {
        while (true)
        {
            using var update = CancellationTokenSource.CreateLinkedTokenSource(token);
            var work = run(update.Token);
            try
            {
                while (!work.IsCompleted)
                {
                    using var timer = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var delay = Task.Delay(interval ?? TimeSpan.FromSeconds(60), timer.Token);
                    if (await Task.WhenAny(work, delay) == work) { timer.Cancel(); break; }
                    token.ThrowIfCancellationRequested();
                    using var prompt = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var decision = choose(false, prompt.Token);
                    if (await Task.WhenAny(work, decision) == work)
                    {
                        prompt.Cancel();
                        try { await decision; } catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                        break;
                    }
                    var choice = await decision;
                    log("Dalamud update decision: " + choice + ".");
                    if (choice == "retry") continue; // The active download continues; no duplicate worker.
                    update.Cancel();
                    try { await work; } catch (Exception ex) when (ex is OperationCanceledException or IOException or HttpRequestException or TimeoutException) { log("Dalamud update stopped before fallback: " + ex.GetType().Name + "."); }
                    token.ThrowIfCancellationRequested();
                    return new(default, choice);
                }
                return new(await work, null);
            }
            catch (Exception ex) when (LocalDalamud.IsTimeout(ex, token))
            {
                log("Dalamud update timed out: " + ex.GetType().Name + ".");
                var choice = await choose(true, token);
                log("Dalamud update decision: " + choice + ".");
                if (choice != "retry") return new(default, choice);
            }
            finally
            {
                update.Cancel();
                // Also drain writes when the user cancels while a prompt is open.
                try { await work; } catch { /* Observed by the paths above; preserve their result/error. */ }
            }
        }
    }
}
