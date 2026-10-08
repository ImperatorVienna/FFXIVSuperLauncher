using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private static readonly HttpClient PluginHttp = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) }) { Timeout = TimeSpan.FromSeconds(90) };
    internal Func<PluginUpdates> ResolvePluginUpdates { get; set; } = () => new(PluginHttp);
    private void BuildPluginUpdateActions()
    {
        var row = new WrapPanel();
        row.Children.Add(Button("检查并更新Dalamud", () => _ = RunOperationAsync(UpdateDalamudManuallyAsync)));
        row.Children.Add(Button("检查并更新插件", () => _ = RunOperationAsync(UpdatePluginsAsync)));
        foreach (var child in row.Children) child.Margin = new Avalonia.Thickness(0, 0, 10, 4);
        pluginPanel.Children.Add(row);
    }
    private async Task UpdateDalamudManuallyAsync(CancellationToken token)
    {
        EnsureGamesStopped();
        var region = settings.SelectedRegion;
        using var downloads = ObserveDownloads("下载 Dalamud", token);
        try
        {
            // Explicit manual update does not enable injection or change the user's launch preference.
            await new RegionDalamudUpdates().UpdateAsync(settings, message => UpdateMessage(message, region), token, manual: true);
            AppendLog("Dalamud update completed.");
        }
        finally { EndDownloads(); RefreshVersionDisplays(); }
    }
    private async Task UpdatePluginsAsync(CancellationToken token)
    {
        EnsureGamesStopped();
        if (HasPendingPluginChanges()) throw new CredentialValidationException("Apply or discard pending plugin changes before updating.", "请先应用或放弃插件启用状态的修改，再检查插件更新。");
        var dll = VersionRecords.DalamudAssembly(settings.Root, settings.SelectedRegion, settings.Current.DalamudVersion) ?? throw new IOException("No installed Dalamud was found. Update Dalamud first.");
        var version = System.Reflection.AssemblyName.GetAssemblyName(dll).Version ?? throw new IOException("Cannot read the installed Dalamud version.");
        var updater = ResolvePluginUpdates();
        var plan = await updater.CheckAsync(settings.Root, settings.SelectedRegion, version, token);
        var body = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(18) };
        body.Children.Add(new TextBlock { Text = $"可更新插件：{plan.Updates.Count}。只从各插件原安装仓库更新。", TextWrapping = TextWrapping.Wrap });
        var checks = plan.Updates.Select(update => (Update: update, Box: new CheckBox
        {
            Content = new TextBlock { Text = $"{update.Plugin.Name}：{update.Plugin.Version} → {update.Version}\n{update.Repository.GetLeftPart(UriPartial.Path)}", TextWrapping = TextWrapping.Wrap }, IsChecked = false
        })).ToArray();
        foreach (var item in checks) body.Children.Add(item.Box);
        if (plan.Problems.Count > 0)
        {
            body.Children.Add(new TextBlock { Text = "未能检查的插件：\n" + string.Join("\n", plan.Problems), TextWrapping = TextWrapping.Wrap });
            AppendLog($"Some plugin updates could not be checked ({plan.Problems.Count}). See the update dialog.", "WARN");
        }
        var dialog = new Window { Title = "选择更新插件", Width = 650, Height = 540, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var actions = new WrapPanel { Margin = new Avalonia.Thickness(18) };
        var all = new Button { Content = "全选", Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        var apply = new Button { Content = "更新所选插件", IsEnabled = false, Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        all.Click += (_, _) => { foreach (var item in checks) item.Box.IsChecked = true; };
        foreach (var item in checks) item.Box.IsCheckedChanged += (_, _) => apply.IsEnabled = checks.Any(x => x.Box.IsChecked == true);
        apply.Click += (_, _) => dialog.Close(true);
        var cancelButton = new Button { Content = "关闭" }; cancelButton.Click += (_, _) => dialog.Close(false);
        actions.Children.Add(all); actions.Children.Add(apply); actions.Children.Add(cancelButton);
        var layout = new DockPanel(); DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions); layout.Children.Add(new ScrollViewer { Content = body }); dialog.Content = layout;
        using var registration = token.Register(() => Dispatcher.UIThread.Post(() => dialog.Close(false)));
        if (!await dialog.ShowDialog<bool>(this)) return;
        token.ThrowIfCancellationRequested();
        using var observation = ObserveDownloads("下载插件", token);
        try
        {
            foreach (var item in checks.Where(x => x.Box.IsChecked == true))
            {
                await updater.InstallAsync(plan, item.Update, EnsureGamesStopped, token);
                AppendLog("Plugin updated: " + item.Update.Plugin.InternalName);
            }
        }
        finally { EndDownloads(); pluginSnapshot = null; RefreshPlugins(); }
    }
}
