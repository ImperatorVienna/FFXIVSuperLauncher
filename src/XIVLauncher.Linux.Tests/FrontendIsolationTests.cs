using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;
public sealed class FrontendIsolationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"frontend-isolation-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    [Theory][InlineData("broken")][InlineData("{\"Accounts\":null}")]
    public async Task CorruptRegionCannotBlockOtherRegionsOrGlobalPreferencesAndIsNeverOverwritten(string broken)
    {
        var settings=LinuxSettings.Load(root);settings.Save();
        var file=Path.Combine(root,"ffxiv","settings.json");File.WriteAllText(file,broken);
        var preferences=new FrontendPreferences(root);
        var snapshot=JsonSerializer.SerializeToElement(preferences.Snapshot(),FrontendHost.JsonOptions);
        Assert.False(snapshot.GetProperty("regions").EnumerateArray().Single(x=>x.GetProperty("id").GetString()=="ffxiv").GetProperty("available").GetBoolean());
        preferences.Update(new(Language:"en"));
        await new FrontendAccounts(root).SaveAsync(new("ffxiv_tc","synthetic",false,"synthetic-password"),default);
        new FrontendApplication(root).SaveWindow(new(0,0,1000,750,false));
        new FrontendCompatibility(root,steamRoots:()=>[]).Read();
        Assert.Throws<IOException>(()=>LinuxSettings.Load(root).RegionProfiles["ffxiv"]);
        Assert.Equal(broken,File.ReadAllText(file));Assert.Equal("en",LinuxSettings.Load(root).Language);
        Assert.Equal("synthetic-password",await RegionCredentials.ReadAsync(root,"ffxiv_tc","synthetic","password",default));
    }
    [Fact] public async Task FailedGlobalSettingsDoNotBlockChinaNews()
    {
        LinuxSettings.Load(root).Save();File.WriteAllText(Path.Combine(root,"ffxiv/settings.json"),"broken");
        await using var official=new FrontendOfficial(root,(_,_,_,_)=>Task.FromResult(new OfficialNews([],[])));
        Assert.Equal("ffxiv_cn",official.Read(new()).Region);
    }
    [Fact] public async Task LateCallbackCannotRestoreACompletedVerification()
    {
        using var operation=new FrontendOperation(root,"ffxiv_cn");operation.Begin(()=>Task.CompletedTask);await operation.Completion;
        operation.NotifyMobile("12345");Assert.Null(operation.View().Challenge);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>operation.AskAsync("otp","late prompt"));
    }
    [Fact] public async Task OtpTimeoutClearsChallengeAndAllowsAnotherOperation()
    {
        await using var operations=new FrontendOperations(root);
        FrontendOperation? first=null;
        operations.Start("ffxiv",async op=>{first=op;await op.AskAsync("otp","synthetic",timeout:TimeSpan.FromMilliseconds(10));});
        for(var i=0;i<500&&operations.Busy;i++)await Task.Delay(5);
        Assert.False(operations.Busy);Assert.NotNull(first);Assert.Null(first.View().Challenge);Assert.Equal("failed",first.View().State);
        operations.Start("ffxiv_cn",_=>Task.CompletedTask);
    }
}
