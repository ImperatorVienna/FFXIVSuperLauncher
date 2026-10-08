using System.Net;
using System.Security.Cryptography;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Linux.Global;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release080Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "super080-" + Guid.NewGuid());
    private const string Version = "2026.01.01.0000.0000";
    private const string Callback = "window.external.user(\"login=auth,ok,x,session,x,1,x,3,x,x,x,1,x,x,x,5\");";
    private void Game()
    {
        Directory.CreateDirectory(Path.Combine(root,"game")); Directory.CreateDirectory(Path.Combine(root,"boot"));
        File.WriteAllText(Path.Combine(root,"game/ffxivgame.ver"),Version);File.WriteAllText(Path.Combine(root,"boot/ffxivboot.ver"),Version);
        foreach(var name in new[]{"ffxivboot.exe","ffxivboot64.exe","ffxivlauncher64.exe","ffxivupdater64.exe"})File.WriteAllText(Path.Combine(root,"boot",name),"synthetic");
    }
    private static HttpResponseMessage Response(string value) => new(HttpStatusCode.OK){Content=new StringContent(value)};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> action):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return action(request);} }
    [Fact] public async Task ManualOtpIsRequestedAfterLoginPageAndImmediatelyPostedWithoutLosingLeadingZero()
    {
        var events = new List<string>();
        using var client = new HttpClient(new Handler(async request => {
            if(request.Method==HttpMethod.Get){events.Add("top");return Response("<input value='a&amp;b' name='_STORED_' />");}
            events.Add("post");var body=await request.Content!.ReadAsStringAsync();Assert.Contains("otppw=012345",body);Assert.Contains("password=p%26%2B",body);Assert.Contains("_STORED_=a%26b",body);return Response(Callback);
        }));
        var session=await new GlobalLogin(client).LoginAsync("account","p&+",false,null,1,_=>{events.Add("otp");return Task.FromResult("012345");},default);
        Assert.Equal(new[]{"top","otp","post"},events);Assert.Equal(new GlobalSession("session",3,5),session);
    }
    [Fact] public async Task SteamLinkedAccountMismatchStopsBeforeOtpOrPasswordPost()
    {
        using var client=new HttpClient(new Handler(request=>{Assert.Equal(HttpMethod.Get,request.Method);Assert.Contains("issteam=1",request.RequestUri!.Query);return Task.FromResult(Response("<input name='_STORED_' value='stored'><input name='sqexid' value='different'>"));}));
        var ticket=Ticket.EncryptAuthSessionTicket([1,2,3,4],1700000000);
        await Assert.ThrowsAsync<CredentialValidationException>(()=>new GlobalLogin(client).LoginAsync("account","password",false,ticket,1,_=>throw new Exception("OTP must not be requested"),default));
    }
    [Fact] public async Task SteamUsesServerCanonicalAccountAndTrialFlag()
    {
        using var client=new HttpClient(new Handler(async request=>{
            if(request.Method==HttpMethod.Get){Assert.Contains("isft=1",request.RequestUri!.Query);return Response("<input name='_STORED_' value='stored'><input name='sqexid' value='Account'>");}
            Assert.Contains("sqexid=Account",await request.Content!.ReadAsStringAsync());return Response(Callback);
        }));
        await new GlobalLogin(client).LoginAsync("account","password",true,Ticket.EncryptAuthSessionTicket([1,2,3,4],1700000000),1,_=>Task.FromResult(""),default);
    }
    [Fact] public void RejectionsDoNotLogServerHtmlOrSecrets()
    {
        var ex=Assert.Throws<GlobalAuthenticationException>(()=>GlobalLogin.ParseSession("<html>password=secret; session=private</html>"));
        Assert.DoesNotContain("secret",DiagnosticLog.Format(ex));Assert.DoesNotContain("private",DiagnosticLog.Format(ex));
        Assert.Throws<IOException>(()=>GlobalLogin.ParseSession(Callback.Replace(",1,x,3",",0,x,3")));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void InternationalLanguageAndSteamFlagReachGameArguments(int language)
    {
        Game();var args=GlobalLogin.Arguments("uid",new("session",3,5),root,language,true);
        Assert.Contains("language="+language,args);Assert.Contains("IsSteam=1",args);Assert.Contains("DEV.TestSID=uid",args);
        Assert.DoesNotContain("IsSteam",GlobalLogin.Arguments("uid",new("session",3,5),root,language,false));
    }
    [Fact] public void VersionReportHashesBootAndIncludesMissingEntitledExpansionBaseVersion()
    {
        Game();var report=GlobalGameUpdater.VersionReport(root,2);
        Assert.Contains("ffxivboot.exe/9/"+Convert.ToHexString(SHA1.HashData("synthetic"u8)).ToLowerInvariant(),report);
        Assert.Contains("ex2\t"+GlobalGameUpdater.BaseVersion,report);Assert.DoesNotContain("ex3",report);
    }
    [Fact] public async Task AuthenticatedPatchesReuseInstallerAndUidWithoutStartingAnyProcess()
    {
        Game();var next="2026.10.07.0000.0000";
        var bytes=Convert.FromHexString("915A4950415443480D0A1A0A00000000454F465F00000000");
        var manifest=$"{bytes.Length}\t0\t0\t0\t{next}\tsha1\t100\t{Convert.ToHexString(SHA1.HashData(bytes))}\thttp://patch-dl.ffxiv.com/game/4e9a232b/D{next}.patch";
        var posts=0;
        using var client=new HttpClient(new Handler(request=>{
            if(request.Method==HttpMethod.Post){posts++;Assert.EndsWith("/session",request.RequestUri!.AbsolutePath);var r=Response(manifest);r.Headers.Add("X-Patch-Unique-Id","uid");return Task.FromResult(r);}
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
        }));
        Assert.Equal("uid",await new GlobalGameUpdater(client).RegisterAndUpdateAsync(root,Path.Combine(root,"ffxiv"),new("session",3,0),_=>{},default));
        Assert.Equal(1,posts);Assert.Equal(next,File.ReadAllText(Path.Combine(root,"game/ffxivgame.ver")));Assert.Equal(next,File.ReadAllText(Path.Combine(root,"game/ffxivgame.bck")));
    }
    [Fact] public void BootManifestAndInvalidSourcesAreDistinguished()
    {
        Assert.Single(GlobalGameUpdater.Parse("--boundary\nContent-Type: application/octet-stream\n\n1\t1\t1\t0\t"+Version+"\thttp://patch-dl.ffxiv.com/boot/repo/file.patch\n--boundary--",true));
        Assert.Throws<IOException>(()=>GlobalGameUpdater.Parse("1\t1\t1\t0\t"+Version+"\thttp://patch-dl.ffxiv.com.evil.test/boot/file",true));
        Assert.Throws<IOException>(()=>GlobalGameUpdater.Parse("<html>error</html>",false));
    }
    [Fact] public async Task CredentialsAndSteamTrialPreferencesAreAccountAndRegionScoped()
    {
        var settings=LinuxSettings.Load(root);settings.SelectedRegion="ffxiv";settings.Account="first";settings.Current.RememberPassword=true;settings.Current.SteamAccount=true;settings.Current.FreeTrial=true;
        Assert.Equal(1,await RegionCredentials.SaveEnteredAsync(settings,"password","",default));
        await Assert.ThrowsAsync<CredentialValidationException>(()=>RegionCredentials.SaveEnteredAsync(settings,"","JBSWY3DPEHPK3PXP",default));
        settings.Current.AutoOtp=true;Assert.Null(await LoginCode.SavedSecretForLoginAsync(settings,default));
        AccountProfiles.Select(settings.Current,"second");Assert.False(settings.Current.SteamAccount);Assert.False(settings.Current.FreeTrial);
        AccountProfiles.Select(settings.Current,"first");Assert.True(settings.Current.SteamAccount);Assert.True(settings.Current.FreeTrial);
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv_tc","first","password",default));
    }
    [Fact] public async Task GlobalDalamudAdapterSuppliesMetadataToSharedZipInstaller()
    {
        using var client=new HttpClient(new Handler(request=>Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("Meta")?
            "{\"version\":1,\"assets\":[{\"fileName\":\"UIRes/font\",\"url\":\"https://example.test/font\",\"hash\":null}]}" :
            "{\"assemblyVersion\":\"15.0.3.6\",\"runtimeVersion\":\"10.0.0\",\"downloadUrl\":\"https://example.test/dalamud\"}"))));
        var distribution=await DalamudSources.For("ffxiv").ResolveAsync(client,default);Assert.True(distribution.Zip);Assert.False(distribution.Soil);Assert.Null(distribution.Hashes);Assert.Equal("10.0.0",distribution.RuntimeVersion);Assert.Single(distribution.Assets);
    }
    [Fact] public void NewsUsesLodestoneIdsAndDiscardsNonOfficialLinks()
    {
        var news=OfficialContent.ParseGlobal("{\"topics\":[{\"title\":\"Event\",\"id\":\"abc\"}],\"news\":[{\"title\":\"bad\",\"url\":\"javascript:alert(1)\"}]}","na");
        Assert.Equal("https://na.finalfantasyxiv.com/lodestone/topics/detail/abc",Assert.Single(news.Activities).Url);Assert.Empty(news.Notices);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
