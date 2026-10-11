using System.Net;
using System.IO.Compression;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using XIVLauncher.Linux.Frontend;
using Xunit;
namespace XIVLauncher.Linux.Tests;

public sealed class FrontendPluginTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"frontend-plugins-"+Guid.NewGuid());
    private const string Region="ffxiv_cn";
    private const string Repo="https://plugins.invalid/master.json";
    private string Config=>Path.Combine(root,Region,"dalamud/dalamudConfig.json");
    private string Plugin(string region=Region,string name="Example",bool delete=false)
    {
        var dir=Path.Combine(root,region,"dalamud/installedPlugins",name,"1.0.0.0");Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,name+".dll"),"never executed");
        File.WriteAllText(Path.Combine(dir,name+".json"),new JObject{["InternalName"]=name,["Name"]=name,["WorkingPluginId"]=Guid.NewGuid().ToString(),["AssemblyVersion"]="1.0.0.0",["InstalledFromUrl"]=Repo,["ScheduledForDeletion"]=delete}.ToString());
        return dir;
    }
    private void Settings()
    {
        var settings=LinuxSettings.Load(root);settings.SelectedRegion=Region;settings.EnableDalamud=false;settings.Save();
    }
    private static JsonElement Json(object data)=>JsonSerializer.SerializeToElement(data,FrontendHost.JsonOptions);
    private static async Task<OperationView> Wait(FrontendOperations operations,object start)
    {
        var id=Json(start).GetProperty("operationId").GetString()!;
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(true){var view=operations.Poll(new(id));if(view.State is not ("running" or "cancelling"))return view;await Task.Delay(5,stop.Token);}
    }
    [Fact] public async Task ReadRefreshAndApplyUseSharedStoreWithPrivateSnapshotAndBackups()
    {
        Settings();Plugin();Plugin("ffxiv_tc");var malformed=Plugin(name:"Broken");File.WriteAllText(Path.Combine(malformed,"Broken.json"),"invalid");
        File.WriteAllText(Config,"{\"PrivatePluginSetting\":\"not-a-frontend-field\"}");
        var service=new FrontendPlugins(root,ensureIdle:()=>{});var list=service.Read(Region);
        Assert.Single(list.Plugins);Assert.Equal(1,list.ProblemCount);Assert.DoesNotContain("ManifestPath",Json(list).ToString());Assert.DoesNotContain("not-a-frontend-field",Json(list).ToString());
        var before=File.ReadAllText(Config);
        await using var operations=new FrontendOperations(root);
        var result=await Wait(operations,service.Start(new(Region,"apply",list.Revision,new(){["Example"]=false}),operations));
        Assert.Equal("completed",result.State);Assert.False(service.Read(Region).Plugins.Single().Enabled);Assert.True(service.Read("ffxiv_tc").Plugins.Single().Enabled);
        var backup=Json(result.Result!).GetProperty("backupDirectory").GetString()!;Assert.Equal(before,File.ReadAllText(Path.Combine(backup,"dalamudConfig.json")));
        Assert.Contains("not-a-frontend-field",File.ReadAllText(Config));
        File.WriteAllText(Config,"{}");Assert.True(service.Read(Region).Plugins.Single().Enabled);
    }
    [Fact] public async Task StaleAndForgedSelectionsCannotModifyFiles()
    {
        Settings();Plugin();Plugin(name:"Deleted",delete:true);var service=new FrontendPlugins(root,ensureIdle:()=>{});
        var old=service.Read(Region);var current=service.Read(Region);await using var operations=new FrontendOperations(root);
        Assert.Throws<CredentialValidationException>(()=>service.Start(new(Region,"apply",old.Revision,new(){["Example"]=false}),operations));
        foreach(var name in new[]{"../../outside","Deleted"})Assert.Throws<ArgumentException>(()=>service.Start(new(Region,"apply",current.Revision,new(){[name]=false}),operations));
        Plugin(); // A reinstalled plugin has a different WorkingPluginId.
        var outcome=await Wait(operations,service.Start(new(Region,"apply",current.Revision,new(){["Example"]=false}),operations));
        Assert.Equal("failed",outcome.State);Assert.False(File.Exists(Config));
        var preferences=new FrontendPreferences(root);Assert.NotNull(preferences.Snapshot());
    }
    [Fact] public async Task SynchronizeOnlyExplicitTargetsAndLeaveOtherCategoriesUntouched()
    {
        Settings();
        foreach(var region in LinuxSettings.Regions){var dir=Path.Combine(root,region.Id,"dalamud/pluginConfigs");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"sample.json"),region.Id);}
        Plugin("ffxiv_tc");var service=new FrontendPlugins(root,ensureIdle:()=>{});await using var operations=new FrontendOperations(root);
        Assert.Throws<ArgumentException>(()=>service.Start(new(Region,"sync",Content:"plugin-settings",Source:Region,Targets:[Region]),operations));
        var outcome=await Wait(operations,service.Start(new(Region,"sync",Content:"plugin-settings",Source:Region,Targets:["ffxiv_tc"]),operations));
        Assert.Equal("completed",outcome.State);Assert.Equal(Region,File.ReadAllText(Path.Combine(root,"ffxiv_tc/dalamud/pluginConfigs/sample.json")));
        Assert.Equal("ffxiv",File.ReadAllText(Path.Combine(root,"ffxiv/dalamud/pluginConfigs/sample.json")));Assert.Single(OfflinePluginManager.Read(root,"ffxiv_tc").Plugins);
        Assert.True(Directory.Exists(Json(outcome.Result!).GetProperty("backupDirectory").GetString()));
    }
    [Fact] public async Task BackgroundCancellationReleasesBusyStateAndDoesNotChangeLaunchPreference()
    {
        Settings();var entered=new TaskCompletionSource();
        var service=new FrontendPlugins(root,ensureIdle:()=>{},updateDalamud:async(settings,status,token)=>{Assert.False(settings.EnableDalamud);entered.SetResult();await Task.Delay(Timeout.Infinite,token);});
        await using var operations=new FrontendOperations(root);
        var started=service.Start(new(Region,"dalamud-update"),operations);var id=Json(started).GetProperty("operationId").GetString()!;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.True(operations.Busy);
        Assert.Throws<InvalidOperationException>(()=>operations.Start(new(Region,"launch")));
        operations.Cancel(new(id));Assert.Equal("cancelled",(await Wait(operations,started)).State);Assert.False(operations.Busy);Assert.False(LinuxSettings.Load(root).EnableDalamud);
    }
    [Fact] public async Task GameGuardFailureDoesNotWriteOrPoisonLaterRead()
    {
        Settings();Plugin();var running=true;var service=new FrontendPlugins(root,ensureIdle:()=>{if(running)throw new IOException("running game");});var list=service.Read(Region);
        await using var operations=new FrontendOperations(root);
        var result=await Wait(operations,service.Start(new(Region,"apply",list.Revision,new(){["Example"]=false}),operations));
        Assert.Equal("failed",result.State);Assert.True(service.Read(Region).Plugins.Single().Enabled);Assert.False(operations.Busy);
        running=false;list=service.Read(Region);
        result=await Wait(operations,service.Start(new(Region,"apply",list.Revision,new(){["Example"]=false}),operations));
        Assert.Equal("completed",result.State);Assert.False(service.Read(Region).Plugins.Single().Enabled);
        var repos=service.Repositories(Region);
        service.SaveRepositories(new(Region,repos.Revision,[new("https://repo.test/list",true)],true));
        Assert.Single(service.Repositories(Region).Entries);

    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(send(request));}}
    [Fact] public async Task UpdatePlanUsesOriginalRepositoryAndCannotBeForgedOrReplayed()
    {
        Settings();Plugin();File.WriteAllText(Config,new JObject{["MainRepoUrl"]=Repo}.ToString());
        // Read assembly metadata only; this fixture assembly is never executed as a plugin.
        var dir=Path.Combine(root,Region,"addon/Hooks/1.0.0.0");Directory.CreateDirectory(dir);File.Copy(typeof(LinuxSettings).Assembly.Location,Path.Combine(dir,"Dalamud.dll"));
        var settings=LinuxSettings.Load(root);settings.Current.DalamudVersion="1.0.0.0";settings.Save();
        var manifest=new JObject{["InternalName"]="Example",["AssemblyVersion"]="2.0.0.0",["DalamudApiLevel"]=typeof(LinuxSettings).Assembly.GetName().Version!.Major,["DownloadLinkInstall"]="https://plugins.invalid/plugin.zip"};
        using var memory=new MemoryStream();using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true)){
            using(var writer=new StreamWriter(archive.CreateEntry("Example.json").Open()))writer.Write(manifest);
            using(var writer=new StreamWriter(archive.CreateEntry("Example.dll").Open()))writer.Write("synthetic DLL");}
        var downloads=0;using var http=new HttpClient(new Handler(request=>{
            Assert.Equal("plugins.invalid",request.RequestUri!.Host);
            if(request.RequestUri.AbsolutePath.EndsWith(".zip")){downloads++;return new(HttpStatusCode.OK){Content=new ByteArrayContent(memory.ToArray())};}
            return new(HttpStatusCode.OK){Content=new StringContent(new JArray(manifest).ToString())};
        }));
        var service=new FrontendPlugins(root,new PluginUpdates(http),()=>{});await using var operations=new FrontendOperations(root);
        var check=await Wait(operations,service.Start(new(Region,"check-updates"),operations));Assert.Equal("completed",check.State);
        var plan=Json(check.Result!).GetProperty("plan");Assert.Single(plan.GetProperty("updates").EnumerateArray());var id=plan.GetProperty("planId").GetString();
        Assert.Throws<CredentialValidationException>(()=>service.Start(new(Region,"install-updates",PlanId:"forged",Plugins:["Example"]),operations));
        Assert.Throws<ArgumentException>(()=>service.Start(new(Region,"install-updates",PlanId:id,Plugins:["wrong"]),operations));Assert.Equal(0,downloads);
        var install=await Wait(operations,service.Start(new(Region,"install-updates",PlanId:id,Plugins:["Example"]),operations));Assert.Equal("completed",install.State);Assert.Equal(1,downloads);
        Assert.Equal("2.0.0.0",service.Read(Region).Plugins.Single().Version);
        Assert.Throws<CredentialValidationException>(()=>service.Start(new(Region,"install-updates",PlanId:id,Plugins:["Example"]),operations));
    }
    [Fact] public async Task BrokenPluginConfigDoesNotBreakHostPreferencesOrOtherRegionLists()
    {
        Settings();Directory.CreateDirectory(Path.GetDirectoryName(Config)!);File.WriteAllText(Config,"invalid");
        var input=new StringReader("""
            {"id":"bad","method":"plugins.read","params":{"region":"ffxiv_cn"}}
            {"id":"good","method":"plugins.read","params":{"region":"ffxiv_tc"}}
            {"id":"settings","method":"snapshot","params":{}}
            """);
        var output=new StringWriter();await FrontendHost.RunAsync(root,input,output);
        var responses=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToArray();
        Assert.True(responses[0].TryGetProperty("error",out _));Assert.True(responses[1].TryGetProperty("result",out _));Assert.True(responses[2].TryGetProperty("result",out _));Assert.Equal("invalid",File.ReadAllText(Config));
    }

    [Fact] public async Task RepositoryWritesAndSyncAreBlockedWhileGameRuns()
    {
        Settings();Plugin();File.WriteAllText(Config,"{}");Plugin("ffxiv_tc");
        var target=Path.Combine(root,"ffxiv_tc/dalamud/dalamudConfig.json");File.WriteAllText(target,"{\"Keep\":true}");
        var service=new FrontendPlugins(root,ensureIdle:()=>throw new IOException("Game running"));
        var current=service.Repositories(Region);
        Assert.Throws<IOException>(()=>service.SaveRepositories(new(Region,current.Revision,[new("https://repo.test/list",true)],true)));
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var operations=new FrontendOperations(root,(_,_)=>Task.CompletedTask);
        var result=await Wait(operations,service.Start(new(Region,"sync",Content:"third-party-repositories",Source:Region,Targets:["ffxiv_tc"]),operations));
        Assert.Equal("failed",result.State);Assert.Equal("{}",File.ReadAllText(Config));Assert.Equal("{\"Keep\":true}",File.ReadAllText(target));
        Assert.Equal(current.Revision,service.Repositories(Region).Revision);
        Assert.False(operations.Busy);
    }
    [Fact] public async Task RepositorySyncRechecksGameBeforeCommit()
    {
        Settings();Plugin();File.WriteAllText(Config,"{}");Plugin("ffxiv_tc");
        var target=Path.Combine(root,"ffxiv_tc/dalamud/dalamudConfig.json");File.WriteAllText(target,"{\"Keep\":true}");
        var calls=0;var service=new FrontendPlugins(root,ensureIdle:()=>{if(++calls>1)throw new IOException("Game started during preparation");});
        await using var operations=new FrontendOperations(root,(_,_)=>Task.CompletedTask);
        var result=await Wait(operations,service.Start(new(Region,"sync",Content:"third-party-repositories",Source:Region,Targets:["ffxiv_tc"]),operations));
        Assert.Equal("failed",result.State);Assert.Equal("{\"Keep\":true}",File.ReadAllText(target));Assert.Equal(2,calls);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
