using System.Net.Http.Headers;
using System.Text.Json;
namespace XIVLauncher.Linux.ChinaTravel;
internal static class GameRecoveryClient
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
    internal static async Task<RecoveryState> SendAsync(GameRecoveryEndpoint.Discovery target, string command, CancellationToken token, HttpClient? transport = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{target.Port}/game-auth/{command}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Token);
        using var response = await (transport ?? Http).SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var state = JsonSerializer.Deserialize<RecoveryState>(await response.Content.ReadAsStringAsync(token)) ?? throw new JsonException();
        if (state.Status is not ("ready" or "required" or "scanning") || state.Image?.Length > 2_000_000) throw new JsonException();
        return state;
    }
}
