using System.Net;
using System.Text.Json;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Linux.Global;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class GlobalAuthenticationResponseTests
{
    private static string Callback(string value) => "window.external.user(" + JsonSerializer.Serialize(value) + ");";
    [Fact] public void ExplicitRejectionPreservesServerExplanationOnlyInPrompt()
    {
        var ex = Assert.Throws<GlobalAuthenticationException>(() => GlobalLogin.ParseSession(
            Callback("login=auth,ng,err,The password entered is invalid."), true, 200));
        Assert.Contains("server-rejected", ex.Message); Assert.Contains("mode=steam", ex.Message);
        Assert.Contains("The password entered is invalid.", ex.UserMessage);
        Assert.DoesNotContain("The password entered", DiagnosticLog.Format(ex));
    }
    [Theory]
    [InlineData("<html>password=private; ticket=secret</html>")]
    [InlineData("window.external.user(\"login=auth,other,secret\");")]
    [InlineData("window.external.user(\"login=auth,ng,err,broken\\q\");")]
    public void UnexpectedResponseDoesNotInventCredentialFailureOrLeakPage(string html)
    {
        var ex = Assert.Throws<GlobalAuthenticationException>(() => GlobalLogin.ParseSession(html));
        Assert.Contains("unexpected-response", ex.Message); Assert.Null(ex.ServerMessage);
        Assert.DoesNotContain("secret", ex.UserMessage + DiagnosticLog.Format(ex));
    }
    [Fact] public void ErrorEscapesMarkupAndEchoedSecretsAreSanitized()
    {
        var html = Callback("login=auth,ng,err,<b>Failed</b> accountName p&+p token-value 012345 user@example.test https://example.test/secret\nRetry.");
        var ex = Assert.Throws<GlobalAuthenticationException>(() => GlobalLogin.ParseSession(html, true, 200, "accountName", "p&+p", "token-value", "012345"));
        foreach (var secret in new[] { "accountName", "p&+p", "token-value", "012345", "user@example.test", "https://", "<b>", "\n" })
            Assert.DoesNotContain(secret, ex.ServerMessage!);
        Assert.Contains("Retry.", ex.ServerMessage!);
    }
    [Fact] public void WhitespaceAndEscapedSuccessCallbackStillYieldSession()
    {
        var html = Callback("login=auth,ok,x,session,x,1,x,3,x,x,x,1,x,x,x,5").Replace("window.external.user(", "window . external . user ( \n").Replace(");", " );");
        Assert.Equal(new GlobalSession("session", 3, 5), GlobalLogin.ParseSession(html));
    }
    [Fact] public async Task SavedGlobalCredentialsReachSteamFormUnchanged()
    {
        var root=Path.Combine(Path.GetTempPath(),"super-global-wire-"+Guid.NewGuid());
        try
        {
            var settings=LinuxSettings.Load(root);settings.SelectedRegion="ffxiv";
            settings.Account="Account"; 
            settings.Current.RememberPassword=true;settings.Current.AutoOtp=true;settings.Save();
            const string password=" A+&%\"'\\=z ";
            const string secret="GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
            await RegionCredentials.StoreAsync(settings,"Account","password",password,default);
            await RegionCredentials.StoreAsync(settings,"Account","otp",secret,default);
            settings=LinuxSettings.Load(root);
            var loadedPassword=await RegionCredentials.PasswordForLoginAsync(settings,"",default);
            var loadedSecret=await LoginCode.SavedSecretForLoginAsync(settings,default);
            Assert.Equal(secret,loadedSecret);
            var otp=OneTimePassword.Generate(loadedSecret!,DateTimeOffset.FromUnixTimeSeconds(59));
            var ticket=Ticket.EncryptAuthSessionTicket(Enumerable.Range(0,256).Select(i=>(byte)i).ToArray(),1700000000);
            Assert.Contains(",",ticket.Text);
            var calls=0;
            using var client=new HttpClient(new Handler(async request=>
            {
                calls++;
                if(request.Method==HttpMethod.Get)
                {
                    // Verify complete ticket transmission alongside credential preservation.
                    Assert.Contains("session_ticket="+Uri.EscapeDataString(ticket.Text),request.RequestUri!.OriginalString);
                    return new(HttpStatusCode.OK){Content=new StringContent("<input name='_STORED_' value='fixture'><input name='sqexid' value='Account'>")};
                }
                var form=(await request.Content!.ReadAsStringAsync()).Split('&').Select(p=>p.Split('=',2))
                    .ToDictionary(p=>WebUtility.UrlDecode(p[0]),p=>WebUtility.UrlDecode(p[1]));
                Assert.Equal(password,form["password"]);Assert.Equal("287082",form["otppw"]);
                Assert.Equal("Account",form["sqexid"]);Assert.Equal("fixture",form["_STORED_"]);
                return new(HttpStatusCode.OK){Content=new StringContent(Callback("login=auth,ok,x,session,x,1,x,3,x,x,x,1,x,x,x,5"))};
            }));
            await new GlobalLogin(client).LoginAsync(settings.Account,loadedPassword,false,ticket,1,_=>Task.FromResult(otp),default);
            Assert.Equal(2,calls);
        }
        finally { if(Directory.Exists(root))Directory.Delete(root,true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
    [Fact] public async Task SteamWireRequestCarriesTicketAndSanitizedServerFailureWithoutRetry()
    {
        var calls = 0;
        var ticket = Ticket.EncryptAuthSessionTicket([1, 2, 3, 4], 1700000000);
        string top = "";
        using var client = new HttpClient(new Handler(async request =>
        {
            calls++;
            if (request.Method == HttpMethod.Get)
            {
                top = request.RequestUri!.AbsoluteUri;
                Assert.Contains("issteam=1", top); Assert.Contains("isft=0", top);
                Assert.Contains("session_ticket=" + Uri.EscapeDataString(ticket.Text), top);
                Assert.Contains("ticket_size=" + ticket.Length, top);
                return new(HttpStatusCode.OK) { Content = new StringContent("<input name='_STORED_' value='stored-secret'><input name='sqexid' value='Account'>") };
            }
            Assert.Equal(top, request.Headers.Referrer!.AbsoluteUri);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("sqexid=Account", body); Assert.Contains("password=p%26%2B", body); Assert.Contains("otppw=012345", body);
            return new(HttpStatusCode.OK) { Content = new StringContent(Callback("login=auth,ng,err,Denied Account p&+ 012345 stored-secret " + ticket.Text)) };
        }));
        var ex = await Assert.ThrowsAsync<GlobalAuthenticationException>(() => new GlobalLogin(client).LoginAsync("account", "p&+", false, ticket, 1, _ => Task.FromResult("012345"), default));
        Assert.Equal(2, calls); Assert.Contains("mode=steam", ex.Message);
        foreach (var secret in new[] { "Account", "p&+", "012345", "stored-secret", ticket.Text }) Assert.DoesNotContain(secret, ex.UserMessage);
    }
}
