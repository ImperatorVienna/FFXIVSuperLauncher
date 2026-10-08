using XIVLauncher.Linux.Patching;
using XIVLauncher.Linux.Taiwan;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XIVLauncher.Common;
using XIVLauncher.Common.Constant;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Util;
using XIVLauncher.Dalamud;
using XIVLauncher.GamePatchV3;
using XIVLauncher.GamePatchV3.Update;
using XIVLauncher.GamePatchV3.Update.Models;
using XIVLauncher.Login.Channels;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;

namespace XIVLauncher.Linux;

public sealed partial class MainWindow : Window
{
    private TabControl tabs = null!;
    public void SelectPreviewTab(int index) => tabs.SelectedIndex = index;
    private LinuxSettings settings = new();
    internal Func<string, IRegionBackend> ResolveBackend { get; set; } = RegionBackends.For;
    internal Func<string, IRegionLogin> ResolveLogin { get; set; } = RegionLogin.For;
    private const int GameTab = 0, CredentialsTab = 1, CompatibilityTab = 2, RegionTab = 3, PluginsTab = 4, AboutTab = 5;
    private readonly ComboBox proton = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox area = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private LoginArea[] areas = [];
    private readonly TextBox gamePath = new();
    private readonly TextBox tcPath = new();
    private readonly TextBox globalPath = new();
    private readonly CheckBox registerSteam = new() { Content = Localization.T("注册为 Steam 兼容性工具（可随时关闭）") };
    private readonly LogTarget registrationStatus;
    private readonly StackPanel regionPanel = new() { Spacing = 12 };
    private readonly StackPanel pluginPanel = new() { Spacing = 12 };
    private readonly StackPanel pluginRows = new() { Spacing = 6 };
    private readonly TextBlock pluginRegion = new() { TextWrapping = TextWrapping.Wrap };
    private readonly LogTarget pluginStatus;
    private readonly Dictionary<string, CheckBox> pluginChecks = new();
    private OfflinePluginList? pluginSnapshot;
    private string? pluginRegionId;
    private bool settingsLoaded;
    private bool synchronizingPlugins;
    private FileStream? instanceLock;
    private readonly StackPanel wizard = new() { Spacing = 10 };
    private readonly TextBlock wizardText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button wizardNext = new() { Content = Localization.T("下一步：兼容性工具设置") };
    private bool wizardEnvironmentStep;
    private readonly ComboBox regionChoice = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox languageChoice = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox dataPath = new();
    private readonly TextBox steamPath = new();
    private readonly TextBox account = new() { Watermark = Localization.T("盛趣账号") };
    private readonly StackPanel globalInputs = new() { Spacing = 8 };
    private readonly CheckBox steamAccount = new() { Content = "我的CDKey来自Steam" };
    private readonly CheckBox freeTrial = new() { Content = "免费试玩账号" };
    private readonly StackPanel taiwanInputs = new() { Spacing = 8 };
    private readonly CheckBox autoOneTimePassword = new() { Content = "使用已保存的 2FA 密钥自动生成验证码" };
    private Control areaField = null!;
    private readonly CheckBox dalamud = new() { Content = Localization.T("启用 Dalamud"), IsChecked = false };
    private readonly CheckBox noPlugins = new() { Content = Localization.T("本次禁用所有插件（故障排查）") };
    private readonly LogTarget loginStatus;
    private readonly LogTarget steamStatus;
    private readonly LogTarget regionStatus;
    private readonly TextBlock protonCount = new() { TextWrapping = TextWrapping.Wrap };
    private bool loginVerificationStarted;
    private LogTarget? operationStatus;
    private LogTarget status => operationStatus ?? (tabs?.SelectedIndex switch { CompatibilityTab => steamStatus, RegionTab => regionStatus, PluginsTab => pluginStatus, _ => loginStatus });
    private readonly StackPanel loginInputs = new() { Spacing = 12 };
    private readonly LogTarget details;
    private readonly Image qr = new() { Width = 220, Height = 220, IsVisible = false };
    private readonly Button launch = new() { Content = Localization.T("登录并启动") };
    private readonly Button cancel = new() { Content = Localization.T("取消操作"), IsEnabled = false };
    private readonly StackPanel settingsPanel = new() { Spacing = 12 };
    private readonly StackPanel loginPanel = new() { Spacing = 12 };
    private CancellationTokenSource? operation;
    private Bitmap? qrBitmap;

