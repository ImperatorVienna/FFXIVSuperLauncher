namespace XIVLauncher.Linux;

// Common update lifecycle. RegionBackends contains only protocol/format adapters and source selection.
public sealed class RegionUpdateService(Func<string, IRegionBackend>? provider = null)
{
    private readonly Func<string, IRegionBackend> resolve = provider ?? RegionBackends.For;
    public async Task UpdateGameAsync(LinuxSettings settings, Action<string> status, CancellationToken token)
    {
        var region = settings.SelectedRegion;
        var profile = settings.RegionProfiles[region];
        var path = profile.GamePath;
        await resolve(region).UpdateGameAsync(new DirectoryInfo(path), Path.Combine(settings.Root, region), status, token);
        token.ThrowIfCancellationRequested();
        RecordGameVersion(settings, region, path);
    }
    internal static void RecordGameVersion(LinuxSettings settings) => RecordGameVersion(settings, settings.SelectedRegion, settings.Current.GamePath);
    private static void RecordGameVersion(LinuxSettings settings, string region, string path)
    {
        var profile = settings.RegionProfiles[region];
        profile.ClientVersion = File.ReadAllText(Path.Combine(path, "game", "ffxivgame.ver")).Trim();
        profile.VersionGamePath = path;
        settings.Save();
    }
}
