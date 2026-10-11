using System.Text.Json;
using System.Text.Json.Serialization;

namespace XIVLauncher.Linux.Frontend;

internal static class FrontendHost
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record Request(string Id, string Method, JsonElement Params);
    private sealed record RegionRequest(string Region);

    internal static async Task<int> RunAsync(string root, TextReader input, TextWriter output, CancellationToken token = default,
        Func<string, CancellationToken, Task<XIVLauncher.Login.Models.LoginArea[]>>? fetchAreas = null,
        Func<string, string, string, CancellationToken, Task<OfficialNews>>? fetchOfficial = null,
        string? launcherEntry = null, bool steamEntry = false,
        Func<CompatibilityView>? scanCompatibility = null, TimeSpan? scanDeadline = null)
    {
        if (launcherEntry != null) LinuxSettings.EnsureCreated(root);
        XIVLauncher.Common.Unix.CompatibilityRunner? gameSession = null;
        var compatibility = new FrontendCompatibility(root, launcherEntry);
        var application = new FrontendApplication(root);
        await using var launcherUpdates = new FrontendLauncherUpdates(root, application);
        var preferences=new FrontendPreferences(root);
        var accounts=new FrontendAccounts(root);
        var plugins=new FrontendPlugins(root);
        using var travel=new FrontendTravel(root);
        await using var operations=new FrontendOperations(root, (request, operation) =>
            FrontendGame.RunAsync(root, request, operation, runner => gameSession = runner));
        await using var official=new FrontendOfficial(root,fetchOfficial);
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);
        using var writerGate=new SemaphoreSlim(1,1);
        Task? areaTask=null, scanReply=null;
        Task<CompatibilityView>? scanTask=null;
        async Task ScanCompatibilityAsync(string requestId)
        {
            object response;
            try
            {
                // At most one actual scan, even if a filesystem call outlives the response deadline.
                if(scanTask is {IsCompleted:false})throw new InvalidOperationException("operation-busy");
                scanTask=Task.Run(scanCompatibility ?? (()=>compatibility.Read()));
                _=scanTask.ContinueWith(task=>{_ = task.Exception;}, TaskContinuationOptions.OnlyOnFaulted);
                var result=await scanTask.WaitAsync(scanDeadline ?? TimeSpan.FromSeconds(7),lifetime.Token);
                response=new {id=requestId,result};
            }
            catch(Exception ex)
            {
                response=new {id=requestId,error=new {code=ex is TimeoutException?"read-timeout":ex is InvalidOperationException {Message:"operation-busy"}?"operation-busy":"operation-failed",message=FrontendErrors.Message(ex),logMessage=FrontendErrors.LogMessage(ex)}};
            }
            await WriteAsync(response);
        }
        async Task WriteAsync(object value)
        {
            await writerGate.WaitAsync(token);
            try {await output.WriteLineAsync(JsonSerializer.Serialize(value,JsonOptions));await output.FlushAsync(token);}
            finally {writerGate.Release();}
        }
        async Task LoadAreasAsync(string requestId,string region)
        {
            object response;
            try
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(7));
                var areas=await (fetchAreas?.Invoke(region,timeout.Token) ?? RegionLogin.For(region).AreasAsync(timeout.Token)).WaitAsync(timeout.Token);
                response=new {id=requestId,result=areas.Select(x=>new {name=x.AreaName}).ToArray()};
            }
            catch(Exception ex) {response=new {id=requestId,error=new {code="operation-failed",message=FrontendErrors.Message(ex),logMessage=FrontendErrors.LogMessage(ex)}};}
            await WriteAsync(response);
        }
        try
        {
        while (await input.ReadLineAsync(token) is { } line)
        {
            string? id=null;object response;
            try
            {
                if(line.Length>65536)throw new ArgumentException("Request too large.");
                var request=JsonSerializer.Deserialize<Request>(line,JsonOptions) ?? throw new JsonException();
                if(string.IsNullOrWhiteSpace(request.Id)||request.Id.Length>64)throw new ArgumentException("Invalid request ID.");
                id=request.Id;
                T Parse<T>() => request.Params.Deserialize<T>(JsonOptions) ?? throw new JsonException();
                if(request.Method is "preferences.update" or "accounts.save" or "accounts.read" or "operations.start" or "plugins.read" or "plugins.repositories.save" or "plugins.start" or "compatibility.update" or "compatibility.diagnose" or "travel.start" or "travel.close" or "setup.complete" or "updates.install")operations.EnsureIdle();
                object result;
                switch(request.Method)
                {
                    case "snapshot":result=preferences.Snapshot();break;
                    case "steam.attach":steamEntry=true;result=new {attached=true};break;
                    case "system.initialize":result=application.Initialize();break;
                    case "window.save":result=application.SaveWindow(Parse<WindowPlacement>());break;
                    case "setup.complete":result=application.CompleteSetup(Parse<SetupRequest>());break;
                    case "about.read":result=application.About();break;
                    case "about.open":result=application.Open(Parse<AboutRequest>());break;
                    case "about.notice":result=application.Notice(Parse<AboutRequest>());break;
                    case "desktop.install":result=application.InstallMenu();break;
                    case "updates.read":result=launcherUpdates.Read(Parse<UpdateCheckRequest>());break;
                    case "updates.open":result=launcherUpdates.Open(Parse<UpdateInstallRequest>());break;
                    case "updates.install":result=launcherUpdates.Install(Parse<UpdateInstallRequest>(),operations);break;
                    case "official.read":result=official.Read(Parse<OfficialRead>());break;
                    case "official.open":result=official.Open(Parse<OfficialOpen>());break;
                    case "preferences.update":result=preferences.Update(Parse<PreferenceChange>());break;
                    case "accounts.list":result=await accounts.ListAsync(Parse<RegionRequest>().Region,token);break;
                    case "accounts.read":result=await accounts.ReadAsync(Parse<AccountIdentity>(),token);break;
                    case "accounts.save":result=await accounts.SaveAsync(Parse<AccountChange>(),token);break;
                    case "operations.start":result=operations.Start(Parse<GameOperationRequest>());break;
                    case "operations.poll":result=operations.Poll(Parse<OperationIdentity>());break;
                    case "operations.clear-log":result=operations.ClearLog(Parse<OperationIdentity>());break;
                    case "operations.cancel":result=operations.Cancel(Parse<OperationIdentity>());break;
                    case "captcha.page":result=operations.WebPage(Parse<ChallengeReply>());break;
                    case "captcha.reply":result=operations.WebReply(Parse<ChallengeReply>());break;
                    case "captcha.fail":result=operations.WebFail(Parse<ChallengeReply>());break;
                    case "operations.reply":result=operations.Reply(Parse<ChallengeReply>());break;
                    case "plugins.read":result=plugins.Read(Parse<RegionRequest>().Region);break;
                    case "plugins.repositories.read":result=plugins.Repositories(Parse<RegionRequest>().Region);break;
                    case "plugins.repositories.save":result=plugins.SaveRepositories(Parse<RepositoryChange>());break;
                    case "plugins.sources":result=plugins.Sources(Parse<PluginSourceRequest>());break;
                    case "plugins.start":result=plugins.Start(Parse<PluginActionRequest>(),operations);break;
                    case "travel.start":result=travel.Start(Parse<TravelRequest>(),operations);break;
                    case "travel.close":travel.Dispose();result=new {closed=true};break;
                    case "compatibility.read":
                        if(scanReply is {IsCompleted:false})throw new InvalidOperationException("operation-busy");
                        scanReply=ScanCompatibilityAsync(request.Id);
                        continue;
                    case "compatibility.update":result=compatibility.Update(Parse<CompatibilityChange>());break;
                    case "compatibility.diagnose":result=compatibility.Diagnose(operations);break;
                    case "areas.list":
                        var region=Parse<RegionRequest>().Region;FrontendAccounts.Validate(region);
                        if(areaTask is {IsCompleted:false})throw new InvalidOperationException("operation-busy");
                        areaTask=LoadAreasAsync(request.Id,region);
                        continue; // Keep cancellation and verification replies responsive during network lookup.
                    default:throw new NotSupportedException();
                }
                response=new {id,result};
            }
            catch(Exception ex) when(!token.IsCancellationRequested)
            {
                var code=ex switch {NotSupportedException=>"unknown-method",JsonException or ArgumentException=>"invalid-request",CredentialValidationException=>"validation-failed",InvalidOperationException {Message:"operation-busy"}=>"operation-busy",_=>"operation-failed"};
                if(code=="operation-failed")new SessionDiagnostics(root).Write(FrontendErrors.Diagnostic(ex),"shared","ERROR");
                // Never echo raw request data or raw server exceptions.
                response=new {id,error=new {code,message=FrontendErrors.Message(ex),logMessage=FrontendErrors.LogMessage(ex)}};
            }
            await WriteAsync(response);
        }
        }
        finally
        {
            lifetime.Cancel();
            await operations.DisposeAsync();
            if(areaTask!=null)try{await areaTask;}catch(IOException){ }catch(OperationCanceledException){ }
            if(scanReply!=null)try{await scanReply;}catch(IOException){ }catch(OperationCanceledException){ }
        }
        // UI work is released before waiting for a game launched by this Steam process.
        if (steamEntry && gameSession != null) await gameSession.WaitForPrefixExitAsync();
        return 0;
    }
}
