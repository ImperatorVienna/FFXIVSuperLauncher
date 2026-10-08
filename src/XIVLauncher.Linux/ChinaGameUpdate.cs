using XIVLauncher.Linux.Patching;
using XIVLauncher.Common.Http;
using XIVLauncher.Common;
using XIVLauncher.GamePatchV3;
using XIVLauncher.GamePatchV3.Update;
using XIVLauncher.GamePatchV3.Update.Models;
namespace XIVLauncher.Linux;
internal static class ChinaGameUpdate
{
    public static async Task RunAsync(DirectoryInfo game, string root, Action<string> progress, CancellationToken token)
    {
        progress("正在检查游戏更新…");
        var check = await XIVLauncher.GamePatchV3.Update.GameUpdater.Check(game, false, token);
        if (check.NeedsUpdate && check.UpdatePlan == null) throw new IOException("游戏更新计划缺失。");
        if (!check.NeedsUpdate) { progress("游戏已是最新版本。"); return; }
        var nativeWorker = Path.Combine(AppContext.BaseDirectory, "Tools/xdelta3");
        if (!File.Exists(nativeWorker)) throw new FileNotFoundException(Localization.T("缺少随包提供的 Linux 差分工具，请重新安装启动器。"), nativeWorker);
        var versionBackups = Enum.GetValues<Repository>().Select(repo => repo.GetVerFile(game).FullName)
            .Distinct().Where(File.Exists).ToDictionary(path => path, File.ReadAllBytes);
        var success = false;
        try
        {
            using var vcdiff = new VcdiffClient(nativeWorker);
            using var installer = new GamePatchInstaller();
            var downloads = DownloadScope.Current;
            await installer.InstallAsync(check.UpdatePlan, game,
                new DirectoryInfo(Path.Combine(root, "patches")), vcdiff, true,
                TimeSpan.FromMilliseconds(250), new Progress<GamePatchProgress>(p =>
                {
                    if (p.IsByteProgress) downloads?.ReportTotal((long)p.Progress, (long)p.Total);
                    if (!p.IsByteProgress) progress(p.PhaseText);
                }), token);
            success = true;
            progress("游戏更新完成。");
        }
        finally
        {
            // A cancelled parallel install must not advertise a fully updated client.
            if (!success)
                foreach (var (path, bytes) in versionBackups) File.WriteAllBytes(path, bytes);
        }
    }

}
