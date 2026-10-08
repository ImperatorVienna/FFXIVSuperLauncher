using System.Net;
using XIVLauncher.Linux.Taiwan;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release0100Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super0100-" + Guid.NewGuid());
    // RFC 6238 Appendix B, SHA1 vectors, truncated to the configured six decimal digits.
    [Theory][InlineData(59L,"287082")][InlineData(1111111109L,"081804")][InlineData(1111111111L,"050471")][InlineData(1234567890L,"005924")][InlineData(2000000000L,"279037")][InlineData(20000000000L,"353130")]
    public void TotpMatchesRfcVectors(long seconds, string expected) => Assert.Equal(expected, OneTimePassword.Generate("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", DateTimeOffset.FromUnixTimeSeconds(seconds)));
    [Theory][InlineData("ffxiv_dx11.exe", true)][InlineData("FFXIV.EXE", true)][InlineData("ffxiv_dx11", true)][InlineData("ffxiv", true)][InlineData("ffxivboot.exe",false)][InlineData("ffxivlauncher64.exe",false)][InlineData("ffxivupdater64",false)][InlineData("proton",false)]
    public void OfficialLauncherIsNotMistakenForGame(string name, bool expected) => Assert.Equal(expected, GameProcessGuard.IsGameProcessName(name));
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
    [Fact] public async Task TaiwanAuthThenUpdateThenSessionKeepsOtpAndPassword()
    {
        Directory.CreateDirectory(Path.Combine(root,"game")); File.WriteAllText(Path.Combine(root,"game/ffxivgame.ver"),"2026.01.01.0000.0000");
        var events = new List<string>();
        using var client = new HttpClient(new Handler(async request =>
        {
            var stage = request.RequestUri!.AbsolutePath;
            if (stage.EndsWith("launcherLogin"))
            {
                events.Add("login"); using var json = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal("012345", json.RootElement.GetProperty("code").GetString()); Assert.Equal("70262b", json.RootElement.GetProperty("password").GetString());
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"token\":\"test-session-token\"}") };
            }
            if (stage.EndsWith("launcherSession")) { events.Add("session"); return new(HttpStatusCode.OK) { Content = new StringContent("{\"sessionId\":\"sid\"}") }; }
            throw new InvalidOperationException("Unexpected authentication request: " + stage);
        }));
        await new TaiwanLogin(client).LoginAsync(root,"test@example.test","p&+","012345","captcha",default, _ => { events.Add("update"); return Task.CompletedTask; });
        Assert.Equal(new[] {"login","update","session"},events);
    }
    [Fact] public async Task TaiwanFailedUpdateDoesNotRequestGameSession()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal(TaiwanLogin.LoginUrl, request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"token\":\"test-token\"}") });
        }));
        await Assert.ThrowsAsync<HttpRequestException>(() => new TaiwanLogin(client).LoginAsync(root,
            "account", "password", "123456", "captcha", default,
            _ => Task.FromException(new HttpRequestException("Patch check failed."))));
        Assert.Equal(1, calls);
    }
    [Fact] public async Task TaiwanRejectedLoginNeverUpdates()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"error\":\"denied\"}") })));
        var updated = false;
        await Assert.ThrowsAsync<TaiwanAuthenticationException>(() => new TaiwanLogin(client).LoginAsync(root,"account","password","123456","captcha",default,_=> { updated=true; return Task.CompletedTask; }));
        Assert.False(updated);
    }
    [Fact] public void ClientVersionRefreshDetectsExternalChangesAndRemoval()
    {
        Directory.CreateDirectory(Path.Combine(root,"game")); var file=Path.Combine(root,"game/ffxivgame.ver"); File.WriteAllText(file,"old");
        var p=new RegionSettings{GamePath=root};VersionRecords.InitializeClient(p);Assert.Equal("old",p.ClientVersion);
        File.WriteAllText(file,"new");VersionRecords.InitializeClient(p);Assert.Equal("new",p.ClientVersion);
        File.Delete(file);VersionRecords.InitializeClient(p);Assert.Equal("",p.ClientVersion);
    }
    [Fact] public async Task GlobalAuthenticatesBeforeBootAndAuthenticatedGameUpdates()
    {
        var settings = LinuxSettings.Load(root); settings.SelectedRegion = "ffxiv"; settings.Account = "test"; settings.GamePath = Path.Combine(root, "client");
        Directory.CreateDirectory(Path.Combine(settings.GamePath,"game")); Directory.CreateDirectory(Path.Combine(settings.GamePath,"boot"));
        File.WriteAllText(Path.Combine(settings.GamePath,"game/ffxivgame.ver"),"2026.01.01.0000.0000"); File.WriteAllText(Path.Combine(settings.GamePath,"boot/ffxivboot.ver"),"2026.01.01.0000.0000");
        foreach(var name in new[]{"ffxivboot.exe","ffxivboot64.exe","ffxivlauncher64.exe","ffxivupdater64.exe"}) File.WriteAllText(Path.Combine(settings.GamePath,"boot",name),"test");
        var events = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            string text;
            if(path.EndsWith("GetLauncherClientConfig")) text="{}";
            else if(path.EndsWith("/top")) { events.Add("top"); text="<input name='_STORED_' value='stored'>"; }
            else if(path.EndsWith("login.send")) { events.Add("login"); text="window.external.user(\"login=auth,ok,x,session,x,1,x,3,x,x,x,1,x,x,x,0\");"; }
            else { events.Add("game-update"); var reply=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("")};reply.Headers.Add("X-Patch-Unique-Id","uid");return Task.FromResult(reply); }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text)});
        }));
        var context = new RegionLoginContext(settings, null, [], new XIVLauncher.Login.Client.LoginRequest(), "synthetic", "", _=>Task.FromResult(""), _=>{}, RequestManualOtp: _=>{events.Add("otp");return Task.FromResult("012345");}, UpdateAfterLogin: _=>{events.Add("boot-update");return Task.CompletedTask;});
        await new XIVLauncher.Linux.Global.GlobalLogin(client).AuthenticateAsync(context, default);
        Assert.Equal(new[]{"top","otp","login","boot-update","game-update"},events);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
