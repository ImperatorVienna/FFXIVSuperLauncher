using XIVLauncher.Linux.Patching;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Constant;
using XIVLauncher.Dalamud;
using XIVLauncher.GamePatchV3.Update;

namespace XIVLauncher.Linux;

internal static class Program
{
    public static bool SteamEntry { get; private set; }
    public static CompatibilityRunner? GameSession { get; set; }

    private static bool appImageIntegrationReady;
    internal static void EnsureAppImageIntegration()
    {
        if (appImageIntegrationReady || Environment.GetEnvironmentVariable("APPIMAGE") is not { Length: > 0 } image) return;
        Updates.DesktopIntegration.PrepareAppImageStartup(Path.GetFullPath(image), Updates.DesktopIntegration.DataRoot,
            Path.Combine(AppContext.BaseDirectory, "icon.png"));
        appImageIntegrationReady = true;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try { EnsureAppImageIntegration(); }
        catch (Exception ex) { Console.Error.WriteLine("AppImage integration initialization failed: " + ex.Message); }
        if (args is ["--launcher-version"]) { Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3)); return 0; }
        if (args is ["--update-launcher"])
        {
            try
            {
                EnsureAppImageIntegration();
                var config = Updates.UpdateDistribution.Load();
                if (config.Kind != "appimage") throw new IOException("Launcher self-update requires an AppImage.");
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                var service = new Updates.LauncherUpdates(http, config);
                var release = service.CheckAsync(timeout.Token).GetAwaiter().GetResult() ?? throw new IOException("No release available.");
                var current = typeof(Program).Assembly.GetName().Version!;
                if (release.Version <= new Version(current.Major, current.Minor, current.Build)) throw new IOException("No newer release available.");
                var update = service.VerifyAsync(release, timeout.Token).GetAwaiter().GetResult();
                var image = Updates.UpdateInstallation.LauncherPath;
                var stage = service.StageAsync(update, image, _ => {}, timeout.Token).GetAwaiter().GetResult();
                using var helper = Updates.UpdateInstallation.Start(image, stage, update.Sha256, image + ".update.log", update.Version);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
        if (args is ["--steam-entry"])
        {
            SteamEntry = true;
            foreach (var key in new[] { "WINEPREFIX", "STEAM_COMPAT_DATA_PATH", "STEAM_COMPAT_TOOL_PATHS", "STEAM_COMPAT_MOUNTS", "STEAM_COMPAT_INSTALL_PATH" })
                Environment.SetEnvironmentVariable(key, null);
        }
        if (args.Contains("--list-proton"))
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ProtonDiscovery.Discover(),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (args.Contains("--help"))
        {
            Console.WriteLine("FFXIV Super Launcher\n无参数：启动图形界面\n--list-proton：列出已安装 Proton\n--diagnose-proton SCRIPT DATA_DIR：创建独立兼容环境并检测\n--check-game GAME_DIR：只检查游戏更新\n--prepare-dalamud DATA_DIR：更新指定目录的 Soil Dalamud");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--check-game")
        {
            try
            {
                var check = GameUpdater.Check(new DirectoryInfo(args[1]), false).GetAwaiter().GetResult();
                Console.WriteLine(JsonSerializer.Serialize(check, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(DiagnosticLog.Format(ex)); return 1; }
        }
        if (args.Length == 2 && args[0] == "--prepare-dalamud")
        {
            try
            {
                var root = Path.GetFullPath(args[1]);
                Paths.OverrideRoamingPath(root);
                var files = RegionDalamudUpdates.PrepareAsync("ffxiv_cn", root, message => Console.WriteLine(EnglishLog.Message(message)), CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"Injector: {files.Injector.FullName}\nRuntime: {files.Runtime.FullName}\nAssets: {files.Assets.FullName}");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
        }
        if (args.Length == 3 && args[0] == "--diagnose-proton" || args.Length == 5 && args[0] == "--diagnose-injector")
        {
            try
            {
                var tool = ProtonDiscovery.Discover().FirstOrDefault(x => x.Script == Path.GetFullPath(args[1]))
                    ?? ProtonDiscovery.FromScript(args[1], ProtonDiscovery.GetSteamRoots().FirstOrDefault() ?? "");
                var settings = new CompatibilitySettings
                {
                    Executable = tool.Script, SteamRoot = tool.SteamRoot,
                    RuntimeEntryPoint = tool.RuntimeEntryPoint, DataDirectory = Path.GetFullPath(args[2])
                };
                var runner = new CompatibilityRunner(settings);
                Console.WriteLine(runner.DiagnoseAsync().GetAwaiter().GetResult());
                if (args[0] == "--diagnose-injector")
                {
                    var runtime = runner.ToWindowsPathAsync(args[4]).GetAwaiter().GetResult();
                    Console.WriteLine(runner.CaptureAsync([Path.GetFullPath(args[3]), "launch", "--help"], environment: new Dictionary<string, string>
                    {
                        ["DOTNET_ROOT"] = runtime, ["DALAMUD_RUNTIME"] = runtime, ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                        ["STEAM_COMPAT_MOUNTS"] = string.Join(':', Path.GetDirectoryName(Path.GetFullPath(args[3])), Path.GetFullPath(args[4])),
                        ["PROTON_LOG"] = "0"
                    }).GetAwaiter().GetResult());
                }
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(DiagnosticLog.Format(ex)); return 1; }
        }
        AppBuilder.Configure<App>().UseX11().UseSkia().StartWithClassicDesktopLifetime(args);
        if (SteamEntry && GameSession != null)
        {
            try { GameSession.WaitForPrefixExitAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(DiagnosticLog.Format(ex)); return 1; }
        }
        return 0;
    }
}

public sealed class App : Application
{
    public override void Initialize()
    {
        try { Localization.SetLanguage(LinuxSettings.Load().Language); } catch { Localization.SetLanguage("en"); }
        Localization.InstallPresentationTranslation();
        Styles.Add(new FluentTheme());
        Styles.Add(ButtonPresentation.CreateStyle());
        using var icon = typeof(App).Assembly.GetManifestResourceStream("XIVLauncher.Linux.Resources.icon.png")!;
        Styles.Add(new Style(selector => selector.Is<Window>())
        {
            Setters = { new Setter(Window.IconProperty, new WindowIcon(icon)) }
        });
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(desktop.Args is ["--game-path", var gamePath] ? gamePath : null);
            // Developer-only visual smoke test; no account or game operation is performed.
            if (desktop.Args is { Length: >= 2 } preview && preview[0] == "--render-preview")
                desktop.MainWindow.Opened += async (_, _) =>
                {
                    if (preview.Length > 2 && int.TryParse(preview[2], out var tab)) ((MainWindow)desktop.MainWindow).SelectPreviewTab(tab);
                    await Task.Delay(1500);
                    if (preview.Length > 3 && preview[3] == "sync-dialog")
                    {
                        await ((MainWindow)desktop.MainWindow).PreviewSyncDialogAsync(preview[1]);
                        desktop.Shutdown();
                        return;
                    }
                    var window = desktop.MainWindow;
                    using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                        new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
                    bitmap.Render(window); bitmap.Save(preview[1]);
                    desktop.Shutdown();
                };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
