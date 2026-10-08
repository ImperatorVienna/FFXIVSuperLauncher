namespace XIVLauncher.Linux;

// Independent from authentication and game-patch backends. Manual updates and
// launch-time updates use the same lifecycle, with an explicit enablement policy.
public sealed class RegionDalamudUpdates(
    Func<string, string, Action<string>, CancellationToken, Task<RegionDalamudFiles>>? prepare = null)
{
    private readonly Func<string, string, Action<string>, CancellationToken, Task<RegionDalamudFiles>> prepare = prepare ?? PrepareAsync;
    private static readonly Lazy<HttpClient> China = new(() => CreateClient("XIVLauncherCN"));
    private static readonly Lazy<HttpClient> Taiwan = new(() => CreateClient("XIVTCLauncher/1.0"));
    private static readonly Lazy<HttpClient> Global = new(() => CreateClient(null));

    private static HttpClient CreateClient(string? agent)
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(20)
        }) { Timeout = TimeSpan.FromSeconds(60) };
        if (agent != null) client.DefaultRequestHeaders.UserAgent.ParseAdd(agent);
        return client;
    }

    public static Task<RegionDalamudFiles> PrepareAsync(string region, string root, Action<string> progress, CancellationToken token)
    {
        var client = region switch
        {
            "ffxiv_cn" => China.Value, "ffxiv_tc" => Taiwan.Value, "ffxiv" => Global.Value,
            _ => throw new NotSupportedException("Unknown game region.")
        };
        return new DalamudUpdateService(client, DalamudSources.For(region)).PrepareAsync(root, progress, token);
    }

    public async Task<RegionDalamudFiles?> UpdateAsync(LinuxSettings settings, Action<string> status, CancellationToken token, bool manual = false)
    {
        if (!manual && !settings.EnableDalamud) return null;
        token.ThrowIfCancellationRequested();
        var region = settings.SelectedRegion;
        var language = GameClientLanguage.For(settings);
        var files = await prepare(region, Path.Combine(settings.Root, region), status, token);
        token.ThrowIfCancellationRequested();
        settings.RegionProfiles[region].DalamudVersion = files.Injector.Directory!.Name;
        settings.Save();
        return files with { Language = language };
    }
}
