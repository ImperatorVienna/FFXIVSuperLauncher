using XIVLauncher.Common.Unix;
using XIVLauncher.Linux.Updates;

namespace XIVLauncher.Linux.Frontend;

internal sealed record AboutRequest(string Key);
internal sealed record SetupRequest(bool EnableDalamud);

internal sealed class FrontendApplication(string root)
{
    private readonly SessionDiagnostics diagnostics = new(root);
    private bool initialized;
    internal bool IsAppImage => Path.GetFullPath(root) == Path.GetFullPath(LinuxSettings.DataRoot)
        && Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 };
    internal static readonly Dictionary<string, string> Links = new()
    {
        ["project"] = "https://github.com/ImperatorVienna/FFXIVSuperLauncher",
        ["issues"] = "https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues",
        ["author"] = "https://github.com/ImperatorVienna",
        ["XIVLauncher"] = "https://github.com/goatcorp/FFXIVQuickLauncher",
        ["XIVLauncherCN"] = "https://github.com/ottercorp/FFXIVQuickLauncher",
        ["XIVLauncherCN (Soil)"] = "https://github.com/AtmoOmen/FFXIVQuickLauncher",
        ["XIVTCLauncher"] = "https://github.com/cycleapple/XIVTCLauncher",
        ["Dalamud (goatcorp)"] = "https://github.com/goatcorp/Dalamud",
        ["Dalamud (Dalamud-DailyRoutines)"] = "https://github.com/Dalamud-DailyRoutines/Dalamud",
        ["Dalamud (yanmucorp)"] = "https://github.com/yanmucorp/Dalamud",
        ["sources"] = "https://github.com/ImperatorVienna/FFXIVSuperLauncher/blob/HEAD/SOURCES.txt",
        ["icon"] = "https://www.pngaaa.com/detail/6354760",
        ["font-genryu"] = "https://github.com/ButTaiwan/genryu-font",
        ["font-cinzel"] = "https://github.com/NDISCOVER/Cinzel"
    };
    internal object Initialize()
    {
        LinuxSettings.EnsureCreated(root);
        string? warning = null;
        try { EnsureIntegration(); }
        catch (Exception ex) { diagnostics.Write(FrontendErrors.Diagnostic(ex), "shared", "WARN"); warning = "应用菜单入口初始化失败，可在“关于”页面重试。"; }
        var settings = LinuxSettings.Load(root);
        return new { settings.WindowPlacement, settings.SetupComplete, warning };
    }
    internal void EnsureIntegration()
    {
        if (initialized) return;
        LinuxSettings.EnsureCreated(root);
        if (IsAppImage)
        {
            DesktopIntegration.PrepareAppImageStartup(UpdateInstallation.LauncherPath, DesktopIntegration.DataRoot, Path.Combine(AppContext.BaseDirectory, "icon.png"));
            if (LinuxSettings.Load(root).SetupComplete) RefreshCaches();
        }
        initialized = true;
    }
    internal object About() => new
    {
        version = typeof(FrontendHost).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? typeof(FrontendHost).Assembly.GetName().Version?.ToString(3), author = "ImperatorVienna",
        isAppImage = IsAppImage, notice = IsAppImage ? AppImageEntry.Notice : "",
        diagnosticPath = diagnostics.FilePath, links = Links.Keys.ToArray()
    };
    internal object Open(AboutRequest request)
    {
        if (!Links.TryGetValue(request.Key, out var url)) throw new ArgumentException("Unknown project link.");
        OfficialBrowser.Open(url, root); return new { opened = true };
    }
    internal object Notice(AboutRequest request)
    {
        var filename = request.Key switch { "license" => "LICENSE", "third-party" => "THIRD-PARTY-NOTICES.txt", _ => throw new ArgumentException("Unknown notice.") };
        var path = Path.Combine(AppContext.BaseDirectory, filename);
        string text;
        if (File.Exists(path)) text = File.ReadAllText(path);
        else
        {
            using var stream = typeof(FrontendHost).Assembly.GetManifestResourceStream("About." + filename) ?? throw new IOException("License notice missing.");
            using var reader = new StreamReader(stream); text = reader.ReadToEnd();
        }
        if (text.Length > 400000) throw new IOException("License notice exceeds the display limit.");
        return new { text };
    }
    internal object InstallMenu()
    {
        if (!IsAppImage) throw new ArgumentException("The application menu entry requires an AppImage.");
        initialized = false; EnsureIntegration(); RefreshCaches(); return new { installed = true };
    }
    internal object CompleteSetup(SetupRequest request)
    {
        var settings = LinuxSettings.Load(root);
        if (settings.ProtonScript.Length == 0) throw new CredentialValidationException("No Proton selected.", "请先选择 Proton。");
        var proton = ProtonDiscovery.FromScript(settings.ProtonScript, settings.SteamRoot);
        new CompatibilityRunner(settings.GetCompatibility(proton)).Validate();
        settings.EnableDalamud = request.EnableDalamud;
        settings.SaveRegionPreferences(settings.SelectedRegion);
        settings.SetupComplete = true; settings.SaveGlobalPreferences();
        if (IsAppImage) RefreshCaches();
        return new FrontendPreferences(root).Snapshot(settings);
    }
    internal object SaveWindow(WindowPlacement placement)
    {
        if (!placement.IsValid || Math.Abs((long)placement.X) > 100000 || Math.Abs((long)placement.Y) > 100000)
            throw new ArgumentException("Invalid window position.");
        var settings = LinuxSettings.Load(root); settings.WindowPlacement = placement; settings.SaveGlobalPreferences();
        return new { saved = true };
    }
    private void RefreshCaches() => _ = Task.Run(async () =>
    {
        try { foreach (var error in await DesktopIntegration.RefreshCachesAsync()) diagnostics.Write(error, "shared", "WARN"); }
        catch (Exception ex) { diagnostics.Write(FrontendErrors.Diagnostic(ex), "shared", "WARN"); }
    });
}
