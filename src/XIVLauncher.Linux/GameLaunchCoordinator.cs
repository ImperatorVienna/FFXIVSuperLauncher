using System.Diagnostics;
using XIVLauncher.Common;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Constant;
using XIVLauncher.Linux.Platforms;
using XIVLauncher.Dalamud;
using XIVLauncher.GamePatchV3.Update;

namespace XIVLauncher.Linux;

// UI-independent launch flow. Keep login before updates and the single entrypoint injection path.
internal static class GameLaunchCoordinator
{
    internal static FileStream AcquireClientLock(string path) =>
        new(Path.Combine(path, ".soil-patch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    internal static async Task<bool> LaunchAsync(LinuxSettings settings, IGameRuntime runner,
        Func<CancellationToken, Task<string>> authenticate,
        Func<CancellationToken, Task<RegionDalamudFiles?>> prepareDalamud,
        bool noPlugins, Action<string> status, Action<IGameRuntime>? onStarting, CancellationToken token)
    {
        if (settings.SelectedRegion == "ffxiv_tc") PlatformServices.EnsureWebVerificationAvailable();
        if (!GameHelpers.IsValidGamePath(settings.GamePath)) throw new InvalidOperationException(Localization.T("请选择当前游戏区服的完整客户端目录，需包含 game/ffxiv_dx11.exe 和有效版本文件。"));
        using var gameLock = AcquireClientLock(settings.GamePath);
        var gameArguments = await authenticate(token);
        await runner.DiagnoseAsync(status, token);
        Paths.OverrideRoamingPath(settings.RegionRoot);
        var game = new DirectoryInfo(settings.GamePath);
        var gameExe = new FileInfo(Path.Combine(game.FullName, "game/ffxiv_dx11.exe"));
        var files = await prepareDalamud(token);
        if (files == null) files = runner.WithoutDalamud(settings.SelectedRegion);
        DalamudStartInfo? info = null;
        if (files != null)
        {
            var config = Directory.CreateDirectory(Path.Combine(settings.RegionRoot, "dalamud"));
            var logs = Directory.CreateDirectory(Path.Combine(settings.RegionRoot, "logs"));
            var plugins = Directory.CreateDirectory(Path.Combine(config.FullName, "installedPlugins"));
            info = new DalamudStartInfo
            {
                WorkingDirectory = files.Injector.DirectoryName!, ConfigurationPath = Path.Combine(config.FullName, "dalamudConfig.json"),
                LoggingPath = logs.FullName, PluginDirectory = plugins.FullName, AssetDirectory = files.Assets.FullName,
                LauncherDirectory = AppContext.BaseDirectory, GameVersion = Repository.Ffxiv.GetVer(game), TroubleshootingPackData = "{}"
            };
        }
        var prepared = await runner.PrepareAsync(settings, files, gameExe, info, noPlugins, token);
        if (settings.SelectedRegion == "ffxiv" && settings.EnableDalamud && files?.SupportedGameVersion is { Length: > 0 } supported && supported != Patching.GamePatchFiles.Version(settings.GamePath))
            throw new CredentialValidationException("ffxiv Dalamud does not support the installed game version.", "当前国际区 Dalamud 尚不支持已安装的客户端版本。可取消“启用 Dalamud”后重新启动，或等待上游更新。");
        onStarting?.Invoke(runner);
        await runner.LaunchAsync(settings, prepared, gameArguments, files != null, status, token);
        return files != null;
    }
}
