using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux;
public sealed partial class MainWindow
{
    private readonly CheckBox useSteamPrefix = new() { Content = "国际区使用 Steam 已有 pfx" };
    private readonly ComboBox steamPrefixes = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox steamPrefixPath = new();
    private readonly TextBlock steamPrefixStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool restoringSteamPrefix;
    private void BuildSteamPrefixSettings()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(useSteamPrefix); panel.Children.Add(steamPrefixes);
        panel.Children.Add(Field("Steam 兼容数据目录", BrowseField(steamPrefixPath, false, SaveSteamPrefixSelection)));
        panel.Children.Add(Button("扫描 Steam 国际区兼容环境", RefreshSteamPrefixes)); panel.Children.Add(steamPrefixStatus);
        panel.Children.Add(new TextBlock { Text = "未勾选时三端共用上方 Proton 数据目录。勾选后仅国际区直接读写 Steam 已有环境，沿用其中的本地游戏设置和角色配置；不会复制文件。更换 Proton 可能升级该环境，请勿同时通过 Steam 启动游戏或官方启动器。", TextWrapping = TextWrapping.Wrap });
        settingsPanel.Children.Add(panel);
        useSteamPrefix.IsCheckedChanged += (_, _) => SaveSteamPrefixSelection();
        steamPrefixPath.LostFocus += (_, _) => SaveSteamPrefixSelection();
        steamPrefixes.SelectionChanged += (_, _) =>
        {
            if (restoringSteamPrefix || steamPrefixes.SelectedItem is not SteamGamePrefix selected) return;
            steamPrefixPath.Text = selected.DataDirectory; SaveSteamPrefixSelection();
        };
    }
    private void RefreshSteamPrefixes()
    {
        var profile = settings.RegionProfiles["ffxiv"];
        restoringSteamPrefix = true;
        try
        {
            var found = SteamPrefixDiscovery.Discover(ProtonDiscovery.GetSteamRoots().Concat(new[] { steamPath.Text ?? "" }));
            steamPrefixes.ItemsSource = found; steamPrefixes.SelectedItem = found.FirstOrDefault(x => x.DataDirectory == profile.SteamCompatibilityData);
            steamPrefixPath.Text = profile.SteamCompatibilityData; useSteamPrefix.IsChecked = profile.UseSteamPrefix;
            steamPrefixStatus.Text = found.Count == 0 ? "未找到已初始化的 Steam 国际区环境，可手动选择。" : $"已找到 {found.Count} 个 Steam 国际区环境。";
        }
        finally { restoringSteamPrefix = false; }
    }
    private void SaveSteamPrefixSelection()
    {
        if (!settingsLoaded || restoringSteamPrefix || operation != null) return;
        var profile = settings.RegionProfiles["ffxiv"];
        var oldPath = profile.SteamCompatibilityData; var oldEnabled = profile.UseSteamPrefix;
        try
        {
            var enabled = useSteamPrefix.IsChecked == true;
            var entered = steamPrefixPath.Text?.Trim() ?? "";
            // Disabling must always allow returning to the shared environment, even if Steam was removed.
            var path = enabled ? SteamPrefixDiscovery.Normalize(entered) : entered;
            if (profile.UseSteamPrefix == enabled && profile.SteamCompatibilityData == path) return;
            profile.SteamCompatibilityData = path; profile.UseSteamPrefix = enabled; settings.Save();
            steamPrefixStatus.Text = enabled ? "国际区已使用所选 Steam 环境。" : "国际区已使用三端共用的 Proton 数据目录。";
            AppendLog(enabled ? "ffxiv Steam compatibility environment selected." : "ffxiv uses the shared compatibility environment.");
        }
        catch (Exception ex)
        {
            profile.SteamCompatibilityData = oldPath; profile.UseSteamPrefix = oldEnabled;
            restoringSteamPrefix = true; useSteamPrefix.IsChecked = profile.UseSteamPrefix; restoringSteamPrefix = false;
            steamPrefixStatus.Text = ex is CredentialValidationException validation ? validation.UserMessage : "请选择有效的 Steam compatdata 目录或其中的 pfx（需要 drive_c 和 system.reg）。";
            AppendLog(UserLogMessage.Failure(ex), "ERROR");
        }
    }
}
