using XIVLauncher.Linux.Taiwan;
using XIVLauncher.GamePatchV3.Update;
using XIVLauncher.GamePatchV3.Update.Models;

namespace XIVLauncher.Linux;

/// <summary>Region protocols are selected separately from UI language and shared Proton settings.</summary>
public sealed record RegionDalamudFiles(FileInfo Injector, DirectoryInfo Runtime, DirectoryInfo Assets, int Language, bool Soil, string? SupportedGameVersion = null);

public interface IRegionBackend
{
    Task UpdateGameAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token);
    Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game, CancellationToken token);
}

public static class RegionBackends
{
    private static readonly Lazy<HttpClient> TaiwanClient = new(CreateTaiwanHttp);
    internal static HttpClient TaiwanHttp => TaiwanClient.Value;
    private static HttpClient CreateTaiwanHttp()
    {
        var client = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) }) { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XIVTCLauncher/1.0"); return client;
    }
    public static IRegionBackend For(string region) => region switch
    {
        "ffxiv" => new GlobalBackend(), "ffxiv_cn" => new SdoBackend(), "ffxiv_tc" => new TaiwanBackend(),
        _ => throw new NotSupportedException("此区服尚未接入登录、游戏更新与 Dalamud 服务。")
    };

    private static readonly Lazy<HttpClient> GlobalClient = new(Global.GlobalGameUpdater.CreateClient);
    internal static HttpClient GlobalHttp => GlobalClient.Value;
    private sealed class GlobalBackend : IRegionBackend
    {
        // Boot patches are public; game patches are authenticated by GlobalLogin after environment preparation.
        public async Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game, CancellationToken token) => new() { NeedsUpdate = (await new Global.GlobalGameUpdater(GlobalHttp).CheckBootAsync(game.FullName, token)).Count != 0 };
        public Task UpdateGameAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token) => new Global.GlobalGameUpdater(GlobalHttp).UpdateBootAsync(game.FullName, root, progress, token);
    }
    private sealed class SdoBackend : IRegionBackend
    {
        public Task UpdateGameAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token) => ChinaGameUpdate.RunAsync(game, root, progress, token);
        public Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game, CancellationToken token) => GameUpdater.Check(game, false, token);
    }

    private sealed class TaiwanBackend : IRegionBackend
    {
        public async Task<GameUpdateCheckResult> CheckGameAsync(DirectoryInfo game, CancellationToken token) => new() { NeedsUpdate = (await new TaiwanGameUpdater(TaiwanHttp).CheckAsync(game.FullName, token)).Count != 0 };
        public Task UpdateGameAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token) => new TaiwanGameUpdater(TaiwanHttp).UpdateAsync(game.FullName, root, progress, token);
    }
}
