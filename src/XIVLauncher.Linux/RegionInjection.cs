using System.Diagnostics;
using XIVLauncher.Common.Unix;
using XIVLauncher.Dalamud;
using XIVLauncher.Linux.Taiwan;
namespace XIVLauncher.Linux;

// Extension boundary: a future regional method implements this interface instead
// of adding launch branches to MainWindow or changing other regions' methods.
internal interface IRegionInjection
{
    Task<ProcessStartInfo> PrepareAsync(UnixDalamudRunner runner, RegionDalamudFiles files,
        FileInfo game, DalamudStartInfo info, bool enabled, bool noPlugins,
        string regionRoot, string launcherRoot, CancellationToken token);
}
internal static class RegionInjection
{
    internal static IRegionInjection For(string region) => region switch
    {
        "ffxiv_cn" => new Entrypoint(false, true),
        "ffxiv_tc" => new Entrypoint(true, true),
        "ffxiv" => new Entrypoint(false, false),
        _ => throw new NotSupportedException("Unknown injection region.")
    };
    private sealed class Entrypoint(bool adaptTaiwan, bool includeLauncherDirectory) : IRegionInjection
    {
        public async Task<ProcessStartInfo> PrepareAsync(UnixDalamudRunner runner, RegionDalamudFiles files,
            FileInfo game, DalamudStartInfo info, bool enabled, bool noPlugins,
            string regionRoot, string launcherRoot, CancellationToken token)
        {
            if (adaptTaiwan)
                files = files with { Injector = await TaiwanEntrypoint.PrepareAsync(files.Injector, regionRoot,
                    Path.Combine(launcherRoot, "Tools/tc-entrypoint/Dalamud.Injector.dll"), token) };
            info.WorkingDirectory = files.Injector.DirectoryName!;
            var result = await runner.PrepareAsync(files.Injector, game, files.Runtime, info, enabled, noPlugins,
                token, files.Language, files.Soil, includeLauncherDirectory);
            if (adaptTaiwan) result.Environment["DALAMUD_FORCE_MINHOOK"] = "false";
            return result;
        }
    }
}
