using System.Text.Json;
using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class FrontendIntegrationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"frontend-integration-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,FrontendHost.JsonOptions);
    [Fact] public async Task AccountEditorUsesSharedStoreAndOnlyExplicitReadsExposeSecrets()
    {
        var service=new FrontendAccounts(root);
        await service.SaveAsync(new("ffxiv","demo",false,"synthetic-password","JBSWY3DPEHPK3PXP"),default);
        await service.SaveAsync(new("ffxiv_cn","demo-cn",false),default);
        var list=Json(await service.ListAsync("ffxiv",default));
        Assert.Single(list.GetProperty("accounts").EnumerateArray());Assert.DoesNotContain("demo-cn",list.ToString());
        Assert.DoesNotContain("synthetic-password",list.ToString());Assert.DoesNotContain("JBSWY3DPEHPK3PXP",list.ToString());
        Assert.True(list.GetProperty("accounts")[0].GetProperty("hasOtp").GetBoolean());
        var loaded=Json(await service.ReadAsync(new("ffxiv","demo"),default));
        Assert.Equal("synthetic-password",loaded.GetProperty("password").GetString());
        await service.SaveAsync(new("ffxiv","demo",true,"synthetic-password","",Revision:loaded.GetProperty("revision").GetString()),default);
        Assert.Null(await RegionCredentials.ReadAsync(root,"ffxiv","demo","otp",default));
        Assert.Equal("synthetic-password",await RegionCredentials.ReadAsync(root,"ffxiv","demo","password",default));
        var current=Json(await service.ReadAsync(new("ffxiv","demo"),default));
        await service.SaveAsync(new("ffxiv","demo",true,Delete:true,Revision:current.GetProperty("revision").GetString()),default);
        Assert.Empty(LinuxSettings.Load(root).RegionProfiles["ffxiv"].Accounts);
        Assert.Contains("demo-cn",LinuxSettings.Load(root).RegionProfiles["ffxiv_cn"].Accounts.Keys);
    }
    [Fact] public async Task InvalidSecretOrStaleEditorCannotOverwriteStoredCredentials()
    {
        var service=new FrontendAccounts(root);
        await Assert.ThrowsAsync<CredentialValidationException>(()=>service.SaveAsync(new("ffxiv_tc","demo",false,"","JBSWY3DPEHPK3PXP"),default));
        Assert.False(Directory.Exists(root));
        await service.SaveAsync(new("ffxiv_tc","demo",false,"original"),default);
        var read=Json(await service.ReadAsync(new("ffxiv_tc","demo"),default));
        await RegionCredentials.StoreValueAsync(root,"ffxiv_tc","demo","password","changed elsewhere",default);
        await Assert.ThrowsAsync<CredentialValidationException>(()=>service.SaveAsync(new("ffxiv_tc","demo",true,"stale",Revision:read.GetProperty("revision").GetString()),default));
        Assert.Equal("changed elsewhere",await RegionCredentials.ReadAsync(root,"ffxiv_tc","demo","password",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveAsync(new("ffxiv_cn","new",false,"password"),default));
    }
    [Fact] public async Task OperationsAcceptOtpWithoutLoggingItAndRejectWrongOrReplayedChallenges()
    {
        var pending=new TaskCompletionSource<FrontendOperation>();var release=new TaskCompletionSource();
        await using var service=new FrontendOperations(root,async(_,op)=>{
            pending.SetResult(op);
            var otp=await op.AskAsync("otp","Enter code",optional:true);
            Assert.Equal("123456",otp);await release.Task;
        });
        var id=Json(service.Start(new("ffxiv","launch"))).GetProperty("operationId").GetString()!;
        var operation=await pending.Task;
        var challenge=await WaitForChallenge(operation);
        Assert.Throws<InvalidOperationException>(()=>service.Start(new("ffxiv_tc","launch")));
        Assert.Throws<ArgumentException>(()=>service.Reply(new(id,"old-challenge","123456")));
        Assert.Throws<ArgumentException>(()=>service.Reply(new(id,challenge.Id,"12345")));
        service.Reply(new(id,challenge.Id,"123456"));
        Assert.Throws<ArgumentException>(()=>service.Reply(new(id,challenge.Id,"123456")));
        release.SetResult();await operation.Completion;
        var view=service.Poll(new(id));Assert.Equal("completed",view.State);Assert.Null(view.Challenge);Assert.DoesNotContain("123456",view.Log);
    }
    [Fact] public async Task CancellationClearsVerificationAndAllowsNextIndependentOperation()
    {
        var pending=new TaskCompletionSource<FrontendOperation>();
        await using var service=new FrontendOperations(root,async(_,op)=>{pending.TrySetResult(op);await op.AskAsync("otp","Enter code");});
        var id=Json(service.Start(new("ffxiv_tc","launch"))).GetProperty("operationId").GetString()!;
        var operation=await pending.Task;await WaitForChallenge(operation);
        service.Cancel(new(id));await operation.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        var state=service.Poll(new(id));Assert.Equal("cancelled",state.State);Assert.Null(state.Challenge);Assert.False(service.Busy);
        service.Start(new("ffxiv_cn","launch"));Assert.True(service.Busy);
        Assert.Throws<ArgumentException>(()=>service.Reply(new(id,"stale","123456")));
    }
    [Fact] public async Task VerificationTimeoutAndBackendExceptionReleaseBusyStateWithoutEchoingSensitiveErrors()
    {
        var operation=new FrontendOperation(root,"ffxiv");
        operation.Begin(async()=>{await operation.AskAsync("otp","Code",timeout:TimeSpan.FromMilliseconds(20));});
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("failed",operation.View().State);Assert.Null(operation.View().Challenge);Assert.False(operation.Busy);
        operation.Dispose();
        var broken=new FrontendOperation(root,"ffxiv");
        broken.Begin(()=>throw new IOException("synthetic-private-password=123456"));await broken.Completion;
        Assert.DoesNotContain("synthetic-private-password",broken.View().Log);
        foreach(var file in Directory.GetFiles(Path.Combine(root,"logs")))Assert.DoesNotContain("synthetic-private-password",File.ReadAllText(file));
        broken.Dispose();
    }
    [Fact] public async Task QrAndMobileNotificationsAreSeparateFromLogsAndDalamudLocalChoiceIsValidated()
    {
        var operation=new FrontendOperation(root,"ffxiv_cn");
        operation.Notify("qr","Scan", "data:image/png;base64,c3ludGhldGlj");
        Assert.Equal("qr",operation.View().Challenge!.Kind);
        operation.NotifyMobile("01234");
        Assert.Equal("01234",operation.View().Challenge!.Code);
        Assert.DoesNotContain("01234",operation.View().Challenge!.Message);Assert.DoesNotContain("01234",operation.View().Log);
        Assert.Throws<ArgumentException>(()=>operation.NotifyMobile("server-message-or-session-key"));
        var wait=operation.AskAsync("dalamud-timeout","Update timed out",allowLocal:false);
        var challenge=operation.View().Challenge!;
        Assert.Throws<ArgumentException>(()=>operation.Reply(new(operation.Id,challenge.Id,"local")));
        operation.Reply(new(operation.Id,challenge.Id,"disable"));Assert.Equal("disable",await wait);operation.Dispose();
    }
    [Fact] public void SharedClientLockPreventsTwoFrontendsFromPatchingTheSameClient()
    {
        Directory.CreateDirectory(root);
        using(var first=GameLaunchCoordinator.AcquireClientLock(root))Assert.Throws<IOException>(()=>GameLaunchCoordinator.AcquireClientLock(root));
        using var next=GameLaunchCoordinator.AcquireClientLock(root);
    }
    [Fact] public async Task ChinaAuthenticationErrorKeepsCodeAndDoesNotExposeServerDetails()
    {
        using var operation=new FrontendOperation(root,"ffxiv_cn");
        operation.Begin(()=>throw new XIVLauncher.Login.Exceptions.LoginException(-123,"synthetic-private-account"));
        await operation.Completion;
        Assert.Contains("-123",operation.View().Error);
        Assert.DoesNotContain("synthetic-private-account",operation.View().Log);
        foreach(var file in Directory.GetFiles(Path.Combine(root,"logs")))Assert.DoesNotContain("synthetic-private-account",File.ReadAllText(file));
    }
    [Fact] public async Task ClosingOperationHostCancelsPendingOtpAndRunsCleanup()
    {
        var pending=new TaskCompletionSource<FrontendOperation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned=false;
        var service=new FrontendOperations(root,async(_,operation)=>
        {
            pending.SetResult(operation);
            try{await operation.AskAsync("otp","Code");}finally{cleaned=true;}
        });
        service.Start(new("ffxiv","launch"));var op=await pending.Task;await WaitForChallenge(op);
        await service.DisposeAsync();
        Assert.True(cleaned);Assert.Equal("cancelled",op.View().State);Assert.Null(op.View().Challenge);
    }
    [Fact] public async Task SlowAreaLookupDoesNotBlockOtherRequestsAndIsCancelledOnEof()
    {
        var output=new StringWriter();
        var input=new StringReader("""
            {"id":"areas","method":"areas.list","params":{"region":"ffxiv_cn"}}
            {"id":"settings","method":"snapshot","params":{}}
            """);
        var cleanup=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await FrontendHost.RunAsync(root,input,output,fetchAreas:async(_,token)=>
        {
            try {await Task.Delay(Timeout.Infinite,token);return [];}
            finally {cleanup.TrySetResult(token.IsCancellationRequested);}
        });
        var responses=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToArray();
        Assert.Equal("settings",responses[0].GetProperty("id").GetString());
        Assert.Equal("areas",responses[1].GetProperty("id").GetString());Assert.True(await cleanup.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    private static async Task<FrontendChallenge> WaitForChallenge(FrontendOperation operation)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while(operation.View().Challenge==null)await Task.Delay(1,timeout.Token);
        return operation.View().Challenge!;
    }
}
