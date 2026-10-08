using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using XIVLauncher.Linux.Updates;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private bool checkingLauncherUpdate;
    private readonly CancellationTokenSource launcherUpdateLifetime = new();
    private static readonly HttpClient LauncherUpdateHttp = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(15) }) { Timeout = TimeSpan.FromMinutes(15) };
    private async Task CheckLauncherUpdateAsync(bool manual)
    {
        if (checkingLauncherUpdate || !settingsLoaded) return;
        checkingLauncherUpdate = true;
        try
        {
            // A failed integration initialization is retried before any update request.
            Program.EnsureAppImageIntegration();
            var distribution = UpdateDistribution.Load();
            var service = new LauncherUpdates(LauncherUpdateHttp, distribution);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(launcherUpdateLifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var release = await service.CheckAsync(timeout.Token);
            var current = typeof(MainWindow).Assembly.GetName().Version!;
            if (release == null || release.Version <= new Version(current.Major, current.Minor, current.Build))
            {
                if (manual) await UpdateNoticeAsync(release == null ? "暂无可用发行版本。" : "当前已是最新版本。");
                return;
            }
            // Background completion must not interrupt a login or first-run wizard.
            while (!manual && (operation != null || !settings.SetupComplete)) await Task.Delay(1000, launcherUpdateLifetime.Token);
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 14 };
            var dialog = new Window { Title = Localization.Display("启动器更新"), Width = 540, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
            panel.Children.Add(new TextBlock { Text = Localization.Display("发现新版本：") + release.Version + "\n" + Localization.Display("当前版本：") + current.ToString(3), TextWrapping = TextWrapping.Wrap });
            var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
            actions.Children.Add(AboutLink("查看发行说明", release.Page));
            if (distribution.Kind == "appimage" && Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
            {
                panel.Children.Add(new TextBlock { Text = "请先退出游戏。更新安装完成后将自动打开新版启动器。", TextWrapping = TextWrapping.Wrap });
                actions.Children.Add(Button("下载并更新", async () =>
                {
                    if (operation != null) throw new IOException("Wait for the current operation to finish.");
                    EnsureGamesStopped(); dialog.Close();
                    await RunOperationAsync(async token =>
                    {
                        using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(token, launcherUpdateLifetime.Token); downloadTimeout.CancelAfter(TimeSpan.FromMinutes(15));
                        var ct = downloadTimeout.Token;
                        var update = await service.VerifyAsync(release, ct);
                        var image = UpdateInstallation.LauncherPath;
                        var downloadId = Guid.NewGuid().ToString("N");
                        var lastPercent = -1;
                        string stage;
                        try
                        {
                            stage = await service.StageAsync(update, image, percent => { if (percent == lastPercent) return; lastPercent = percent; launcherLog.Download(downloadId, "Downloading launcher", "shared", new("appimage", Path.GetFileName(image), update.Size * percent / 100, update.Size, true), false); }, ct);
                            launcherLog.Download(downloadId, "Downloading launcher", "shared", new("appimage", Path.GetFileName(image), update.Size, update.Size, false, true), false);
                        }
                        catch { launcherLog.Download(downloadId, "Downloading launcher", "shared", new("appimage", Path.GetFileName(image), update.Size * Math.Max(lastPercent, 0) / 100, update.Size, false, false), ct.IsCancellationRequested); throw; }
                        try
                        {
                            ct.ThrowIfCancellationRequested(); EnsureGamesStopped();
                            using var helper = UpdateInstallation.Start(image, stage, update.Sha256, sessionDiagnostics.FilePath, update.Version);
                            Close();
                        }
                        catch { File.Delete(stage); throw; }
                    });
                }));
            }
            actions.Children.Add(Button("稍后", () => dialog.Close())); panel.Children.Add(actions);
            await dialog.ShowDialog(this);
        }
        catch (OperationCanceledException) when (launcherUpdateLifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppendLog("Launcher update check failed: " + DiagnosticLog.Summary(ex), "WARN");
            sessionDiagnostics.Write(DiagnosticLog.Format(ex), "shared", "DETAIL");
            if (manual) await UpdateNoticeAsync("检查更新失败，请稍后重试。");
        }
        finally { checkingLauncherUpdate = false; }
    }
    private async Task UpdateNoticeAsync(string message)
    {
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        var dialog = new Window { Title = Localization.Display("启动器更新"), Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        panel.Children.Add(new TextBlock { Text = Localization.Display(message), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Button("关闭", () => dialog.Close())); await dialog.ShowDialog(this);
    }
}
