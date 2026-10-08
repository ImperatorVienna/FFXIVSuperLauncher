using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class Release051Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super051-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact] public void CallerCancellationNeverTriggersOfflinePrompt()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.False(LocalDalamud.IsTimeout(new TaskCanceledException(), cancellation.Token));
        Assert.False(LocalDalamud.IsTimeout(new TimeoutException(), cancellation.Token));
        Assert.True(LocalDalamud.IsTimeout(new HttpRequestException("request", new TimeoutException()), default));
        Assert.True(LocalDalamud.IsTimeout(new TaskCanceledException(), default));
        Assert.False(LocalDalamud.IsTimeout(new IOException("hash failed"), default));
        Assert.False(LocalDalamud.IsTimeout(new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden), default));
    }
    [Fact] public void DisablePersistsOnlyForSelectedRegion()
    {
        var settings = LinuxSettings.Load(root); settings.EnableDalamud = true;
        settings.RegionProfiles["ffxiv_tc"].EnableDalamud = true;
        LocalDalamud.Disable(settings);
        var reloaded = LinuxSettings.Load(root);
        Assert.False(reloaded.EnableDalamud); Assert.True(reloaded.RegionProfiles["ffxiv_tc"].EnableDalamud);
    }
    private LinuxSettings Install(bool receipt)
    {
        var settings = LinuxSettings.Load(root); settings.EnableDalamud = true; settings.Current.DalamudVersion = "test-1";
        var region = settings.RegionRoot; var addon = Path.Combine(region, "addon/Hooks/test-1");
        void Write(string relative, string content = "fixture") { var p = Path.Combine(region, relative); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, content); }
        var hashes = new Dictionary<string, string>();
        foreach (var name in new[] { "Dalamud.dll", "Dalamud.Injector.exe", "Dalamud.Boot.dll" })
        { Write("addon/Hooks/test-1/" + name); hashes[name] = Convert.ToHexString(MD5.HashData("fixture"u8)); }
        Write("addon/Hooks/test-1/hashes.json", JsonSerializer.Serialize(hashes));
        Write("addon/Hooks/test-1/Dalamud.runtimeconfig.json", "{\"runtimeOptions\":{\"frameworks\":[{\"name\":\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"}]}}");
        Write("runtime/version", "10.0.1"); Write("runtime/host/fxr/10.0.1/hostfxr.dll");
        Write("runtime/shared/Microsoft.NETCore.App/10.0.1/System.Private.CoreLib.dll");
        Write("runtime/shared/Microsoft.NETCore.App/10.0.1/coreclr.dll");
        Write("runtime/shared/Microsoft.WindowsDesktop.App/10.0.1/PresentationCore.dll");
        Write("assets/asset.ver", "6");
        foreach (var name in new[] { "FontAwesomeFreeSolid.otf", "Inconsolata-Regular.ttf", "gamesym.ttf", "NotoSansCJKjp-Medium.otf", "defaultIcon.png", "logo.png" }) Write("assets/6/UIRes/" + name);
        if (receipt) LocalDalamud.Record(region, new("test-1", "", "", "10.0.1", 6, [], null, true), Path.Combine(region, "assets/6"));
        return settings;
    }
    [Theory] [InlineData(true)] [InlineData(false)] public void LocalComponentsWorkWithoutNetworkAndRejectDamagedProgram(bool receipt)
    {
        var settings = Install(receipt); var local = LocalDalamud.Find(settings, default);
        Assert.True(local.Soil); Assert.EndsWith("assets/6", local.Assets.FullName);
        File.WriteAllText(Path.Combine(local.Injector.DirectoryName!, "Dalamud.dll"), "damaged");
        Assert.Throws<IOException>(() => LocalDalamud.Find(settings, default));
    }
    [Fact] public void MissingOrMismatchedRuntimeRefusesOfflineUse()
    {
        var settings = Install(true);
        File.WriteAllText(Path.Combine(settings.RegionRoot, "runtime/version"), "9.0.11");
        Assert.Throws<IOException>(() => LocalDalamud.Find(settings, default));
        File.WriteAllText(Path.Combine(settings.RegionRoot, "runtime/version"), "10.0.1");
        File.Delete(Path.Combine(settings.RegionRoot, "runtime/shared/Microsoft.NETCore.App/10.0.1/coreclr.dll"));
        Assert.Throws<IOException>(() => LocalDalamud.Find(settings, default));
    }
    [Fact] public void ReceiptRejectsChangedAssetsAndDoesNotCrossRegions()
    {
        var settings = Install(true);
        File.WriteAllText(Path.Combine(settings.RegionRoot, "assets/6/UIRes/logo.png"), "changed");
        Assert.Throws<IOException>(() => LocalDalamud.Find(settings, default));
        settings.SelectedRegion = "ffxiv_tc";
        Assert.Throws<IOException>(() => LocalDalamud.Find(settings, default));
    }
    [Theory] [InlineData("{\"error\":\"captcha failed secret-account\"}", "captcha")]
    [InlineData("{\"error\":\"OTP invalid secret-account\"}", "otp")]
    [InlineData("{\"error\":\"secret-account\"}", "rejected")]
    [InlineData("[]", "response-format")]
    public async Task AuthenticationErrorsIdentifyStageWithoutLeakingBodies(string body, string reason)
    {
        using var client = new HttpClient(new Handler(body));
        var e = await Assert.ThrowsAsync<TaiwanAuthenticationException>(() => new TaiwanLogin(client).LoginAsync(root, "account", "password", "", "captcha", default));
        Assert.Contains("launcherLogin", e.Message); Assert.Contains(reason, e.Message);
        Assert.DoesNotContain("secret-account", e.Message); Assert.DoesNotContain("secret-account", DiagnosticLog.Format(e));
        Assert.Contains("ffxiv_tc authentication failed", DiagnosticLog.Format(e));
    }
    private sealed class Handler(string body) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }); }
}
