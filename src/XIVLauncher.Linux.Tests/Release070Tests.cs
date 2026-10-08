using System.Net;
using System.Text;
using System.Text.Json;
using XIVLauncher.Linux.Taiwan;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.DCTravel;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release070Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private LinuxSettings Settings(string region = "ffxiv_tc")
    { var s=LinuxSettings.Load(root);s.SelectedRegion=region;s.Account="account-a";s.Current.RememberPassword=true;return s; }
    [Fact] public async Task PasswordOnlyAllowedSecretOnlyRejectedWithoutWrites()
    {
        var s=Settings();Assert.Equal(1,await RegionCredentials.SaveEnteredAsync(s,"password-a","",default));
        var original=File.ReadAllText(Path.Combine(root,"ffxiv_tc","credentials.json"));
        await Assert.ThrowsAsync<CredentialValidationException>(()=>RegionCredentials.SaveEnteredAsync(s,"","JBSWY3DPEHPK3PXP",default));
        Assert.Equal(original,File.ReadAllText(Path.Combine(root,"ffxiv_tc","credentials.json")));
        Assert.Equal(2,await RegionCredentials.SaveEnteredAsync(s,"password-b","JBSWY3DPEHPK3PXP",default));
        Assert.Equal("password-b",await RegionCredentials.ReadAsync(s,s.Account,"password",default));
    }
    [Fact] public async Task AutoOtpUsesOnlySelectedAccountAndFallsBackToManualWhenMissing()
    {
        var s=Settings();s.Current.AutoOtp=true;
        Assert.Null(await LoginCode.SavedSecretForLoginAsync(s,default));
        await RegionCredentials.StoreAsync(s,s.Account,"otp","JBSWY3DPEHPK3PXP",default);
        Assert.NotNull(await LoginCode.SavedSecretForLoginAsync(s,default));
        s.Account="account-b";
        Assert.Null(await LoginCode.SavedSecretForLoginAsync(s,default));
        s.Current.AutoOtp=false;Assert.Null(await LoginCode.SavedSecretForLoginAsync(s,default));
    }
    [Fact] public async Task ManualOtpRequestedLateAndForwardedUnchangedWhileAutoNeverPrompts()
    {
        var called=false;
        Assert.Equal("012345",await LoginCode.GetAsync(null,ct=>{called=true;return Task.FromResult("012345");},default));
        Assert.True(called);
        var auto=await LoginCode.GetAsync("JBSWY3DPEHPK3PXP",ct=>throw new Exception("Manual prompt used"),default);
        Assert.True(OneTimePassword.IsManualCode(auto));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>LoginCode.GetAsync(null,ct=>throw new Exception("Prompt after cancellation"),new CancellationToken(true)));
    }
    [Fact] public async Task AccountSwitchRestoresPreferencesAndDeletionIsRegionScoped()
    {
        var s=Settings();s.Current.AutoOtp=true;s.AreaName="area-a";s.Save();
        await RegionCredentials.StoreAsync(s,s.Account,"password","a",default);
        AccountProfiles.Select(s.Current,"account-b");s.Current.RememberPassword=true;s.AreaName="area-b";s.Save();
        await RegionCredentials.StoreAsync(s,s.Account,"password","b",default);
        AccountProfiles.Select(s.Current,"account-a");Assert.True(s.Current.AutoOtp);Assert.Equal("area-a",s.AreaName);s.Save();
        s=LinuxSettings.Load(root);Assert.Equal(2,s.Current.Accounts.Count);
        s.SelectedRegion="ffxiv";s.Account="account-a";s.Save();await RegionCredentials.StoreAsync(s,s.Account,"password","global",default);
        s.SelectedRegion="ffxiv_tc";await AccountProfiles.DeleteAsync(s,"account-a",default);
        Assert.False(s.Current.Accounts.ContainsKey("account-a"));Assert.Equal("",s.Account);
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv_tc","account-a","password",default));
        Assert.Equal("b",await RegionCredentials.ReadAsync(root,"ffxiv_tc","account-b","password",default));
        Assert.Equal("global",await RegionCredentials.ReadAsync(root,"ffxiv","account-a","password",default));
    }
    [Fact] public async Task FailedDeletionRetainsRecordAndRetriesFileDeletion()
    {
        var s=Settings();s.Save();
        await Assert.ThrowsAsync<IOException>(()=>AccountProfiles.DeleteAsync(s,s.Account,default,(a,ct)=>throw new IOException("locked")));
        Assert.True(s.Current.Accounts.ContainsKey(s.Account));
        var seen=new HashSet<string>();await AccountProfiles.DeleteAsync(s,s.Account,default,(a,ct)=>{seen.Add(a);return Task.CompletedTask;});
        Assert.Single(seen);Assert.Empty(s.Current.Accounts);
    }
    [Fact] public async Task OrderMonitorConfirmsOnceAndNeverStartsNewOrder()
    {
        var calls=0;var confirmations=0;
        var states=new[]{DCTravelStatusType.Checking,DCTravelStatusType.NeedConfirmation,DCTravelStatusType.NeedConfirmation,DCTravelStatusType.Processing,DCTravelStatusType.Success};
        await TravelOrderMonitor.WaitAsync(id=>Task.FromResult(new DCTravelOrderInfo{Status=states[calls++]}),(id,yes)=>{Assert.True(yes);confirmations++;return Task.CompletedTask;},"order",_=>{},default,_=>Task.CompletedTask);
        Assert.Equal(1,confirmations);Assert.Equal(5,calls);
        await Assert.ThrowsAsync<IOException>(()=>TravelOrderMonitor.WaitAsync(id=>Task.FromResult(new DCTravelOrderInfo{Status=DCTravelStatusType.TravelFailed}),(_,_)=>throw new Exception(),"order",_=>{},default));
    }
    [Fact] public async Task TravelSubmissionNetworkFailureIsNotRetried()
    {
        var count=0;
        using var transport=new FakeTransport(request=>
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("travelOrder")){count++;throw new HttpRequestException("connection lost");}
            return new(HttpStatusCode.OK){Content=new StringContent("{\"return_code\":0,\"data\":{}}")};
        });
        using var client=new DCTravelClient("",transport);await client.GetValidCookie();
        var group=new DCTravelGroup{AreaID=1,AreaName="area",GroupID=2,GroupCode="group",GroupName="name"};
        await Assert.ThrowsAsync<HttpRequestException>(()=>client.TravelOrder(group,group,new(){ContentID="role",Name="name"}));
        Assert.Equal(1,count);
    }
    [Fact] public async Task LoginRequestPreservesManualCodeAndEncodesOnlyAccountAndPassword()
    {
        Directory.CreateDirectory(Path.Combine(root,"game"));File.WriteAllText(Path.Combine(root,"game","ffxivgame.ver"),"2026.01.01.0000.0000");
        var bodies=new List<JsonElement>();
        using var handler=new FakeTransport(request=>
        {
            if(request.RequestUri!.Host=="user.ffxiv.com.tw")
            {
                bodies.Add(JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result).RootElement.Clone());
                return new(HttpStatusCode.OK){Content=new StringContent(bodies.Count==1?"{\"token\":\"test-token\"}":"{\"sessionId\":\"test-session\"}")};
            }
            return new(HttpStatusCode.OK){Content=new StringContent("")};
        });
        using var http=new HttpClient(handler);
        await new TaiwanLogin(http).LoginAsync(root,"email","password","012345","captcha",default);
        Assert.Equal("012345",bodies[0].GetProperty("code").GetString());
        Assert.Equal("captcha",bodies[0].GetProperty("token").GetString());
        Assert.Equal(TaiwanLogin.EncodeCredential("password"),bodies[0].GetProperty("password").GetString());
    }
    [Fact] public async Task SelectedDataCenterChangesOnlyAfterConfirmedSuccessAndPersistsPerAccount()
    {
        var s=Settings("ffxiv_cn");s.AreaName="source";s.Save();
        void Apply(){s.AreaName="target";s.Save();}
        await Assert.ThrowsAsync<IOException>(()=>TravelOrderMonitor.WaitAsync(id=>Task.FromResult(new DCTravelOrderInfo{Status=DCTravelStatusType.PreCheckFailed}),(_,_)=>Task.CompletedTask,"id",_=>{},default,onSuccess:Apply));
        Assert.Equal("source",LinuxSettings.Load(root).AreaName);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>TravelOrderMonitor.WaitAsync(id=>Task.FromResult(new DCTravelOrderInfo{Status=DCTravelStatusType.Success}),(_,_)=>Task.CompletedTask,"id",_=>{},new CancellationToken(true),onSuccess:Apply));
        Assert.Equal("source",s.AreaName);
        await TravelOrderMonitor.WaitAsync(id=>Task.FromResult(new DCTravelOrderInfo{Status=DCTravelStatusType.Success}),(_,_)=>Task.CompletedTask,"id",_=>{},default,onSuccess:Apply);
        var reloaded=LinuxSettings.Load(root);Assert.Equal("target",reloaded.AreaName);Assert.Equal("target",reloaded.Current.Accounts[s.Account].AreaName);
    }
    [Fact] public async Task AccountRecordWithoutSecretsCanBeDeleted()
    {
        var s=Settings();s.Save();
        await AccountProfiles.DeleteAsync(s,s.Account,default);
        Assert.Empty(s.Current.Accounts);
    }
    [Fact] public async Task ServerRejectionRetainsSuccessfulHttpStatusInsteadOfReportingNoResponse()
    {
        using var transport=new FakeTransport(_=>new(HttpStatusCode.OK){Content=new StringContent("{\"error\":\"連線錯誤\"}")});
        using var http=new HttpClient(transport);
        var error=await Assert.ThrowsAsync<TaiwanAuthenticationException>(()=>new TaiwanLogin(http).LoginAsync(root,"account","password","012345","captcha",default));
        Assert.Contains("HTTP=200",error.Diagnostic);Assert.Contains("server-connection",error.Diagnostic);
    }
    private sealed class FakeTransport(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(send(request)); }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
