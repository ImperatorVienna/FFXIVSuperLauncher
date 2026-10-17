using System.Net;
using System.Text.Json;
using XIVLauncher.Account.DeviceProfiles;
using XIVLauncher.Login.Channels;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Exceptions;
using XIVLauncher.Login.Models;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class QrLoginRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "qr-recovery-" + Guid.NewGuid());
    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    private static HttpResponseMessage Json(object data, int code = 0) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { return_code = code, error_type = 0, data })) };
    private static HttpResponseMessage Code(int sequence)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([(byte)sequence]) };
        response.Headers.Add("Set-Cookie", $"CODEKEY=synthetic-{sequence}; Path=/");
        return response;
    }
    private static HttpResponseMessage Success(string endpoint) => endpoint switch
    {
        "getGuid.json" => Json(new { guid = "synthetic-guid" }),
        "getAccountGroup" => Json(new { sndaIdArray = new[] { "sid" }, accountArray = new[] { "account" } }),
        "accountGroupLogin" => Json(new { tgt = "ticket", autoLoginSessionKey = "replacement" }),
        _ => throw new InvalidOperationException(endpoint)
    };
    private static string Endpoint(HttpRequestMessage request) => request.RequestUri!.Segments.Last();

    [Theory]
    [InlineData(-1)]
    [InlineData((int)LoginExceptionCode.QrCodeVerifyFailed)]
    public async Task ExpiryRenewsQrIncludingWhenPendingCodeIsAlsoReturned(int expiredCode)
    {
        var codes = 0; var scans = 0; var shown = new List<byte>();
        using var transport = new Transport((request, _) => Task.FromResult(Endpoint(request) switch
        {
            "getCodeKey.json" => Code(++codes),
            "codeKeyLogin.json" => ++scans == 1
                ? Json(new { failReason = "二维码不存在或已过期，请重试" }, expiredCode)
                : Json(new { sndaId = "sid", tgt = "ticket", inputUserId = "account", nextAction = 0 }),
            var endpoint => Success(endpoint)
        }));
        using var cancel = new CancellationTokenSource();
        var channel = new QRCodeLoginChannel(new LoginChannelContext(FakeMachineInfo.CreateSnapshot(), transport));
        var result = await channel.LoginAsync(new() { LoginCancellationTokenSource = cancel, ShowQRCode = bytes => shown.Add(bytes[0]) });
        Assert.Equal(LoginState.Ok, result.State);
        Assert.Equal(new byte[] { 1, 2 }, shown);
        Assert.Equal(2, scans);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothCancellationSourcesStopInFlightPollingWithoutRenewing(bool outer)
    {
        using var user = new CancellationTokenSource(); using var operation = new CancellationTokenSource();
        var codes = 0;
        using var transport = new Transport(async (request, token) =>
        {
            var endpoint = Endpoint(request);
            if (endpoint == "getCodeKey.json") return Code(++codes);
            if (endpoint != "codeKeyLogin.json") return Success(endpoint);
            (outer ? operation : user).Cancel();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        });
        var channel = new QRCodeLoginChannel(new LoginChannelContext(FakeMachineInfo.CreateSnapshot(), transport));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => channel.LoginAsync(new() { LoginCancellationTokenSource = user }, operation.Token).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, codes);
    }

    [Fact]
    public async Task LocalExpiryInterruptsPendingHttpRequest()
    {
        using var expiry = new CancellationTokenSource();
        using var transport = new Transport(async (_, token) =>
        {
            expiry.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        });
        var channel = new QRCodeLoginChannel(new LoginChannelContext(FakeMachineInfo.CreateSnapshot(), transport));
        var result = await channel.WaitForScanAsync("code", "guid", expiry, default).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Null(result.sndaId);
    }

    [Fact]
    public async Task ExpiredSavedLoginThenQrTimeoutCanRetryWithoutDeletingAccount()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv_cn";
        settings.Account = "account"; settings.Current.RememberPassword = true;
        var profile = settings.GetDeviceProfile(settings.Account);
        await RegionCredentials.StoreAsync(settings, settings.Account, "session", "expired", default);
        await RegionCredentials.StoreAsync(settings, settings.Account, "otp", "unrelated", default);
        var codes = 0; var quick = 0; var allowScan = false;
        using var transport = new Transport((request, _) => Task.FromResult(Endpoint(request) switch
        {
            "autoLogin.json" => RejectQuick(),
            "getCodeKey.json" => Code(++codes),
            "codeKeyLogin.json" => allowScan
                ? Json(new { sndaId = "sid", tgt = "ticket", inputUserId = "account", nextAction = 0 })
                : Json(new { failReason = "二维码不存在或已过期，请重试" }, -1),
            var endpoint => Success(endpoint)
        }));
        HttpResponseMessage RejectQuick() { quick++; return Json(new { failReason = "expired" }, -16027517); }
        Task<LoginResult> Login(LoginType type, LoginRequest request, CancellationToken token)
        {
            var context = new LoginChannelContext(profile, transport);
            return type == LoginType.QuickLogin ? new SessionKeyLoginChannel(context).LoginAsync(request, token)
                : new QRCodeLoginChannel(context).LoginAsync(request, token);
        }
        using (var first = new CancellationTokenSource())
        {
            var ex = await Assert.ThrowsAsync<LoginException>(() => ChinaLoginSession.AuthenticateAsync(settings,
                new() { Account = settings.Account, DeviceProfile = profile, LoginCancellationTokenSource = first }, _ => {}, first.Token, Login));
            Assert.Equal((int)LoginExceptionCode.ScanTimeoutOrCanceled, ex.ErrorCode);
        }
        Assert.Equal(2, codes);
        Assert.Null(await RegionCredentials.ReadAsync(settings, settings.Account, "session", default));
        settings = LinuxSettings.Load(root); allowScan = true;
        using var second = new CancellationTokenSource();
        await ChinaLoginSession.AuthenticateAsync(settings,
            new() { Account = settings.Account, DeviceProfile = profile, LoginCancellationTokenSource = second }, _ => {}, second.Token, Login);
        Assert.Equal(1, quick); Assert.Equal(3, codes);
        Assert.Equal("replacement", await RegionCredentials.ReadAsync(settings, settings.Account, "session", default));
        Assert.Equal("unrelated", await RegionCredentials.ReadAsync(settings, settings.Account, "otp", default));
        Assert.Equal(profile, settings.GetDeviceProfile(settings.Account));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
