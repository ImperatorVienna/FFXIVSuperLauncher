using System.Diagnostics;
using XIVLauncher.Common;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Constant;
using XIVLauncher.Common.Unix;
using XIVLauncher.Dalamud;
using XIVLauncher.GamePatchV3.Update;

namespace XIVLauncher.Linux;

// UI-independent launch flow. Keep login before updates and the single entrypoint injection path.
internal static class GameLaunchCoordinator
{
    internal static FileStream AcquireClientLock(string path) =>
        new(Path.Combine(path, ".soil-patch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    internal static async Task<bool> LaunchAsync(LinuxSettings settings, CompatibilityRunner runner,
        Func<CancellationToken, Task<string>> authenticate,
        Func<CancellationToken, Task<RegionDalamudFiles?>> prepareDalamud,
        bool noPlugins, Action<string> status, Action<CompatibilityRunner>? onStarting, CancellationToken token)
    {
        if (settings.SelectedRegion == "ffxiv_tc") _ = WebviewRuntime.FindBrowser();
        if (!GameHelpers.IsValidGamePath(settings.GamePath)) throw new InvalidOperationException(Localization.T("请选择当前游戏区服的完整客户端目录，需包含 game/ffxiv_dx11.exe 和有效版本文件。"));
        using var gameLock = AcquireClientLock(settings.GamePath);
        var gameArguments = await authenticate(token);
        status(Localization.T("正在检查 Proton 环境…"));
        await runner.DiagnoseAsync(token);
        Paths.OverrideRoamingPath(settings.RegionRoot);
        var game = new DirectoryInfo(settings.GamePath);
        var gameExe = new FileInfo(Path.Combine(game.FullName, "game/ffxiv_dx11.exe"));
        var files = await prepareDalamud(token);
        if (files == null && settings.SelectedRegion == "ffxiv_cn") files = RegionLaunchPreparation.FixedChinaHelper(AppContext.BaseDirectory);
        var injector = new UnixDalamudRunner(runner);
        ProcessStartInfo prepared;
        if (files != null)
        {
            var config = Directory.CreateDirectory(Path.Combine(settings.RegionRoot, "dalamud"));
            var logs = Directory.CreateDirectory(Path.Combine(settings.RegionRoot, "logs"));
            var plugins = Directory.CreateDirectory(Path.Combine(config.FullName, "installedPlugins"));
            prepared = await RegionInjection.For(settings.SelectedRegion).PrepareAsync(injector, files, gameExe, new DalamudStartInfo
            {
                WorkingDirectory = files.Injector.DirectoryName!, ConfigurationPath = Path.Combine(config.FullName, "dalamudConfig.json"),
                LoggingPath = logs.FullName, PluginDirectory = plugins.FullName, AssetDirectory = files.Assets.FullName,
                LauncherDirectory = AppContext.BaseDirectory, GameVersion = Repository.Ffxiv.GetVer(game), TroubleshootingPackData = "{}"
            }, settings.EnableDalamud, noPlugins, settings.RegionRoot, AppContext.BaseDirectory, token);
        }
        else prepared = runner.BuildStartInfo([gameExe.FullName], gameExe.DirectoryName, mainSession: true, protonVerb: "runinprefix");
        if (settings.SelectedRegion == "ffxiv" && settings.EnableDalamud && files?.SupportedGameVersion is { Length: > 0 } supported && supported != Patching.GamePatchFiles.Version(settings.GamePath))
            throw new CredentialValidationException("ffxiv Dalamud does not support the installed game version.", "当前国际区 Dalamud 尚不支持已安装的客户端版本。可取消“启用 Dalamud”后重新启动，或等待上游更新。");
        if (settings.SelectedRegion == "ffxiv" && settings.Current.SteamAccount)
        {
            prepared.Environment["IS_FFXIV_LAUNCH_FROM_STEAM"] = "1";
            prepared.Environment["SteamAppId"] = settings.Current.FreeTrial ? "312060" : "39210";
            prepared.Environment["SteamGameId"] = prepared.Environment["SteamAppId"];
            prepared.Environment["STEAM_COMPAT_APP_ID"] = prepared.Environment["SteamAppId"];
        }
        onStarting?.Invoke(runner);
        if (files != null)
        {
            var winePid = await injector.LaunchAsync(prepared, gameArguments, token,
                confirmationTimeout: settings.SelectedRegion == "ffxiv_tc" ? TimeSpan.FromMinutes(4) : null);
            status(settings.EnableDalamud ? Localization.F($"游戏已由注入器启动（Wine PID {winePid}）；Dalamud 加载状态请在游戏内确认。") : Localization.F($"中国区游戏已启动（Wine PID {winePid}）；未启用或更新 Dalamud。"));
        }
        else
        {
            await RegionLaunchPreparation.LaunchPlainAsync(prepared, gameArguments, token);
            status(Localization.T("已通过 Proton 提交游戏启动命令；本次未启用或更新 Dalamud。"));
        }
        return files != null;
    }
}
