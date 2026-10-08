using Avalonia.Controls;
using Avalonia.Threading;
using XIVLauncher.Common.Http;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly CheckBox wizardDalamud = new() { Content = Localization.T("启用 Dalamud"), IsChecked = false };

    private readonly Dictionary<string, TextBlock> clientVersions = new();
    private readonly TextBlock dalamudVersionText = new();
    private void RefreshVersionDisplays()
    {
        VersionRecords.Refresh(settings);
        foreach (var (region, text) in clientVersions) text.Text = "客户端版本：" + settings.RegionProfiles[region].ClientVersion;
        dalamudVersionText.Text = "Dalamud 版本：" + settings.Current.DalamudVersion;
    }
    private TextBlock ClientVersionRow(string region)
    {
        var text = new TextBlock(); clientVersions[region] = text; return text;
    }
    private DownloadScope ObserveDownloads(string label, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var region = settings.SelectedRegion;
        var english = label == "下载补丁" ? "Downloading patch" : label == "下载插件" ? "Downloading plugin" : "Downloading Dalamud";
        return new DownloadScope(value =>
        {
            launcherLog.Download(id, english, region, value, token.IsCancellationRequested);
            if (!value.Active) sessionDiagnostics.Write($"{english}: {value.File}; received={value.Received}; succeeded={value.Succeeded}; cancelled={token.IsCancellationRequested}", region, "DOWNLOAD");
        }, token);
    }
    private void EndDownloads() { lastUpdateMessage = null; }
    private string? lastUpdateMessage;
    private void UpdateMessage(string message, string region)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (message == lastUpdateMessage) return;
            lastUpdateMessage = message;
            AppendLog(message, region: region);
        });
    }
    private async Task PerformGameUpdateAsync(CancellationToken token)
    {
        var region = settings.SelectedRegion;
        using var observation = ObserveDownloads("下载补丁", token);
        try
        {
            await new RegionUpdateService(ResolveBackend).UpdateGameAsync(settings,
                message => UpdateMessage(message, region), token);
        }
        finally { EndDownloads(); RefreshVersionDisplays(); }
    }
    private async Task<RegionDalamudFiles?> PerformDalamudUpdateAsync(CancellationToken token)
    {
        if (!settings.EnableDalamud) return null;
        var region = settings.SelectedRegion;
        using var observation = ObserveDownloads("下载 Dalamud", token);
        try
        {
            return await new RegionDalamudUpdates().UpdateAsync(settings,
                message => UpdateMessage(message, region), token);
        }
        catch (Exception ex) when (LocalDalamud.IsTimeout(ex, token))
        {
            EndDownloads();
            RegionDalamudFiles? local = null; string? unavailable = null;
            try { local = await Task.Run(() => LocalDalamud.Find(settings, token), token); }
            catch (Exception check) when (check is IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            { unavailable = "本地组件检查未通过，无法使用现有 Dalamud。"; }
            token.ThrowIfCancellationRequested();
            var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 14 };
            panel.Children.Add(new TextBlock { Text = "Dalamud 更新连接超时。是否跳过本次更新？使用本地版本不能保证与当前游戏兼容。", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            if (unavailable != null) panel.Children.Add(new TextBlock { Text = unavailable });
            panel.Children.Add(new TextBlock { Text = "选择禁用后将保存设置；以后需要自行重新勾选“启用 Dalamud”。", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var dialog = new Window { Title = "Dalamud 更新超时", Width = 560, SizeToContent = SizeToContent.Height, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var useLocal = new Button { Content = "跳过更新，使用本地 Dalamud", IsEnabled = local != null };
            var disable = new Button { Content = "禁用 Dalamud 并继续启动" };
            var cancelLaunch = new Button { Content = "取消启动" };
            useLocal.Click += (_, _) => dialog.Close("local"); disable.Click += (_, _) => dialog.Close("disable"); cancelLaunch.Click += (_, _) => dialog.Close(null);
            panel.Children.Add(useLocal); panel.Children.Add(disable); panel.Children.Add(cancelLaunch);
            using var registration = token.Register(() => Dispatcher.UIThread.Post(() => dialog.Close(null)));
            var choice = await dialog.ShowDialog<string?>(this); token.ThrowIfCancellationRequested();
            if (choice == "local") return local!;
            if (choice == "disable") { LocalDalamud.Disable(settings); dalamud.IsChecked = false; return null; }
            throw new OperationCanceledException("已取消启动。", token);
        }
        finally { EndDownloads(); RefreshVersionDisplays(); }
    }
}
