using Avalonia.Controls;
using Avalonia.Media;
using XIVLauncher.Login.Models;
namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly TextBlock areaInstruction = new() { TextWrapping = TextWrapping.Wrap };
    private CancellationTokenSource? areaLoadCancellation;
    private Task<LoginArea[]>? areaLoadTask;
    internal Func<CancellationToken, Task<LoginArea[]>> FetchChinaAreas { get; set; } = token => RegionLogin.For("ffxiv_cn").AreasAsync(token);

    private void ResetAreaLoading()
    {
        areaLoadCancellation?.Cancel(); areaLoadCancellation?.Dispose();
        areaLoadCancellation = null; areaLoadTask = null;
        areas = []; area.ItemsSource = null; areaInstruction.Text = "";
        areaInstruction.IsVisible = settings.SelectedRegion == "ffxiv_cn";
        if (settings.SelectedRegion != "ffxiv_cn") return;
        areaLoadCancellation = new CancellationTokenSource();
        _ = PreloadAreasAsync();
    }
    private async Task PreloadAreasAsync()
    {
        try { await LoadAreaListAsync(CancellationToken.None); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppendLog(DiagnosticLog.Format(ex), "DETAIL", "ffxiv_cn"); }
    }
    private async Task LoadAreaListAsync(CancellationToken token)
    {
        if (settings.SelectedRegion != "ffxiv_cn" || areaLoadCancellation == null) return;
        var owner = areaLoadCancellation;
        areaInstruction.Text = "正在获取游戏大区…";
        try
        {
            areaLoadTask ??= FetchChinaAreas(owner.Token);
            var result = await areaLoadTask.WaitAsync(token);
            if (owner != areaLoadCancellation || owner.IsCancellationRequested || settings.SelectedRegion != "ffxiv_cn") return;
            if (result.Length == 0) throw new IOException("The ffxiv_cn data center list is empty. Retry loading data centers.");
            var selected = area.SelectedItem as string ?? settings.AreaName;
            areas = result;
            area.ItemsSource = areas.Select(x => x.AreaName).ToArray();
            area.SelectedItem = areas.FirstOrDefault(x => x.AreaName == selected)?.AreaName;
            UpdateAreaInstruction();
        }
        catch
        {
            if (owner == areaLoadCancellation && !owner.IsCancellationRequested)
                areaInstruction.Text = "游戏大区未加载成功，请点击“登录并启动”重试。";
            throw;
        }
    }
    private void UpdateAreaInstruction()
    {
        if (settings.SelectedRegion == "ffxiv_cn")
            areaInstruction.Text = area.SelectedItem == null ? "请选择游戏大区，然后点击“登录并启动”。" : "";
    }
}
