using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Exceptions;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class Release062Tests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private LinuxSettings Settings(string region = "ffxiv_cn")
    { var s = LinuxSettings.Load(root); s.SelectedRegion = region;  s.Current.RememberPassword = true; return s; }
    private static LoginResult Ok(string account, string secret) => new() { State = LoginState.Ok, OAuthLogin = new() { InputUserID = account, QuickLoginSecret = secret, TGT = "one-use-test-ticket", Guid = "test-guid" } };
    [Fact] public async Task ScanPersistsSessionAndProfileThenUsesAndRotatesQuickLoginAfterRestart()
    {
        var s = Settings(); var profile = s.GetDeviceProfile("qr-default");
        await ChinaLoginSession.AuthenticateAsync(s, new() { DeviceProfile = profile }, _=>{}, default,
            (method, request, ct) => { Assert.Equal(LoginType.QRCode, method); Assert.True(request.QuickLoginEnabled); return Task.FromResult(Ok("scan-account", "session-1")); });
        s = LinuxSettings.Load(root); Assert.Equal("scan-account", s.Account);
        Assert.Equal(profile, s.GetDeviceProfile(s.Account));
        Assert.Equal("session-1", await RegionCredentials.ReadAsync(s, s.Account, "session", default));
        Assert.DoesNotContain("one-use-test-ticket", File.ReadAllText(Path.Combine(root,"ffxiv_cn","credentials.json")));
        await ChinaLoginSession.AuthenticateAsync(s, new() { Account = s.Account, DeviceProfile = s.GetDeviceProfile(s.Account) }, _=>{}, default,
            (method, request, ct) => { Assert.Equal(LoginType.QuickLogin, method); Assert.Equal("session-1", request.Secret); return Task.FromResult(Ok(s.Account,"session-2")); });
        Assert.Equal("session-2", await RegionCredentials.ReadAsync(s,s.Account,"session",default));
    }
    [Fact]
    public async Task NewAccountAlwaysEnrollsByQrWithoutSendingPush()
    {
        var s=Settings(); s.Account="new-synthetic-account"; var calls=0;
        await ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account,DeviceProfile=s.GetDeviceProfile(s.Account)},_=>{},default,
            (method,request,ct)=>{calls++;Assert.Equal(LoginType.QRCode,method);return Task.FromResult(Ok(s.Account,"new-secret"));});
        Assert.Equal(1,calls);Assert.Equal("new-secret",await RegionCredentials.ReadAsync(s,s.Account,"session",default));
    }
    [Fact] public async Task ExpiredSessionFallsBackButNetworkFailureDoesNotEraseIt()
    {
        var s=Settings();s.Account="account";await RegionCredentials.StoreAsync(s,s.Account,"session","expired",default);
        var calls=0;
        await ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account},_=>{},default,(method,request,ct)=>
        { calls++; if(method==LoginType.QuickLogin)throw new LoginException(1,"expired",true);Assert.Equal(LoginType.QRCode,method);return Task.FromResult(Ok(s.Account,"new")); });
        Assert.Equal(2,calls);
        await Assert.ThrowsAsync<HttpRequestException>(()=>ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account},_=>{},default,(m,r,c)=>throw new HttpRequestException("offline")));
        Assert.Equal("new",await RegionCredentials.ReadAsync(s,s.Account,"session",default));
    }
    [Fact] public async Task NonExpiryRejectionDoesNotRetryOrDeleteSavedSecret()
    {
        var s=Settings();s.Account="account";await RegionCredentials.StoreAsync(s,s.Account,"session","kept",default);
        var calls=0;
        await Assert.ThrowsAsync<LoginException>(()=>ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account},_=>{},default,
            (method,request,ct)=>{calls++;throw new LoginException(-123,"synthetic rejection",false);}));
        Assert.Equal(1,calls);Assert.Equal("kept",await RegionCredentials.ReadAsync(s,s.Account,"session",default));
    }
    [Fact] public async Task CancellationDoesNotFallBackToQrOrDeleteSecret()
    {
        var s=Settings();s.Account="account";await RegionCredentials.StoreAsync(s,s.Account,"session","kept",default);
        using var cancel=new CancellationTokenSource();var calls=0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account},_=>{},cancel.Token,
            (method,request,ct)=>{calls++;cancel.Cancel();throw new OperationCanceledException(ct);}));
        Assert.Equal(1,calls);Assert.Equal("kept",await RegionCredentials.ReadAsync(s,s.Account,"session",default));
    }
    [Fact] public async Task RememberOffIgnoresSessionAndClearRemovesAllKinds()
    {
        var s=Settings();s.Account="account";
        foreach(var kind in new[]{"session","password","otp"})await RegionCredentials.StoreAsync(s,s.Account,kind,"test",default);
        s.Current.RememberPassword=false;
        await ChinaLoginSession.AuthenticateAsync(s,new(){Account=s.Account},_=>{},default,(m,r,c)=>
        {Assert.Equal(LoginType.QRCode,m);Assert.False(r.QuickLoginEnabled);return Task.FromResult(Ok(s.Account,"ignored"));});
        Assert.Equal("test",await RegionCredentials.ReadAsync(s,s.Account,"session",default));
        await RegionCredentials.ClearAsync(s,s.Account,default);
        foreach(var kind in new[]{"session","password","otp"})Assert.Null(await RegionCredentials.ReadAsync(s,s.Account,kind,default));
    }
    [Fact] public async Task TaiwanPasswordAndOtpAreReadAfterRestartAndIsolatedByAccountAndRegion()
    {
        var s=Settings("ffxiv_tc");s.Account="test@example.invalid";s.Current.AutoOtp=true;s.Save();
        Assert.Equal(2,await RegionCredentials.SaveEnteredAsync(s,"test-password","JBSWY3DPEHPK3PXP",default));
        s=LinuxSettings.Load(root);
        Assert.Equal("test-password",await RegionCredentials.PasswordForLoginAsync(s,"",default));
        Assert.Equal("manual",await RegionCredentials.PasswordForLoginAsync(s,"manual",default));
        Assert.Equal("JBSWY3DPEHPK3PXP",await RegionCredentials.ReadAsync(s,s.Account,"otp",default));
        Assert.Null(await RegionCredentials.ReadAsync(s,"another-account","password",default));
        s.SelectedRegion="ffxiv_cn";Assert.Null(await RegionCredentials.ReadAsync(s,"test@example.invalid","password",default));
    }
    [Fact] public async Task EmptySaveDoesNotPretendToWriteAndInvalidOtpDoesNotWritePassword()
    {
        var s=Settings("ffxiv_tc");s.Account="test";
        await Assert.ThrowsAnyAsync<IOException>(()=>RegionCredentials.SaveEnteredAsync(s,"","",default));
        Assert.False(File.Exists(Path.Combine(root,"ffxiv_tc","credentials.json")));
        await Assert.ThrowsAnyAsync<Exception>(()=>RegionCredentials.SaveEnteredAsync(s,"password","!invalid!",default));
        Assert.Null(await RegionCredentials.ReadAsync(s,s.Account,"password",default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
