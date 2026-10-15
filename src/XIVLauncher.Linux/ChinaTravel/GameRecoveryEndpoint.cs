using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace XIVLauncher.Linux.ChinaTravel;

// The discovery file contains only a private local capability, never login tickets.
internal sealed class GameRecoveryEndpoint : IAsyncDisposable
{
    internal sealed record Discovery(int Port, string Token, int ProcessId = 0, long StartedAt = 0);
    private readonly GameSessionAuthentication authentication;
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task serving;
    private readonly string path;
    private int disposed;
    private readonly string capability = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public GameRecoveryEndpoint(string root, int port, GameSessionAuthentication authentication)
    {
        this.authentication = authentication;
        listener.Prefixes.Add($"http://127.0.0.1:{port}/game-auth/");
        var directory = Path.Combine(root, "ffxiv_cn", "game-sessions");
        path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        try
        {
            listener.Start();
            Platforms.PlatformServices.Storage.CreatePrivateDirectory(directory);
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using (var file = Platforms.PlatformServices.Storage.OpenPrivateFile(path + ".tmp", FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(file, new Discovery(port, capability, process.Id, process.StartTime.ToUniversalTime().Ticks));
            File.Move(path + ".tmp", path);
            serving = ServeAsync();
        }
        catch
        {
            listener.Close();
            DeleteDiscovery();
            lifetime.Dispose();
            throw;
        }
    }
    private async Task ServeAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(lifetime.Token); }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or HttpListenerException) { break; }
            try
            {
                var request = context.Request;
                if (request.HttpMethod != "POST" || request.Headers["Origin"] != null || request.Headers["Sec-Fetch-Site"] != null ||
                    request.Headers["Authorization"] != "Bearer " + capability)
                { context.Response.StatusCode = 403; continue; }
                var command = request.Url!.AbsolutePath;
                RecoveryState state;
                switch (command)
                {
                    case "/game-auth/state": state = authentication.State; break;
                    case "/game-auth/start": state = authentication.StartScan(); break;
                    case "/game-auth/cancel": authentication.CancelScan(); state = authentication.State; break;
                    default: context.Response.StatusCode = 404; continue;
                }
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.OutputStream, state, cancellationToken: lifetime.Token);
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or OperationCanceledException or ObjectDisposedException) { }
            finally { context.Response.Close(); }
        }
    }
    private void DeleteDiscovery()
    {
        try { File.Delete(path); File.Delete(path + ".tmp"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Serilog.Log.Warning("[DCTravelerX] Could not remove recovery discovery file ({Type})", ex.GetType().Name); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); listener.Close();
        try { await serving; }
        finally { DeleteDiscovery(); lifetime.Dispose(); }
    }
}
