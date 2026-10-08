using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace XIVLauncher.Linux;

public sealed partial class MainWindow
{
    private readonly StackPanel aboutPanel = new() { Spacing = 10 };
    private const string ProjectUrl = "https://github.com/ImperatorVienna/FFXIVSuperLauncher";

    private async Task RefreshApplicationCachesAsync()
    {
        try
        {
            foreach (var error in await Updates.DesktopIntegration.RefreshCachesAsync())
                sessionDiagnostics.Write(error, "shared", "WARN");
        }
        catch (Exception ex) { sessionDiagnostics.Write("Desktop cache refresh failed: " + DiagnosticLog.Summary(ex), "shared", "WARN"); }
    }

    private void InstallApplicationMenuEntry()
    {
        Updates.DesktopIntegration.Install(Updates.AppImageEntry.RegistrationPath,
            Updates.DesktopIntegration.DataRoot, Path.Combine(AppContext.BaseDirectory, "icon.png"));
        AppendLog("Application menu entry installed.");
    }

    private void BuildAbout()
    {
        var versionRow = new WrapPanel { ItemSpacing = 12, LineSpacing = 8 };
        versionRow.Children.Add(new TextBlock { Text = Localization.T("启动器版本：") + typeof(MainWindow).Assembly.GetName().Version?.ToString(3), VerticalAlignment = VerticalAlignment.Center });
        versionRow.Children.Add(Button("检查更新", () => CheckLauncherUpdateAsync(true)));
        if (Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
            versionRow.Children.Add(Button("添加到应用菜单", () =>
            {
                InstallApplicationMenuEntry();
                _ = RefreshApplicationCachesAsync();
            }));
        aboutPanel.Children.Add(versionRow);
        if (Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
            aboutPanel.Children.Add(new TextBlock { Text = Localization.T(Updates.AppImageEntry.Notice), TextWrapping = TextWrapping.Wrap });
        aboutPanel.Children.Add(new TextBlock { Text = Localization.T("维护者：") + "ImperatorVienna", TextWrapping = TextWrapping.Wrap });
        var links = new WrapPanel { ItemSpacing = 10, LineSpacing = 8 };
        links.Children.Add(AboutLink("GitHub 仓库", ProjectUrl));
        links.Children.Add(AboutLink("报告问题", ProjectUrl + "/issues"));
        links.Children.Add(AboutLink("维护者主页", "https://github.com/ImperatorVienna"));
        var discord = new Button { Content = Localization.T("加入 Discord"), IsEnabled = false };
        ToolTip.SetTip(discord, Localization.Display("Discord 社群尚未开放。"));
        links.Children.Add(discord);
        aboutPanel.Children.Add(links);
        aboutPanel.Children.Add(new TextBlock
        {
            Text = Localization.T("独立维护的社区衍生项目，并非 Square Enix、Shengqu Games、USERJOY 或上游项目的官方发行版本，亦未获其背书。"),
            TextWrapping = TextWrapping.Wrap
        });
        aboutPanel.Children.Add(new TextBlock { Text = Localization.T("上游与致谢"), FontSize = 18 });
        var upstreams = new WrapPanel { ItemSpacing = 10, LineSpacing = 8 };
        foreach (var (name, url) in new[]
        {
            ("XIVLauncher", "https://github.com/goatcorp/FFXIVQuickLauncher"),
            ("XIVLauncherCN", "https://github.com/ottercorp/FFXIVQuickLauncher"),
            ("XIVLauncherCN (Soil)", "https://github.com/AtmoOmen/FFXIVQuickLauncher"),
            ("XIVTCLauncher", "https://github.com/cycleapple/XIVTCLauncher"),
            ("Dalamud (goatcorp)", "https://github.com/goatcorp/Dalamud"),
            ("Dalamud (Dalamud-DailyRoutines)", "https://github.com/Dalamud-DailyRoutines/Dalamud"),
            ("Dalamud (yanmucorp)", "https://github.com/yanmucorp/Dalamud")
        }) upstreams.Children.Add(AboutLink(name, url));
        aboutPanel.Children.Add(upstreams);
        aboutPanel.Children.Add(new TextBlock
        {
            Text = Localization.T("感谢上述项目及 Avalonia、.NET、Proton、Steamworks、Electron、Chromium、Node.js、xdelta3 等组件的贡献者。详细来源和版权声明请查看第三方声明。"),
            TextWrapping = TextWrapping.Wrap
        });
        aboutPanel.Children.Add(new TextBlock
        {
            Text = Localization.T("启动器代码按 GNU GPL v3 分发，不提供担保。原作者版权声明予以保留；第三方组件和图标分别适用各自的许可，不属于统一的 GPL 授权。"),
            TextWrapping = TextWrapping.Wrap
        });
        var legal = new WrapPanel { ItemSpacing = 10, LineSpacing = 8 };
        legal.Children.Add(Button(Localization.T("查看许可证"), () => ShowAboutNoticeAsync("查看许可证", "LICENSE")));
        legal.Children.Add(Button(Localization.T("第三方声明"), () => ShowAboutNoticeAsync("第三方声明", "THIRD-PARTY-NOTICES.txt")));
        legal.Children.Add(AboutLink("源码与来源记录", ProjectUrl + "/blob/HEAD/SOURCES.txt"));
        legal.Children.Add(AboutLink("图标来源", "https://www.pngaaa.com/detail/6354760"));
        aboutPanel.Children.Add(legal);
    }

    private Button AboutLink(string title, string url) =>
        Button(Localization.T(title), () => OpenOfficialUrl(url));

    private async Task ShowAboutNoticeAsync(string title, string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, filename);
        string text;
        if (File.Exists(path)) text = await File.ReadAllTextAsync(path);
        else
        {
            using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("About." + filename)
                ?? throw new IOException("Bundled license notice is missing: " + filename);
            using var reader = new StreamReader(stream);
            text = await reader.ReadToEndAsync();
        }
        var dialog = new Window
        {
            Title = Localization.Display(title), Width = 760, Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(18),
                VerticalAlignment = VerticalAlignment.Stretch }
        };
        await dialog.ShowDialog(this);
    }
}
