using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Linux.Frontend;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Exceptions;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class GameSessionAuthenticationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "game-auth-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static LoginSessionRefreshContext Expired => new()
    {
        RefreshGameSessionIdAsync = () => throw new LoginException(-10515004, "expired"),
        RefreshDcTravelSessionIdAsync = () => throw new LoginException(-10515004, "expired")
    };
    private static LoginSessionRefreshContext Fresh => new()
    {
        RefreshGameSessionIdAsync = () => Task.FromResult("fresh-game-ticket"),
        RefreshDcTravelSessionIdAsync = () => Task.FromResult("fresh-travel-ticket")
    };
    private static LoginResult Success(string id = "same-account") => new()
    {
        State = LoginState.Ok, OAuthLogin = new() { SndaID = id, TGT = "new-tgt", Guid = "new-guid", QuickLoginSecret = "rotated-secret" }
    };
    private GameSessionAuthentication Auth(Func<LoginType, LoginRequest, CancellationToken, Task<LoginResult>> login) =>
        new(root, "alias", "same-account", FakeMachineInfo.CreateSnapshot(), Expired, login, _ => Fresh);

    [Fact]
    public async Task QuickLoginRotatesSecretAndRefreshesBothServices()
    {
        await RegionCredentials.StoreValueAsync(root, "ffxiv_cn", "alias", "session", "old-secret", default);
        var calls = 0;
        await using var auth = Auth((type, request, _) =>
        {
            Assert.Equal(LoginType.QuickLogin, type); Assert.Equal("old-secret", request.Secret); calls++;
            return Task.FromResult(Success());
        });
        Assert.Equal("fresh-game-ticket", await auth.Refresh.RefreshGameSessionIdAsync());
        Assert.Equal("fresh-travel-ticket", await auth.Refresh.RefreshDcTravelSessionIdAsync());
        Assert.Equal(1, calls);
        Assert.Equal("rotated-secret", await RegionCredentials.ReadAsync(root, "ffxiv_cn", "alias", "session", default));
    }

    [Fact]
    public async Task WrongScanCannotReplaceGameAccount()
    {
        await RegionCredentials.StoreValueAsync(root, "ffxiv_cn", "alias", "session", "old", default);
        var calls = 0;
        await using var auth = Auth((type, request, _) =>
        {
            calls++;
            if (type == LoginType.QuickLogin) throw new LoginException(-1, "invalid", true);
            request.ShowQRCode!([1,2,3]); return Task.FromResult(Success("wrong-account"));
        });
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
        Assert.Equal(1, calls);
        auth.StartScan(); await Wait(() => auth.State.Status == "required");
        Assert.Equal(2, calls); Assert.Null(auth.State.Image);
        Assert.Null(await RegionCredentials.ReadAsync(root, "ffxiv_cn", "alias", "session", default));
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
    }

    [Fact]
    public async Task ReopenedHostShowsQrAndRestoresOriginalGameBridge()
    {
        var scanned = new TaskCompletionSource<LoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = Auth((type, request, token) =>
        {
            Assert.Equal(LoginType.QRCode, type); request.ShowQRCode!([1,2,3]);
            return scanned.Task.WaitAsync(token);
        });
        await using var bridge = await GameTravelSession.CreateAsync(root, auth, [], _ => { });
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
        var proxy = new FrontendGameRecovery(root);
        Assert.True(JsonSerializer.SerializeToElement(await proxy.CheckAsync(default)).GetProperty("required").GetBoolean());
        await using var operations = new FrontendOperations(root);
        var id = JsonSerializer.SerializeToElement(proxy.Start(operations)).GetProperty("operationId").GetString()!;
        await Wait(() => operations.Poll(new(id)).Challenge?.Kind == "qr");
        Assert.Equal("data:image/png;base64,AQID", operations.Poll(new(id)).Challenge!.Image);
        scanned.SetResult(Success()); await Wait(() => !operations.Busy);
        Assert.Equal("completed", operations.Poll(new(id)).State);
        using var http = new HttpClient();
        using var response = await http.PostAsync($"http://127.0.0.1:{bridge.Port}/dctravel/",
            new StringContent("""{"Method":"RefreshGameSessionId","Params":[]}""", System.Text.Encoding.UTF8, "application/json"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("fresh-game-ticket", json.RootElement.GetProperty("Result").GetString());
        await bridge.DisposeAsync();
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "ffxiv_cn", "game-sessions")));
    }

    [Fact]
    public async Task RecoveryApiRejectsUnauthenticatedAndBrowserRequests()
    {
        var auth = Auth((_, _, _) => throw new Exception("Must not authenticate"));
        await using var bridge = await GameTravelSession.CreateAsync(root, auth, [], _ => { });
        var path = Directory.GetFiles(Path.Combine(root, "ffxiv_cn", "game-sessions")).Single();
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        var entry = JsonSerializer.Deserialize<GameRecoveryEndpoint.Discovery>(await File.ReadAllTextAsync(path))!;
        using var http = new HttpClient();
        var url = $"http://127.0.0.1:{entry.Port}/game-auth/start";
        using var denied = await http.PostAsync(url, null); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", entry.Token);
        http.DefaultRequestHeaders.Add("Origin", "https://example.com");
        using var browser = await http.PostAsync(url, null); Assert.Equal(HttpStatusCode.Forbidden, browser.StatusCode);
    }

    [Fact]
    public async Task CancelledScanCanBeRetried()
    {
        var attempts = 0;
        await using var auth = Auth(async (_, request, token) =>
        {
            request.ShowQRCode!([1]);
            if (++attempts == 1) await Task.Delay(Timeout.Infinite, token);
            return Success();
        });
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
        auth.StartScan(); await Wait(() => auth.State.Image != null);
        auth.StartScan(); Assert.Equal(1, attempts);
        auth.CancelScan(); await Wait(() => auth.State.Status == "required");
        await Task.Delay(20);
        auth.StartScan(); await Wait(() => auth.State.Status == "ready");
        Assert.Equal(2, attempts);
    }
    [Fact]
    public async Task DiscoveryContinuesPastStaleEntries()
    {
        var directory = Path.Combine(root, "ffxiv_cn", "game-sessions");
        Directory.CreateDirectory(directory);
        for (var i = 0; i < 40; i++)
            await File.WriteAllTextAsync(Path.Combine(directory, $"{i}.json"), "{}");
        var auth = Auth((_, _, _) => throw new Exception());
        await using var bridge = await GameTravelSession.CreateAsync(root, auth, [], _ => { });
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
        // Put the sole live entry after the first batch in actual enumeration order.
        var files = Directory.GetFiles(directory);
        var live = files.Single(path => Path.GetFileNameWithoutExtension(path).Length == 32);
        var contents = await File.ReadAllTextAsync(live);
        await File.WriteAllTextAsync(live, "{}");
        await File.WriteAllTextAsync(files[^1], contents);
        var proxy = new FrontendGameRecovery(root);
        Assert.True(JsonSerializer.SerializeToElement(await proxy.CheckAsync(default)).GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task FailedDiscoveryCreationReleasesListener()
    {
        await using var auth = Auth((_, _, _) => throw new Exception());
        await using var bridge = new DcTravelerBridge(auth.Refresh, [], _ => { });
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ffxiv_cn"), "blocked");
        Assert.ThrowsAny<IOException>(() => new GameRecoveryEndpoint(root, bridge.Port, auth));
        File.Delete(Path.Combine(root, "ffxiv_cn"));
        await using var endpoint = new GameRecoveryEndpoint(root, bridge.Port, auth);
    }

    [Fact]
    public async Task DiscoveryDeletionFailureDoesNotPreventBridgeDisposal()
    {
        var auth = Auth((_, _, _) => throw new Exception());
        var bridge = await GameTravelSession.CreateAsync(root, auth, [], _ => { });
        var path = Directory.GetFiles(Path.Combine(root, "ffxiv_cn", "game-sessions")).Single();
        File.Delete(path); Directory.CreateDirectory(path);
        await bridge.DisposeAsync();
        await bridge.DisposeAsync();
        await using var replacement = new GameRecoveryEndpoint(root, bridge.Port, auth);
    }

    [Fact]
    public async Task PollFailureCancelsRemoteScan()
    {
        var auth = Auth(async (_, request, token) =>
        {
            request.ShowQRCode!([1]);
            await Task.Delay(Timeout.Infinite, token);
            return Success();
        });
        await using var bridge = await GameTravelSession.CreateAsync(root, auth, [], _ => { });
        await Assert.ThrowsAsync<GameSessionLoginRequiredException>(auth.Refresh.RefreshGameSessionIdAsync);
        using var http = new HttpClient(new FailRecoveryPoll());
        var proxy = new FrontendGameRecovery(root, http);
        await proxy.CheckAsync(default);
        await using var operations = new FrontendOperations(root);
        var id = JsonSerializer.SerializeToElement(proxy.Start(operations)).GetProperty("operationId").GetString()!;
        await Wait(() => !operations.Busy);
        Assert.Equal("failed", operations.Poll(new(id)).State);
        await Wait(() => auth.State.Status == "required");
    }

    private sealed class FailRecoveryPoll : DelegatingHandler
    {
        private int reads;
        public FailRecoveryPoll() : base(new HttpClientHandler { UseProxy = false }) { }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/game-auth/state" && ++reads > 1)
                throw new HttpRequestException("Simulated polling failure");
            return base.SendAsync(request, token);
        }
    }

    [Fact]
    public async Task DiscoveryCleansDeadOwnersButKeepsLiveAndRecentIncompleteEntries()
    {
        var directory = Path.Combine(root, "ffxiv_cn", "game-sessions");
        Directory.CreateDirectory(directory);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var dead = Path.Combine(directory, "dead.json");
        var live = Path.Combine(directory, "live.json");
        var oldInvalid = Path.Combine(directory, "old.json");
        var recent = Path.Combine(directory, "recent.json");
        await File.WriteAllTextAsync(dead, JsonSerializer.Serialize(new GameRecoveryEndpoint.Discovery(12345, new string('a', 64), process.Id, 1)));
        await File.WriteAllTextAsync(live, JsonSerializer.Serialize(new GameRecoveryEndpoint.Discovery(12345, new string('a', 64), process.Id, process.StartTime.ToUniversalTime().Ticks)));
        await File.WriteAllTextAsync(oldInvalid, "{");
        File.SetLastWriteTimeUtc(oldInvalid, DateTime.UtcNow.AddMinutes(-10));
        await File.WriteAllTextAsync(recent, "{");
        using var http = new HttpClient(new UnavailableRecovery(false));
        await new GameSessionDiscovery(root, http).CleanupAsync(default);
        Assert.False(File.Exists(dead)); Assert.False(File.Exists(oldInvalid));
        Assert.True(File.Exists(live)); Assert.True(File.Exists(recent));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyDiscoveryIsRemovedOnlyOnConnectionRefusal(bool refused)
    {
        var directory = Path.Combine(root, "ffxiv_cn", "game-sessions");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "legacy.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new GameRecoveryEndpoint.Discovery(12345, new string('a', 64))));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
        using var http = new HttpClient(new UnavailableRecovery(refused));
        await new GameSessionDiscovery(root, http).CleanupAsync(default);
        Assert.Equal(!refused, File.Exists(path));
    }

    private sealed class UnavailableRecovery(bool refused) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            throw (refused ? new HttpRequestException("Unavailable", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused))
                : new TaskCanceledException("Simulated timeout"));
    }

    [Fact]
    public void StartupCleanupOnlyRemovesOldKnownAtomicFiles()
    {
        Directory.CreateDirectory(root);
        var old = Path.Combine(root, "linux-settings.json." + Guid.NewGuid().ToString("N") + ".tmp");
        var recent = Path.Combine(root, "linux-settings.json." + Guid.NewGuid().ToString("N") + ".tmp");
        var unknown = Path.Combine(root, "other." + Guid.NewGuid().ToString("N") + ".tmp");
        var actual = Path.Combine(root, "linux-settings.json");
        foreach (var path in new[] { old, recent, unknown, actual }) File.WriteAllText(path, "keep");
        foreach (var path in new[] { old, unknown, actual }) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        var shortName = Path.Combine(root, "linux-settings.json.tmp");
        File.WriteAllText(shortName, "keep");
        StartupCleanup.Run(root);
        Assert.True(File.Exists(shortName));
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent)); Assert.True(File.Exists(unknown)); Assert.True(File.Exists(actual));
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(20);
        Assert.True(condition());
    }
}
