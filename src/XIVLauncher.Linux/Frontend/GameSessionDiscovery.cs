using System.Text.Json;
using XIVLauncher.Linux.ChinaTravel;
namespace XIVLauncher.Linux.Frontend;
internal sealed class GameSessionDiscovery(string root, HttpClient? transport = null)
{
    private GameRecoveryEndpoint.Discovery? selected;
    internal Task CleanupAsync(CancellationToken token) => ScanAsync(token, true);
    internal Task<GameRecoveryEndpoint.Discovery?> FindAsync(CancellationToken token) => ScanAsync(token, false);
    private async Task<GameRecoveryEndpoint.Discovery?> ScanAsync(CancellationToken token, bool cleanup)
    {
        selected = null;
        var directory = Path.Combine(root, "ffxiv_cn", "game-sessions");
        if (!Directory.Exists(directory)) return null;
        if (cleanup) foreach (var path in Directory.EnumerateFiles(directory, "*.json.tmp"))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.LinkTarget == null && info.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-5))
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        // Bound concurrency, not the number of files: crashed hosts can leave stale entries.
        foreach (var batch in Directory.EnumerateFiles(directory, "*.json").Chunk(32))
        {
            token.ThrowIfCancellationRequested();
            var candidates = await Task.WhenAll(batch.Select(Check));
            selected ??= candidates.FirstOrDefault(x => x != null);
        }
        return selected;

        async Task<GameRecoveryEndpoint.Discovery?> Check(string path)
        {
            string? contents = null;
            GameRecoveryEndpoint.Discovery? discovery = null;
            try
            {
                var info = new FileInfo(path);
                if (info.LinkTarget != null || info.Length > 1024) return null;
                contents = await File.ReadAllTextAsync(path, token);
                discovery = JsonSerializer.Deserialize<GameRecoveryEndpoint.Discovery>(contents);
                if (discovery == null || discovery.Port is < 1 or > 65535 || discovery.Token?.Length != 64)
                { if (cleanup) RemoveIfUnchanged(path, contents, requireOld: true); return null; }
                if (OwnerExited(discovery))
                { if (cleanup) RemoveIfUnchanged(path, contents); return null; }
                var state = await SendAsync(discovery, "state", token);
                return state.Status is "required" or "scanning" ? discovery : null;
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or TaskCanceledException or UnauthorizedAccessException)
            {
                token.ThrowIfCancellationRequested();
                if (contents != null && (ex is JsonException ||
                    discovery?.ProcessId == 0 && ex is HttpRequestException { InnerException: System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.ConnectionRefused } }))
                    if (cleanup) RemoveIfUnchanged(path, contents, requireOld: true);
                return null;
            }
        }
    }
    private static bool OwnerExited(GameRecoveryEndpoint.Discovery entry) =>
        Processes.ProcessFacts.Identify(entry.ProcessId, entry.StartedAt) == Processes.ProcessPresence.Gone;

    private static void RemoveIfUnchanged(string path, string contents, bool requireOld = false)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.LinkTarget != null || requireOld && info.LastWriteTimeUtc > DateTime.UtcNow.AddMinutes(-5)) return;
            if (File.ReadAllText(path) == contents) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private Task<RecoveryState> SendAsync(GameRecoveryEndpoint.Discovery target, string command, CancellationToken token) =>
        GameRecoveryClient.SendAsync(target, command, token, transport);
}