    public MainWindow(string? initialGamePath = null)
    {
        loginStatus = steamStatus = regionStatus = pluginStatus = new LogTarget(message => AppendLog(message));
        registrationStatus = new LogTarget(message => AppendLog(message, region: "shared"));
        details = new LogTarget(message => AppendLog(message, level: "DETAIL"));
        details.Text = "Launcher session started.";
        Title = "FFXIV Super Launcher";
        Width = 860; Height = 860; MinWidth = 640; MinHeight = 650;
        ObserveWindowPlacement();
        var root = new StackPanel { Margin = new Thickness(28), Spacing = 18 };
        root.Children.Add(new TextBlock { Text = "FFXIV Super Launcher", FontSize = 28, FontWeight = FontWeight.SemiBold });

        regionChoice.ItemsSource = LinuxSettings.Regions.Select(r => new ComboBoxItem { Content = r.Name }).ToArray();
        regionChoice.SelectedIndex = 0;
        languageChoice.ItemsSource = Localization.Languages.Select(x => x.Name).ToArray();
        languageChoice.SelectedIndex = Array.FindIndex(Localization.Languages, x => x.Id == Localization.Language);
        regionPanel.Children.Add(Field(Localization.T("启动器界面语言"), LanguageApplyRow(languageChoice, ApplyLauncherLanguage)));
        var regionRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        regionRow.Children.Add(Field(Localization.T("游戏区服"), regionChoice));
        globalClientLanguage.ItemsSource = GameClientLanguage.Choices.Select(c => c.Name).ToArray();
        globalClientLanguage.SelectedIndex = 1;
        globalClientLanguageField = Field("国际区客户端语言", LanguageApplyRow(globalClientLanguage, ApplyClientLanguage));
        globalClientLanguageField.Margin = new Thickness(12, 0, 0, 0);
        globalClientLanguageField.IsVisible = false;
        Grid.SetColumn(globalClientLanguageField, 1);
        regionRow.Children.Add(globalClientLanguageField);
        regionPanel.Children.Add(regionRow);
        InitializeInternationalWebSettings();
        regionPanel.Children.Add(Field(Localization.T("中国区客户端目录（包含 game 文件夹）"), BrowseField(gamePath, false, SaveClientDirectories)));
        regionPanel.Children.Add(ClientVersionRow("ffxiv_cn"));
        regionPanel.Children.Add(Field(Localization.T("繁中区客户端目录（包含 game 文件夹）"), BrowseField(tcPath, false, SaveClientDirectories)));
        regionPanel.Children.Add(ClientVersionRow("ffxiv_tc"));
        regionPanel.Children.Add(Field(Localization.T("国际区客户端目录（包含 game 文件夹）"), BrowseField(globalPath, false, SaveClientDirectories)));
        regionPanel.Children.Add(ClientVersionRow("ffxiv"));
        settingsPanel.Children.Add(Field(Localization.T("Proton版本"), proton));
        settingsPanel.Children.Add(protonCount);
        var toolButtons = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        toolButtons.Children.Add(Button(Localization.T("刷新已安装的Proton"), () => RefreshProton()));
        toolButtons.Children.Add(Button(Localization.T("手动添加 Proton…"), async () => await AddProtonAsync()));
        settingsPanel.Children.Add(toolButtons);
        settingsPanel.Children.Add(Field(Localization.T("Steam 安装目录（用于手动添加工具）"), BrowseField(steamPath, false, ApplyCompatibilitySettings)));
        settingsPanel.Children.Add(Field(Localization.T("Proton 数据目录（自动在此创建 pfx，建议独立使用）"), BrowseField(dataPath, false, ApplyCompatibilitySettings)));
        var actions = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        actions.Children.Add(Button(Localization.T("环境检测"), () => _ = RunOperationAsync(DiagnoseAsync)));

        BuildSteamPrefixSettings();
        settingsPanel.Children.Add(registerSteam);
        if (Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
            settingsPanel.Children.Add(new TextBlock { Text = Localization.T(Updates.AppImageEntry.Notice), TextWrapping = TextWrapping.Wrap });

        settingsPanel.Children.Add(actions);


        loginPanel.Children.Add(loginInputs);
        var accountRow = new DockPanel { LastChildFill = true };
        chooseAccount.Click += async (_, _) => await ChooseAccountAsync();
        var manageAccounts = chooseAccount;
        DockPanel.SetDock(manageAccounts, Dock.Right); accountRow.Children.Add(manageAccounts); accountRow.Children.Add(account);
        loginPanel.Children.Insert(0, Field("登录名", accountRow));
        var areaRow = new DockPanel { LastChildFill = true };
        var travelButton = Button("超域传送", async () => await OpenChinaTravelAsync());
        DockPanel.SetDock(travelButton, Dock.Right); areaRow.Children.Add(travelButton); areaRow.Children.Add(area);
        areaField = Field(Localization.T("游戏大区"), areaRow); loginInputs.Children.Add(areaField);
        globalInputs.Children.Add(steamAccount); globalInputs.Children.Add(new TextBlock { Text = Localization.T("通过 Steam 购买 CDKey 的账户必须勾选此项。登录前请保证 Steam 客户端已经登录并正在运行中，否则无法登录游戏。"), TextWrapping = TextWrapping.Wrap }); globalInputs.Children.Add(freeTrial); loginInputs.Children.Add(globalInputs);
        taiwanInputs.Children.Add(autoOneTimePassword);
        loginInputs.Children.Add(taiwanInputs); taiwanInputs.IsVisible = false;
        loginInputs.Children.Add(dalamud); loginInputs.Children.Add(noPlugins);
        pluginPanel.Children.Add(pluginRegion);
        pluginPanel.Children.Add(dalamudVersionText);
        BuildPluginUpdateActions();
        pluginPanel.Children.Add(new TextBlock { Text = Localization.T("在不同游戏区服间同步插件设置"), FontWeight = FontWeight.SemiBold });
        var syncButtons = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var content in Enum.GetValues<RegionSyncContent>())
        {
            var button = Button(SyncLabel(content), () => _ = RunOperationAsync(token => SynchronizeRegionAsync(content, token)));
            button.Margin = new Thickness(0, 0, 10, 8);
            syncButtons.Children.Add(button);
        }
        pluginPanel.Children.Add(syncButtons);

        pluginPanel.Children.Add(new TextBlock { Text = Localization.T("勾选启用、取消勾选禁用，点击应用后保存，下次启动游戏生效。横线表示受插件集合条件控制；不修改则保留原规则。"), TextWrapping = TextWrapping.Wrap });
        pluginPanel.Children.Add(new TextBlock { Text = Localization.T("手动启用会加入默认集合；禁用会关闭此插件在所有集合中的启用项。不会删除插件或插件设置。"), TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        var pluginActions = new WrapPanel { ItemSpacing = 12, LineSpacing = 6 };
        pluginActions.Children.Add(Button(Localization.T("应用插件启用状态"), () => _ = RunPluginOperationAsync()));
        pluginActions.Children.Add(Button("刷新插件启用状态", RefreshPluginStatesAsync));
        pluginActions.Children.Add(Button(Localization.T("放弃修改"), RefreshPlugins));
        pluginPanel.Children.Add(pluginActions); pluginPanel.Children.Add(pluginRows);
        var loginLayout = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
        officialPanel.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(officialPanel, 1);
        loginLayout.Children.Add(loginPanel); loginLayout.Children.Add(officialPanel);
        credentialManager = new AccountManagerPanel(LinuxSettings.DataRoot, CredentialsSaved, busy => { if (tabs != null) tabs.IsEnabled = !busy; });
        tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = Localization.T("游戏"), Content = loginLayout },
                new TabItem { Header = "登录凭据管理", Content = credentialManager },
                new TabItem { Header = Localization.T("兼容性工具设置"), Content = settingsPanel },
                new TabItem { Header = Localization.T("语言和区服"), Content = regionPanel },
                new TabItem { Header = Localization.T("Dalamud和插件"), Content = pluginPanel },
                new TabItem { Header = Localization.T("关于"), Content = aboutPanel }
            }
        };
        tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source != tabs) return;
            diagnosticPath.IsVisible = tabs.SelectedIndex == AboutTab;
            if (tabs.SelectedIndex == CredentialsTab && settingsLoaded) credentialManager.RefreshCurrentRegion();
            if (tabs.SelectedIndex == PluginsTab && settingsLoaded && (pluginSnapshot == null || pluginRegionId != SelectedPluginRegion())) RefreshPlugins();
        };
        wizard.Children.Add(wizardText); wizard.Children.Add(wizardDalamud);
        wizard.Children.Add(new TextBlock { Text = Localization.T("Dalamud 功能违反游戏用户协议，使用后果由用户自行承担。"), TextWrapping = TextWrapping.Wrap });
        if (Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
            wizard.Children.Add(new TextBlock { Text = Localization.T(Updates.AppImageEntry.Notice), TextWrapping = TextWrapping.Wrap });
        wizard.Children.Add(wizardNext); wizard.IsVisible = false;
        root.Children.Add(wizard); root.Children.Add(tabs);
        wizardNext.Click += (_, _) =>
        {
            try
            {
                if (!wizardEnvironmentStep)
                {
                    settings.Language = Localization.Languages[Math.Clamp(languageChoice.SelectedIndex, 0, 3)].Id; settings.Save();
                    wizardEnvironmentStep = true; tabs.SelectedIndex = CompatibilityTab;
                    wizardText.Text = Localization.T("第二步：确认自动发现的 Steam / Proton。未发现时请手动选择；注册 Steam 兼容性工具为可选项。");
                    wizardNext.Content = Localization.T("完成配置");
                }
                else
                {
                    GetRunner(); dalamud.IsChecked = wizardDalamud.IsChecked == true; SaveSettings(); settings.SetupComplete = true; settings.Save();
                    wizard.IsVisible = false; launch.IsEnabled = CanLaunch; tabs.SelectedIndex = GameTab;
                    _ = RefreshApplicationCachesAsync();
                    status.Text = "Setup completed. Configure the selected game region client directory before launching.";
                }
            }
            catch (Exception ex) { status.Text = Localization.T("配置未完成"); details.Text = DiagnosticLog.Format(ex); }
        };
        BuildAbout();

        account.LostFocus += (_, _) => { if (settingsLoaded && operation == null) { ApplyTypedChinaAccount(); GuideUnknownAccount(); _ = RefreshOtpDefaultAsync(); } };
        autoOneTimePassword.IsCheckedChanged += (_, _) => otpDefaultRevision++;
        launch.Click += (_, _) => _ = RunOperationAsync(LaunchAsync, clearLoginInputs: true);
        cancel.Click += (_, _) => { operation?.Cancel(); status.Text = Localization.T("正在取消；已启动的游戏不会被终止。"); };
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(launch); buttons.Children.Add(Button(Localization.T("检查并更新游戏"), () => _ = RunOperationAsync(UpdateGameAsync, clearLoginInputs: true))); buttons.Children.Add(cancel);
        foreach (var button in buttons.Children) button.Margin = new Thickness(0, 0, 10, 6);
        loginPanel.Children.Add(buttons);
        Content = WithLogPanel(root);
        tabs.SelectionChanged += (_, _) => gamePromptHost.IsVisible = tabs.SelectedIndex == GameTab;
        area.SelectionChanged += (_, _) => UpdateAreaInstruction();
        proton.SelectionChanged += (_, _) =>
        {
            if (proton.SelectedItem is ProtonInstallation selected &&
                (string.IsNullOrWhiteSpace(steamPath.Text) || selected.Script != settings.ProtonScript)) steamPath.Text = selected.SteamRoot;
            if (proton.SelectedItem != null) ApplyCompatibilitySettings();
        };
        Opened += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(LinuxSettings.DataRoot);
                instanceLock = new FileStream(Path.Combine(LinuxSettings.DataRoot, ".launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                settings = LinuxSettings.Load(); settingsLoaded = true;
                RestoreWindowPlacement();
                languageChoice.SelectedIndex = Array.FindIndex(Localization.Languages, x => x.Id == settings.Language);
                gamePath.Text = settings.RegionProfiles["ffxiv_cn"].GamePath;
                if (initialGamePath != null) settings.GamePath = initialGamePath;
                if (settings.SelectedRegion == "ffxiv_cn" && initialGamePath != null) gamePath.Text = initialGamePath;
                tcPath.Text = settings.RegionProfiles["ffxiv_tc"].GamePath;
                globalPath.Text = settings.RegionProfiles["ffxiv"].GamePath;
                globalClientLanguage.SelectedIndex = Array.FindIndex(GameClientLanguage.Choices, c => c.Value == GameClientLanguage.Validate(settings.RegionProfiles["ffxiv"].ClientLanguage));
                dataPath.Text = settings.CompatibilityData;
                steamPath.Text = settings.SteamRoot; account.Text = settings.Account; dalamud.IsChecked = settings.EnableDalamud;
                regionChoice.SelectedIndex = Array.FindIndex(LinuxSettings.Regions, r => r.Id == settings.SelectedRegion);
                UpdateLoginRegion();
                RefreshVersionDisplays();
                RefreshProton();
                RefreshSteamPrefixes();
                var registeredRoot = string.IsNullOrEmpty(settings.RegisteredSteamRoot) ? steamPath.Text ?? "" : settings.RegisteredSteamRoot;
                registerSteam.IsChecked = SteamToolRegistration.IsRegistered(registeredRoot);
                if (registerSteam.IsChecked == true) settings.RegisteredSteamRoot = registeredRoot;
                if (!settings.SetupComplete)
                {
                    wizard.IsVisible = true; wizardText.Text = Localization.T("欢迎使用 FFXIV Super Launcher。第一步：选择启动器界面语言及游戏区服，国际区请同时选择账户CDKey版本；客户端路径可稍后填写。");
                    tabs.SelectedIndex = RegionTab; launch.IsEnabled = false;
                }
                else tabs.SelectedIndex = GameTab;
                EnableRegionAutoSave();
                EnableCompatibilityAutoSave();
                if (!Environment.GetCommandLineArgs().Contains("--render-preview")) _ = CheckLauncherUpdateAsync(false);
            }
            catch (Exception ex)
            {
                settingsLoaded = false; launch.IsEnabled = false; settingsPanel.IsEnabled = false; regionPanel.IsEnabled = false;
                status.Text = Localization.T("无法加载配置或已有启动器正在运行；未覆盖原设置。"); details.Text = DiagnosticLog.Format(ex);
            }
        };
        regionChoice.SelectionChanged += (_, _) =>
        {
            if (!settingsLoaded || operation != null) return;
            var selected = SelectedPluginRegion(); if (selected == settings.SelectedRegion) return;
            SaveSettings(false); settings.SelectedRegion = selected; settings.Save();
            ResetChinaTravel(); UpdateLoginRegion(); RefreshVersionDisplays(); pluginSnapshot = null;
            status.Text = "Selected game region: " + selected;
        };
        Closing += (_, e) =>
        {
            if (credentialManager.Busy) { e.Cancel = true; return; }
            CaptureWindowPlacement();
            operation?.Cancel();
            if (synchronizingPlugins)
            {
                e.Cancel = true;
                status.Text = Localization.T("正在取消同步并还原，请稍后再关闭窗口。");
            }
        };
        Closed += (_, _) => { launcherUpdateLifetime.Cancel(); SaveWindowPlacement(); credentialManager.ClearDraftSecrets(); ResetChinaTravel(); areaLoadCancellation?.Cancel(); areaLoadCancellation?.Dispose(); newsCancellation?.Cancel(); qrBitmap?.Dispose(); instanceLock?.Dispose(); };
    }

    private static Control Field(string label, Control content)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label }); panel.Children.Add(content);
        return panel;
    }

    private Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { ReportFailure(ex); } };
        return button;
    }
    private Button Button(string text, Action action)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { ReportFailure(ex); }
        };
        return button;
    }

    private Control BrowseField(TextBox box, bool file, Action? selectedAction = null)
    {
        var row = new DockPanel { LastChildFill = true };
        var browse = Button(Localization.T("浏览…"), async () =>
        {
            try
            {
                if (file)
                {
                    var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
                    if (selected.FirstOrDefault()?.TryGetLocalPath() is { } path) { box.Text = path; selectedAction?.Invoke(); }
                }
                else
                {
                    var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
                    if (selected.FirstOrDefault()?.TryGetLocalPath() is { } path) { box.Text = path; selectedAction?.Invoke(); }
                }
            }
            catch (Exception ex) { ReportFailure(ex); }
        });
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); row.Children.Add(box);
        return row;
    }

    private void RefreshProton()
    {
        var selected = (proton.SelectedItem as ProtonInstallation)?.Script ?? settings.ProtonScript;
        var tools = ProtonDiscovery.Discover().ToList();
        if (string.IsNullOrWhiteSpace(steamPath.Text)) steamPath.Text = ProtonDiscovery.GetSteamRoots().FirstOrDefault() ?? "";
        if (!string.IsNullOrWhiteSpace(steamPath.Text))
            tools = tools.Concat(ProtonDiscovery.Discover([steamPath.Text], [])).DistinctBy(x => x.Script).ToList();
        if (!string.IsNullOrWhiteSpace(selected) && File.Exists(selected) && tools.All(x => x.Script != selected))
            tools.Add(ProtonDiscovery.FromScript(selected, steamPath.Text ?? ""));
        proton.ItemsSource = tools;
        proton.SelectedItem = tools.FirstOrDefault(x => x.Script == selected) ?? tools.FirstOrDefault(x => x.IsReady && x.Name.Contains("cachyos", StringComparison.OrdinalIgnoreCase)) ?? tools.FirstOrDefault();
        protonCount.Text = tools.Count == 0 ? Localization.T("未找到 Proton。请在 Steam 安装 Proton，或手动添加已有安装。") : Localization.F($"已找到 {tools.Count} 个 Proton。");
    }

    private async Task AddProtonAsync()
    {
        try
        {
            var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Localization.T("选择 Proton 目录中的 proton 脚本") });
            if (selected.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            var tool = ProtonDiscovery.FromScript(path, steamPath.Text ?? "");
            var tools = ((IEnumerable<ProtonInstallation>?)proton.ItemsSource ?? []).Where(x => x.Script != tool.Script).Append(tool).ToList();
            proton.ItemsSource = tools; proton.SelectedItem = tool;
        }
        catch (Exception ex) { ReportFailure(ex); }
    }

    private ProtonInstallation SelectedProton() => proton.SelectedItem as ProtonInstallation ?? throw new InvalidOperationException(Localization.T("请先选择 Proton。"));

    private void SaveSettings(bool applyRegistration = true)
    {
        if (!settingsLoaded) throw new InvalidOperationException(Localization.T("设置尚未安全加载。"));
        settings.RegionProfiles["ffxiv_cn"].GamePath = gamePath.Text?.Trim() ?? "";
        settings.RegionProfiles["ffxiv_tc"].GamePath = tcPath.Text?.Trim() ?? "";
        settings.RegionProfiles["ffxiv"].GamePath = globalPath.Text?.Trim() ?? "";
        foreach (var profile in settings.RegionProfiles.Values) VersionRecords.InitializeClient(profile);
        settings.CompatibilityData = dataPath.Text?.Trim() ?? "";
        AccountProfiles.Capture(settings.Current);
        settings.Account = account.Text?.Trim() ?? "";
        settings.EnableDalamud = dalamud.IsChecked == true;
        if (settings.SelectedRegion == "ffxiv_cn") settings.Current.RememberPassword = true;
        if (settings.SelectedRegion != "ffxiv_cn") settings.Current.AutoOtp = autoOneTimePassword.IsChecked == true;
        if (settings.SelectedRegion == "ffxiv") { settings.Current.SteamAccount = steamAccount.IsChecked == true; settings.Current.FreeTrial = freeTrial.IsChecked == true; SaveInternationalWebSettings(); }
        settings.AreaName = area.SelectedItem as string ?? settings.AreaName;
        settings.SteamRoot = steamPath.Text?.Trim() ?? "";
        if (Directory.Exists(settings.SteamRoot))
            settings.SteamRoot = new DirectoryInfo(settings.SteamRoot).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(settings.SteamRoot);
        if (Directory.Exists(settings.RegisteredSteamRoot))
            settings.RegisteredSteamRoot = new DirectoryInfo(settings.RegisteredSteamRoot).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(settings.RegisteredSteamRoot);
        settings.ProtonScript = (proton.SelectedItem as ProtonInstallation)?.Script ?? settings.ProtonScript;
        if (applyRegistration)
        {
            if (registerSteam.IsChecked == true)
            {
                SteamToolRegistration.Install(settings.SteamRoot, Updates.AppImageEntry.RegistrationPath);
                if (!string.IsNullOrEmpty(settings.RegisteredSteamRoot) && settings.RegisteredSteamRoot != settings.SteamRoot)
                    SteamToolRegistration.Remove(settings.RegisteredSteamRoot);
                settings.RegisteredSteamRoot = settings.SteamRoot;
            }
            else if (!string.IsNullOrEmpty(settings.RegisteredSteamRoot))
            {
                SteamToolRegistration.Remove(settings.RegisteredSteamRoot); settings.RegisteredSteamRoot = "";
            }
            registrationStatus.Text = registerSteam.IsChecked == true ? Localization.T("已注册。请重启 Steam 后选择 FFXIV Super Launcher。") : Localization.T("未注册；如刚关闭，请重启 Steam 并重新选择原兼容工具。");
        }
        settings.Save();
        if (applyRegistration) status.Text = "Compatibility tool settings saved.";
    }

    private CompatibilityRunner GetRunner()
    {
        SaveSettings(false);
        var selected = ProtonDiscovery.FromScript(SelectedProton().Script, settings.SteamRoot);
        if (settings.SelectedRegion == "ffxiv" && settings.Current.UseSteamPrefix) SteamPrefixDiscovery.EnsureIdle(settings.Current.SteamCompatibilityData);
        var runner = new CompatibilityRunner(settings.GetCompatibility(selected));
        runner.Validate();
        return runner;
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> action, bool clearLoginInputs = false)
    {
        if (operation != null || credentialManager.Busy) return;
        if (clearLoginInputs) ApplyTypedChinaAccount();
        if (clearLoginInputs && GuideUnknownAccount()) return;
        if (clearLoginInputs) await RefreshOtpDefaultAsync();
        if (operation != null || credentialManager.Busy) return;
        loginVerificationStarted = false;
        operationStatus = status;
        operation = new CancellationTokenSource();
        launch.IsEnabled = false; cancel.IsEnabled = true;
        chooseAccount.IsEnabled = false; credentialManager.IsEnabled = false; settingsPanel.IsEnabled = false; loginInputs.IsEnabled = false; regionPanel.IsEnabled = false; pluginPanel.IsEnabled = false; details.Text = "";
        try { await action(operation.Token); }
        catch (LaunchUnconfirmedException ex) { status.Text = Localization.T("启动状态未确认，请查看游戏窗口；Dalamud 是否加载请在游戏内确认。"); details.Text = DiagnosticLog.Format(ex); }
        catch (OperationCanceledException ex) { status.Text = operation.IsCancellationRequested ? "Operation cancelled." : UserLogMessage.Failure(ex); details.Text = DiagnosticLog.Format(ex); }
        catch (Exception ex) { ReportFailure(ex); }
        finally
        {
            EndDownloads();
            operation.Dispose(); operation = null; operationStatus = null;
            launch.IsEnabled = settingsLoaded && settings.SetupComplete && CanLaunch; cancel.IsEnabled = false;
            chooseAccount.IsEnabled = settingsLoaded; credentialManager.IsEnabled = settingsLoaded;  settingsPanel.IsEnabled = settingsLoaded; loginInputs.IsEnabled = true; regionPanel.IsEnabled = settingsLoaded;
            pluginPanel.IsEnabled = settingsLoaded;
            qr.Source = null; qrBitmap?.Dispose(); qrBitmap = null; qr.IsVisible = false; authInstruction.Text = "";
            if (clearLoginInputs) account.Text = settings.Account;
        }
    }

    private void UpdateLoginRegion()
    {
        credentialInstruction.Text = "";
        var taiwan = settings.SelectedRegion == "ffxiv_tc";
        var international = settings.SelectedRegion == "ffxiv";
        globalInputs.IsVisible = international; steamAccount.IsChecked = settings.Current.SteamAccount; freeTrial.IsChecked = settings.Current.FreeTrial;
        account.Text = settings.Account;
        areaField.IsVisible = settings.SelectedRegion == "ffxiv_cn"; taiwanInputs.IsVisible = taiwan || international;
        account.Watermark = international ? "Square Enix 账号（不是电子邮件地址）" : taiwan ? "繁中区电子邮件账号" : "盛趣账号";
        dalamud.IsChecked = settings.EnableDalamud;
        _ = RefreshOtpDefaultAsync(force: true);
        ResetAreaLoading(); wizardDalamud.IsChecked = settings.EnableDalamud;
        globalClientLanguageField.IsVisible = settings.SelectedRegion == "ffxiv";
        RestoreInternationalWebSettings();
        loginInputs.IsVisible = CanLaunch;
        launch.IsEnabled = settingsLoaded && settings.SetupComplete && CanLaunch;
        
        credentialManager.SwitchRegion(settings.SelectedRegion);
        _ = RefreshOfficialAsync();
    }
    private async Task<string> ReadLoginPasswordAsync(bool needsPassword, CancellationToken token)
    {
        if (!needsPassword) return "";
        var value = await RegionCredentials.PasswordForLoginAsync(settings, "", token);
        if (string.IsNullOrEmpty(value)) GuideMissingCredentials();
        if (string.IsNullOrEmpty(value)) throw new CredentialValidationException("No saved password in the selected credential storage.", "当前账号没有可用密码，请到“登录凭据管理”选项卡保存账号密码。");
        return value;
    }

    private async Task DiagnoseAsync(CancellationToken token)
    {
        var runner = GetRunner();
        status.Text = Localization.T("正在准备 Proton 环境，首次运行可能需要几分钟…");
        details.Text = await runner.DiagnoseAsync(token);
        status.Text = Localization.T("环境检测通过。此检测不代表游戏或插件已通过实机验证。");
    }

    private async Task LoadAreasAsync(CancellationToken token)
    {
        if (areaLoadTask?.IsCompleted == true) areaLoadTask = null;
        await LoadAreaListAsync(token);
    }

    private async Task LaunchAsync(CancellationToken token)
    {
        settings.EnsureRegionAvailable();
        SaveSettings(false);
        credentialInstruction.Text = "";
        if (settings.SelectedRegion != "ffxiv_cn") _ = await ReadLoginPasswordAsync(true, token);
        if (settings.SelectedRegion == "ffxiv_cn")
        {
            if (areas.Length == 0) await LoadAreasAsync(token);
            if (area.SelectedItem == null)
            {
                areaInstruction.Text = "请选择游戏大区，然后再次点击“登录并启动”。";
                status.Text = "Select a data center, then click sign in and launch again.";
                return;
            }
        }
        var runner = GetRunner();
        if (settings.SelectedRegion == "ffxiv_tc") _ = WebviewRuntime.FindBrowser();
        if (!GameHelpers.IsValidGamePath(settings.GamePath)) throw new InvalidOperationException(Localization.T("请选择当前游戏区服的完整客户端目录，需包含 game/ffxiv_dx11.exe 和有效版本文件。"));
        using var gameLock = AcquireGameLock(settings.GamePath);
        var gameArguments = await AuthenticateRegionAsync(token);
        status.Text = Localization.T("正在检查 Proton 环境…");
        await runner.DiagnoseAsync(token);
        Paths.OverrideRoamingPath(settings.RegionRoot);
        var game = new DirectoryInfo(settings.GamePath);
        var gameExe = new FileInfo(Path.Combine(game.FullName, "game/ffxiv_dx11.exe"));
        var files = await PerformDalamudUpdateAsync(token);
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
            }, settings.EnableDalamud, noPlugins.IsChecked == true, settings.RegionRoot, AppContext.BaseDirectory, token);
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
        if (Program.SteamEntry) Program.GameSession = runner;
        if (files != null)
        {
            var winePid = await injector.LaunchAsync(prepared, gameArguments, token,
                confirmationTimeout: settings.SelectedRegion == "ffxiv_tc" ? TimeSpan.FromMinutes(4) : null);
            status.Text = settings.EnableDalamud ? Localization.F($"游戏已由注入器启动（Wine PID {winePid}）；Dalamud 加载状态请在游戏内确认。") : Localization.F($"中国区游戏已启动（Wine PID {winePid}）；未启用或更新 Dalamud。");
            details.Text = "Dalamud log directory: " + Path.Combine(settings.RegionRoot, "logs");
        }
        else
        {
            await RegionLaunchPreparation.LaunchPlainAsync(prepared, gameArguments, token);
            status.Text = Localization.T("已通过 Proton 提交游戏启动命令；本次未启用或更新 Dalamud。");
        }
        MinimizeAfterGameLaunch();
    }

    private async Task<string> AuthenticateRegionAsync(CancellationToken token)
    {
        var selectedArea = settings.SelectedRegion == "ffxiv_cn" ? areas.FirstOrDefault(x => x.AreaName == (string?)area.SelectedItem) : null;
        var needsPassword = settings.SelectedRegion != "ffxiv_cn";
        if (needsPassword && string.IsNullOrWhiteSpace(settings.Account)) throw new InvalidOperationException("Enter the account name before signing in.");
        var profile = settings.SelectedRegion == "ffxiv_cn" ? settings.GetDeviceProfile(string.IsNullOrWhiteSpace(settings.Account) ? "qr-default" : settings.Account) : null;
        var request = new LoginRequest
        {
            Account = settings.Account, Secret = await ReadLoginPasswordAsync(needsPassword, token), DeviceProfile = profile,
            LoginCancellationTokenSource = operation,
            LoginSessionRefreshSink = settings.SelectedRegion == "ffxiv_cn" ? GetChinaTravelSession() : null,
            ShowQRCode = bytes => Dispatcher.UIThread.Post(() =>
            {
                qr.Source = null; qrBitmap?.Dispose();
                qrBitmap = new Bitmap(new MemoryStream(bytes)); qr.Source = qrBitmap; qr.IsVisible = true;
                status.Text = Localization.T("请使用叨鱼扫描二维码登录。");
            }),
            ShowLoginMessage = text => Dispatcher.UIThread.Post(() => status.Text = text),
        };
        status.Text = Localization.T("正在登录…");
        loginVerificationStarted = true;
        using var downloadObservation = settings.SelectedRegion == "ffxiv" ? ObserveDownloads("下载补丁", token) : null;
        var gameArguments = await ResolveLogin(settings.SelectedRegion).AuthenticateAsync(new(settings, selectedArea, areas,
            request, request.Secret ?? "", "", ct => TaiwanCaptcha.GetTokenAsync(settings.RegionRoot, ct),
            message => Dispatcher.UIThread.Post(() => status.Text = message),
            (authenticate, ct) => TaiwanCaptcha.GetTokenAsync(settings.RegionRoot, ct, authenticate), RequestManualOtpAsync, async ct =>
            { GameProcessGuard.EnsureClientFilesIdle(); await PerformGameUpdateAsync(ct); }), token);
        RefreshVersionDisplays();
        account.Text = settings.Account; // QR login may resolve the account name from the server.
        return gameArguments;
    }

    private async Task UpdateGameAsync(CancellationToken token)
    {
        settings.EnsureRegionAvailable();
        SaveSettings(false);
        if (!GameHelpers.IsValidGamePath(settings.GamePath)) throw new InvalidOperationException(Localization.T("请选择当前游戏区服已安装的完整游戏目录。"));
        using var gameLock = AcquireGameLock(settings.GamePath);
        GameProcessGuard.EnsureClientFilesIdle();
        if (settings.SelectedRegion == "ffxiv") await AuthenticateRegionAsync(token);
        else await PerformGameUpdateAsync(token);
        status.Text = "Game update completed.";
    }

    private static string SyncLabel(RegionSyncContent content) => content switch
    {
        RegionSyncContent.DalamudSettings => Localization.T("同步 Dalamud 设置"),
        RegionSyncContent.InstalledPlugins => Localization.T("同步已安装插件"),
        RegionSyncContent.PluginSettings => Localization.T("同步插件设置"),
        _ => throw new ArgumentOutOfRangeException(nameof(content))
    };

    private sealed record RegionSyncSelection(string Source, string[] Targets);

    internal Task PreviewSyncDialogAsync(string path) => SynchronizeRegionAsync(RegionSyncContent.PluginSettings, CancellationToken.None, path);

    private async Task SynchronizeRegionAsync(RegionSyncContent content, CancellationToken token, string? previewPath = null)
    {
        if (!settingsLoaded) throw new InvalidOperationException(Localization.T("设置尚未安全加载。"));
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = Localization.T("选择来源游戏区服及需要覆盖的目标游戏区服。仅替换勾选目标的对应内容，目标中来源没有的文件也会被移除。被覆盖的内容会保留备份。"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = content == RegionSyncContent.InstalledPlugins
                ? Localization.T("请勿将繁中区的插件 DLL 与中国区或国际区的版本互相覆盖。同步插件文件不代表它们兼容目标游戏区服。")
                : Localization.T("仅同步所选类别，不同步 Dalamud 主程序、运行时或资源。"),
            TextWrapping = TextWrapping.Wrap, Opacity = .7
        });
        var choices = LinuxSettings.Regions.Select(region => new ComboBoxItem
        {
            Content = Localization.Display(region.Name) + (RegionDataSync.HasSource(settings.Root, region.Id, content) ? "" : Localization.Display("（无来源数据）")),
            Tag = region.Id, IsEnabled = RegionDataSync.HasSource(settings.Root, region.Id, content)
        }).ToArray();
        var source = new ComboBox { ItemsSource = choices, HorizontalAlignment = HorizontalAlignment.Stretch };
        source.SelectedItem = choices.FirstOrDefault(item => item.IsEnabled && (string?)item.Tag == settings.SelectedRegion)
            ?? choices.FirstOrDefault(item => item.IsEnabled);
        panel.Children.Add(Field(Localization.T("以哪个游戏区服为基准"), source));
        panel.Children.Add(new TextBlock { Text = Localization.T("覆盖目标：") });
        var targets = LinuxSettings.Regions.Select(region => new CheckBox
        {
            Content = Localization.Display(region.Name), Tag = region.Id, IsChecked = false
        }).ToArray();
        var confirm = new Button { Content = Localization.T("同步到所选游戏区服"), IsEnabled = false };
        void UpdateConfirmation() => confirm.IsEnabled = source.SelectedItem != null &&
            targets.Any(box => box.IsEnabled && box.IsChecked == true);
        void UpdateTargets()
        {
            foreach (var box in targets)
            {
                box.IsEnabled = source.SelectedItem is ComboBoxItem selected && !Equals(box.Tag, selected.Tag);
                // A new source always requires an explicit destination selection.
                box.IsChecked = false;
            }
            UpdateConfirmation();
        }
        foreach (var box in targets)
        {
            box.IsCheckedChanged += (_, _) => UpdateConfirmation();
            panel.Children.Add(box);
        }
        source.SelectionChanged += (_, _) => UpdateTargets();
        UpdateTargets();
        if (source.SelectedItem == null)
            panel.Children.Add(new TextBlock { Text = Localization.T("各游戏区服均无此项数据，请先在来源游戏区服生成设置或安装插件。"), TextWrapping = TextWrapping.Wrap });
        var dismiss = new Button { Content = Localization.T("取消") };
        var buttons = new WrapPanel { ItemSpacing = 12, LineSpacing = 6 };
        buttons.Children.Add(confirm); buttons.Children.Add(dismiss); panel.Children.Add(buttons);
        var dialog = new Window { Title = SyncLabel(content), Width = 580, SizeToContent = SizeToContent.Height,
            Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        confirm.Click += (_, _) =>
        {
            if (source.SelectedItem is ComboBoxItem { Tag: string sourceId })
            {
                var selectedTargets = targets.Where(box => box.IsEnabled && box.IsChecked == true)
                    .Select(box => (string)box.Tag!).ToArray();
                if (selectedTargets.Length > 0) dialog.Close(new RegionSyncSelection(sourceId, selectedTargets));
            }
        };
        dismiss.Click += (_, _) => dialog.Close(null);
        if (previewPath != null)
            dialog.Opened += async (_, _) =>
            {
                await Task.Delay(500);
                using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                    new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height), new Vector(96, 96));
                bitmap.Render(dialog); bitmap.Save(previewPath);
                dialog.Close(null);
            };
        using var registration = token.Register(() => Dispatcher.UIThread.Post(() => dialog.Close(null)));
        var selection = await dialog.ShowDialog<RegionSyncSelection?>(this);
        if (previewPath != null) return;
        if (selection == null) { status.Text = Localization.T("已取消同步。"); return; }
        token.ThrowIfCancellationRequested();
        EnsureGamesStopped();
        synchronizingPlugins = true;
        status.Text = Localization.T("正在准备并同步所选数据…");
        try
        {
            var result = await Task.Run(() => RegionDataSync.Synchronize(settings.Root, selection.Source, selection.Targets, content, token), token);
            pluginSnapshot = null;
            status.Text = Localization.T("同步完成。原目标数据备份：") + result.BackupDirectory;
        }
        finally { synchronizingPlugins = false; }
    }

    private static void EnsureGamesStopped()
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (GameProcessGuard.IsGameProcessName(process.ProcessName))
                        throw new IOException(Localization.T("请退出各游戏区服游戏后再修改插件数据，避免运行中的插件覆盖设置。"));
                }
                catch (InvalidOperationException) { }
            }
        }
    }

    private string SelectedPluginRegion() => LinuxSettings.Regions[Math.Max(0, regionChoice.SelectedIndex)].Id;

    private bool HasPendingPluginChanges() => pluginSnapshot?.Plugins.Any(p =>
        pluginChecks.TryGetValue(p.InternalName, out var check) && check.IsChecked != p.Enabled) == true;

    private void RefreshPlugins()
    {
        if (!settingsLoaded || operation != null) return;
        pluginStatus.Text = "";
        pluginRows.Children.Clear(); pluginChecks.Clear(); pluginSnapshot = null;
        pluginRegionId = SelectedPluginRegion();
        pluginRegion.Text = Localization.T("当前管理的游戏区服：") + LinuxSettings.Regions.First(r => r.Id == pluginRegionId).Name;
        try
        {
            pluginSnapshot = OfflinePluginManager.Read(settings.Root, pluginRegionId);
            foreach (var item in pluginSnapshot.Plugins)
            {
                var box = new CheckBox { Content = new TextBlock { Text = $"{item.Name}  ({item.InternalName})  {item.Version}" +
                    (item.ScheduledForDeletion ? Localization.Display("（已计划删除）") : ""), TextWrapping = TextWrapping.Wrap },
                    IsThreeState = item.Enabled == null, IsChecked = item.Enabled, IsEnabled = !item.ScheduledForDeletion };
                box.IsCheckedChanged += (_, _) => pluginStatus.Text = Localization.T("有未应用的修改。点击应用保存，或点击放弃修改。");
                pluginChecks[item.InternalName] = box; pluginRows.Children.Add(box);
            }
            if (pluginSnapshot.Problems.Count > 0)
            {
                pluginStatus.Text = $"Some plugins could not be loaded ({pluginSnapshot.Problems.Count}); see the diagnostic log.";
                details.Text = string.Join("\n", pluginSnapshot.Problems);
            }

        }
        catch (Exception ex) { ReportFailure(ex); }
    }

    private Task RunPluginOperationAsync() => RunOperationAsync(ApplyPluginStatesAsync);

    private Task ApplyPluginStatesAsync(CancellationToken token)
    {
        if (!settingsLoaded || pluginSnapshot == null || pluginRegionId != SelectedPluginRegion())
            throw new IOException(Localization.T("请点击放弃修改，重新读取当前游戏区服的插件列表。"));
        if (!HasPendingPluginChanges()) { AppendLog("Failed: no changes.", "ERROR"); return Task.CompletedTask; }
        var changes = pluginSnapshot.Plugins.Where(p => !p.ScheduledForDeletion && pluginChecks[p.InternalName].IsChecked is bool)
            .ToDictionary(p => p.InternalName, p => pluginChecks[p.InternalName].IsChecked!.Value);
        if (changes.Count == 0) { AppendLog("Failed: no changes.", "ERROR"); return Task.CompletedTask; }
        token.ThrowIfCancellationRequested(); EnsureGamesStopped();
        var backup = OfflinePluginManager.ApplySelection(settings.Root, pluginRegionId, pluginSnapshot, changes);
        pluginSnapshot = OfflinePluginManager.Read(settings.Root, pluginRegionId);
        pluginStatus.Text = string.IsNullOrEmpty(backup) ? Localization.T("当前文件已与勾选列表一致，无需修改。") : Localization.T("已应用，下次启动游戏生效。原设置备份：") + backup;
        return Task.CompletedTask;
    }

    private static FileStream AcquireGameLock(string path) =>
        new(Path.Combine(path, ".soil-patch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
