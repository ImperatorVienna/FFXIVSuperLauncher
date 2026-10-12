using System.Net;
using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using XIVLauncher.Linux.Updates;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class FrontendApplicationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "frontend-application-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact] public void InitializationCreatesDefaultsBeforeSetupAndWindowSavesPreserveProfiles()
    {
        var application = new FrontendApplication(root);
        application.Initialize();
        Assert.True(File.Exists(Path.Combine(root, "linux-settings.json")));
        var settings = LinuxSettings.Load(root);
        Assert.False(settings.SetupComplete);
        Assert.Equal("en", settings.Language);
        Assert.Equal("NA", settings.RegionProfiles["ffxiv"].CdKeyVersion);
        Assert.Equal("en-gb", settings.RegionProfiles["ffxiv"].EuShopLanguage);
        Assert.Equal(1, (int)settings.RegionProfiles["ffxiv"].ClientLanguage);
        settings.Save();
        var files = new[] { "ffxiv_cn", "ffxiv_tc", "ffxiv" }.ToDictionary(r => r, r => File.ReadAllBytes(Path.Combine(root, r, "settings.json")));
        var placement = new WindowPlacement(12, 34, 1100, 800, true);
        application.SaveWindow(placement);
        Assert.Equal(placement, LinuxSettings.Load(root).WindowPlacement);
        foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root, file.Key, "settings.json")));
        Assert.Throws<ArgumentException>(() => application.SaveWindow(new(0, 0, double.NaN, 800, false)));
        Assert.Throws<CredentialValidationException>(() => application.CompleteSetup(new(true)));
        Assert.False(LinuxSettings.Load(root).SetupComplete);
        Assert.False(application.IsAppImage);
    }

    [Fact] public void SteamIntegrationUsesExistingRegistrationWithoutAnotherPreference()
    {
        var application = new FrontendApplication(root);
        Assert.False(JsonSerializer.SerializeToElement(application.Initialize()).GetProperty("steamIntegration").GetBoolean());
        var steam = Path.Combine(root, "synthetic-steam");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        var entry = Path.Combine(root, "launcher");File.WriteAllText(entry, "synthetic");
        XIVLauncher.Common.Unix.SteamToolRegistration.Install(steam, entry);
        var settings = LinuxSettings.Load(root);settings.RegisteredSteamRoot = steam;settings.SaveGlobalPreferences();
        Assert.True(JsonSerializer.SerializeToElement(application.Initialize()).GetProperty("steamIntegration").GetBoolean());
        XIVLauncher.Common.Unix.SteamToolRegistration.Remove(steam);
        Assert.False(JsonSerializer.SerializeToElement(application.Initialize()).GetProperty("steamIntegration").GetBoolean());
    }

    [Fact] public async Task UpdateCheckFailureDoesNotBlockOtherOperationsAndCanRetry()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(_ => ++requests == 1 ? throw new HttpRequestException("synthetic unavailable") : new(HttpStatusCode.OK) { Content = new StringContent("[]") }));
        await using var updates = new FrontendLauncherUpdates(root, new(root), new(client, new()));
        var result = updates.Read(new(true));
        for (var i = 0; i < 200 && result.State == "checking"; i++) { await Task.Delay(5); result = updates.Read(new()); }
        Assert.Equal("failed", result.State);
        Assert.NotNull(new FrontendPreferences(root).Snapshot());
        Assert.True(File.Exists(Path.Combine(root, "linux-settings.json")));
        result = updates.Read(new(true));
        for (var i = 0; i < 200 && result.State == "checking"; i++) { await Task.Delay(5); result = updates.Read(new()); }
        Assert.Equal("ready", result.State); Assert.False(result.Available); Assert.Equal(2, requests);
        Assert.Throws<ArgumentException>(() => updates.Open(new("unknown")));
    }

    [Fact] public async Task CommittedUpdateResultSurvivesCancellationAndShutdown()
    {
        using var operation = new FrontendOperation(root, "ffxiv_cn");
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Begin(async () => { operation.CommitResult(new { exitForUpdate = true }); committed.SetResult(); await finish.Task; });
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        operation.Cancel(); finish.SetResult(); await operation.Completion;
        Assert.Equal("completed", operation.View().State);
        Assert.True(JsonSerializer.SerializeToElement(operation.View().Result).GetProperty("exitForUpdate").GetBoolean());
    }

    [Fact] public async Task ServicesShareOneDiagnosticFileWithoutDroppingConcurrentEntries()
    {
        var first = new SessionDiagnostics(root); var second = new SessionDiagnostics(Path.Combine(root, "."));
        Assert.Equal(first.FilePath, second.FilePath);
        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => Task.Run(() => (i % 2 == 0 ? first : second).Write("synthetic-" + i, "shared", "INFO"))));
        Assert.Equal(100, File.ReadAllLines(first.FilePath).Length);
        var about = JsonSerializer.SerializeToElement(new FrontendApplication(root).About());
        Assert.Equal(first.FilePath, about.GetProperty("diagnosticPath").GetString());
        Assert.Throws<ArgumentException>(() => new FrontendApplication(root).Notice(new("../../private")));
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", JsonSerializer.SerializeToElement(new FrontendApplication(root).Notice(new("license"))).GetProperty("text").GetString());
    }
    [Fact] public async Task AutomaticChecksWaitForSetupAndRespectPersistedPreference()
    {
        new FrontendApplication(root).Initialize();
        Assert.True(LinuxSettings.Load(root).AutoCheckUpdates);
        var requests = 0;
        using var client = new HttpClient(new Handler(_ => { requests++; return new(HttpStatusCode.OK) { Content = new StringContent("[]") }; }));
        await using var updates = new FrontendLauncherUpdates(root, new(root), new(client, new()));
        Assert.Equal("idle", updates.Read(new()).State); Assert.Equal(0, requests);
        var preferences = new FrontendPreferences(root);
        preferences.Update(new(AutoCheckUpdates: false));
        var settings = LinuxSettings.Load(root); settings.SetupComplete = true; settings.SaveGlobalPreferences();
        Assert.False(LinuxSettings.Load(root).AutoCheckUpdates);
        Assert.Equal("idle", updates.Read(new()).State); Assert.Equal(0, requests);
        preferences.Update(new(AutoCheckUpdates: true));
        var result = updates.Read(new());
        for (var i = 0; i < 200 && result.State == "checking"; i++) { await Task.Delay(5); result = updates.Read(new()); }
        Assert.Equal("ready", result.State); Assert.Equal(1, requests);
        preferences.Update(new(AutoCheckUpdates: false));
        result = updates.Read(new(true));
        for (var i = 0; i < 200 && result.State == "checking"; i++) { await Task.Delay(5); result = updates.Read(new()); }
        Assert.Equal("ready", result.State); Assert.Equal(2, requests);
        Assert.False(LinuxSettings.Load(root).AutoCheckUpdates);
    }

    [Fact] public async Task ValidationLogRemainsEnglishWhileUiMessageIsSeparate()
    {
        var failure = new CredentialValidationException("Invalid repository URL.", "仓库地址必须是有效的 HTTP 或 HTTPS 地址。");
        using var operation = new FrontendOperation(root, "ffxiv_cn");
        operation.Begin(() => throw failure);
        await operation.Completion;
        Assert.Contains("The repository URL must be a valid HTTP or HTTPS address.", operation.View().Log);
        Assert.DoesNotContain("仓库地址", operation.View().Log);
        Assert.Contains("The repository URL must be a valid HTTP or HTTPS address.", FrontendErrors.Diagnostic(failure));
    }
    [Fact] public void GameSettingsCannotChangeWhileRunningButUiLanguageCan()
    {
        new FrontendApplication(root).Initialize();
        var running=true;
        var preferences=new FrontendPreferences(root,()=>{if(running)throw new IOException("Game running");});
        preferences.Update(new(Language:"ja"));
        Assert.Equal("ja",LinuxSettings.Load(root).Language);
        var original=LinuxSettings.Load(root).RegionProfiles["ffxiv"].ClientLanguage;
        Assert.Throws<IOException>(()=>preferences.Update(new(ClientLanguage:1)));
        Assert.Equal(original,LinuxSettings.Load(root).RegionProfiles["ffxiv"].ClientLanguage);
        Assert.Throws<IOException>(()=>preferences.Update(new(ClientPaths:new(){["ffxiv"]="/test/game"})));
        var compatibility=new FrontendCompatibility(root,ensureGameIdle:()=>throw new IOException("Game running"));
        Assert.Throws<IOException>(()=>compatibility.Update(new(DataDirectory:"/test/prefix")));
        running=false;preferences.Update(new(ClientLanguage:1));
        Assert.Equal(1,(int)LinuxSettings.Load(root).RegionProfiles["ffxiv"].ClientLanguage);
    }
    [Fact] public void SetupDalamudChoicePersistsOnlyForSelectedRegionAndRespectsGameGuard()
    {
        new FrontendApplication(root).Initialize();
        var preferences = new FrontendPreferences(root, () => {});
        foreach (var region in LinuxSettings.Regions) preferences.Update(new(Region: region.Id, EnableDalamud: false));
        var result = JsonSerializer.SerializeToElement(preferences.Update(new(Region: "ffxiv_tc", EnableDalamud: true)));
        Assert.True(result.GetProperty("regions").EnumerateArray().Single(x => x.GetProperty("id").GetString() == "ffxiv_tc").GetProperty("enableDalamud").GetBoolean());
        var saved = LinuxSettings.Load(root);
        Assert.True(saved.RegionProfiles["ffxiv_tc"].EnableDalamud);
        Assert.False(saved.RegionProfiles["ffxiv"].EnableDalamud);
        Assert.False(saved.RegionProfiles["ffxiv_cn"].EnableDalamud);
        var blocked = new FrontendPreferences(root, () => throw new IOException("Game running"));
        Assert.Throws<IOException>(() => blocked.Update(new(EnableDalamud: false)));
        Assert.True(LinuxSettings.Load(root).RegionProfiles["ffxiv_tc"].EnableDalamud);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
}
