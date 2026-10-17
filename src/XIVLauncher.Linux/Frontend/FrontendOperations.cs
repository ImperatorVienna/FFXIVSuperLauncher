namespace XIVLauncher.Linux.Frontend;

internal sealed record GameOperationRequest(string Region, string Kind, string Account = "", string AreaName = "", bool AutoOtp = false,
    bool SteamAccount = false, bool FreeTrial = false, bool EnableDalamud = false, bool NoPlugins = false);
internal sealed record OperationIdentity(string OperationId);
internal sealed record ChallengeReply(string OperationId, string ChallengeId, string Value);
internal sealed record FrontendChallenge(string Id, string Kind, string Message, string? Image = null, bool Optional = false, bool AllowLocal = false, string? Code = null);
internal sealed record OperationView(string Id, string Region, string State, FrontendChallenge? Challenge, string Log, string? Error, bool Launched, object? Result = null, string? WebVerificationId = null);

// Long operations are independent of the request reader: polling, replies and cancellation never wait behind login.
internal sealed class FrontendOperations(string root, Func<GameOperationRequest, FrontendOperation, Task>? execute = null) : IAsyncDisposable
{
    private FrontendOperation? current;
    internal bool Busy => current?.Busy == true;
    internal void EnsureIdle() { if (Busy) throw new InvalidOperationException("operation-busy"); }
    internal object Start(GameOperationRequest request)
    {
        EnsureIdle(); FrontendAccounts.Validate(request.Region);
        if (request.Kind is not ("launch" or "game-update") || request.Account == null || request.AreaName == null || request.Account.Length > 256 || request.AreaName.Length > 256)
            throw new ArgumentException("Invalid game operation.");
        return Start(request.Region, async operation =>
        {
            if (execute != null) await execute(request,operation);
            else await FrontendGame.RunAsync(root,request,operation);
        });
    }
    internal object Start(string region, Func<FrontendOperation, Task> action)
    {
        EnsureIdle(); FrontendAccounts.Validate(region);
        current?.Dispose();
        var operation = new FrontendOperation(root,region);
        current = operation;
        operation.Begin(() => action(operation));
        return new { operationId=operation.Id };
    }
    private FrontendOperation Find(string id) => current?.Id == id ? current : throw new ArgumentException("Unknown operation.");
    internal OperationView Poll(OperationIdentity request) => Find(request.OperationId).View();
    internal object WebPage(ChallengeReply request) => Find(request.OperationId).WebPage(request.ChallengeId);
    internal object WebReply(ChallengeReply request) { Find(request.OperationId).WebReply(request.ChallengeId,request.Value);return new {accepted=true}; }
    internal object WebFail(ChallengeReply request) { Find(request.OperationId).WebFail(request.ChallengeId,request.Value);return new {accepted=true}; }
    internal object ClearLog(OperationIdentity request) { Find(request.OperationId).ClearLog(); return new { accepted=true }; }
    internal object Cancel(OperationIdentity request) { Find(request.OperationId).Cancel(); return new { accepted=true }; }
    internal object Reply(ChallengeReply reply) { Find(reply.OperationId).Reply(reply); return new { accepted=true }; }
    public async ValueTask DisposeAsync()
    {
        if (current == null) return;
        current.Cancel();
        try { await current.Completion.WaitAsync(TimeSpan.FromSeconds(6)); } catch (Exception) { }
        current.Dispose(); current = null;
    }
}

internal sealed class FrontendOperation(string root, string region) : IDisposable
{
    private readonly object gate=new();
    private readonly CancellationTokenSource cancellation=new();
    private readonly LauncherLog log=new(500);
    private readonly SessionDiagnostics diagnostics=new(root);
    private string state="running";
    private string? error;
    private bool launched;
    private bool committed;
    private string? webVerificationId;
    private object? result;
    private FrontendChallenge? challenge;
    private TaskCompletionSource<string>? reply;
    internal string Id { get; } = Guid.NewGuid().ToString("N");
    internal CancellationToken Token => cancellation.Token;
    internal CancellationTokenSource Cancellation => cancellation;
    internal Task Completion { get; private set; } = Task.CompletedTask;
    internal bool Busy { get { lock(gate) return state is "running" or "cancelling"; } }
    internal void Begin(Func<Task> action) => Completion=Task.Run(async()=>
    {
        try { Token.ThrowIfCancellationRequested(); await action(); lock(gate) { if(!committed)Token.ThrowIfCancellationRequested(); state="completed"; } }
        catch(OperationCanceledException) { lock(gate) state="cancelled"; Status("Operation cancelled."); }
        catch(Exception ex)
        {
            // Fixed diagnostic summaries and stacks only; never raw response bodies or submitted values.
            var message=FrontendErrors.Message(ex);
            diagnostics.Write(FrontendErrors.Diagnostic(ex),region,"ERROR");
            log.Append(FrontendErrors.LogMessage(ex),region,"ERROR");
            lock(gate) { state="failed";error=message; }
        }
        finally { lock(gate) { challenge=null;webVerificationId=null;reply?.TrySetCanceled();reply=null; } }
    });
    internal void Status(string message)
    {
        lock(gate) { if(state=="cancelling")return; }
        var safe=UserLogMessage.Compact(message);log.Append(safe,region);
        diagnostics.Write(safe,region,"INFO");
    }
    internal void Download(XIVLauncher.Common.Http.DownloadProgress progress) => log.Download(Id,"Downloading",region,progress,Token.IsCancellationRequested);
    internal void ClearLog() => log.Clear();
    internal void SetResult(object value) { lock(gate) result=value; }
    internal void CommitResult(object value) { lock(gate) { result=value;committed=true; } }
    internal void MarkLaunched() { lock(gate) launched=true; }
    internal void Notify(string kind,string message,string? image=null,string? code=null)
    {
        Token.ThrowIfCancellationRequested();
        lock(gate) { if(state!="running"||reply!=null)return;challenge=new(Guid.NewGuid().ToString("N"),kind,message,image,Code:code); }
    }
    internal void NotifyMobile(string code)
    {
        if(string.IsNullOrEmpty(code)||code.Length>32||code.Any(c=>c<'0'||c>'9'))throw new ArgumentException("Invalid mobile verification code.");
        Notify("mobile-confirmation","请在手机上打开叨鱼 App，完成滑动验证。",code:code);
    }
    internal void ClearNotification() { lock(gate) { if(reply==null)challenge=null; } }
    internal async Task<string> AskAsync(string kind,string message,bool optional=false,bool allowLocal=false, TimeSpan? timeout=null, CancellationToken token=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(Token,token);
        var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(gate)
        {
            linked.Token.ThrowIfCancellationRequested();
            if(state!="running")throw new InvalidOperationException("Verification operation is no longer running.");
            if(reply!=null)throw new InvalidOperationException("Another verification is pending.");
            reply=completion;challenge=new(Guid.NewGuid().ToString("N"),kind,message,Optional:optional,AllowLocal:allowLocal);
            if(kind=="web-captcha")webVerificationId=challenge.Id;
        }
        try { return await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(90),linked.Token); }
        finally { lock(gate) { if(reply==completion) {reply=null;challenge=null;} } }
    }
    internal async Task<string> AuthenticateWithWebAsync(Func<string, CancellationToken, Task<string>> authenticate, CancellationToken token)
    {
        try
        {
            var value = await AskAsync("web-captcha", "正在执行官方网页验证…", timeout: TimeSpan.FromMinutes(5), token: token);
            return await authenticate(value, token);
        }
        finally { lock(gate) webVerificationId=null; }
    }
    internal object WebPage(string id)
    {
        lock(gate)
        {
            if(region!="ffxiv_tc" || state!="running" || webVerificationId!=id)throw new ArgumentException("Stale web verification.");
            var binding="superCaptcha"+id;
            return new { url=Taiwan.TaiwanCaptcha.PageUrl,binding,html=Taiwan.TaiwanCaptcha.CreatePage(binding) };
        }
    }
    internal void WebFail(string id, string reason)
    {
        // Never accept arbitrary page errors, response bodies, or token values as diagnostics.
        if (reason is not ("renderer-exited" or "page-load" or "verification-timeout" or "invalid-token"
            or "token-delivery" or "page-command" or "page-initialization"))
            throw new ArgumentException("Invalid verification failure stage.");
        lock(gate)
        {
            ValidateWebReply(id);
            Status("Web verification failed: " + reason + ".");
            reply!.TrySetException(new IOException("Verification page failed."));
        }
    }
    private void ValidateWebReply(string id)
    {
        if(region!="ffxiv_tc" || state!="running" || challenge?.Kind!="web-captcha" || challenge.Id!=id || reply==null || reply.Task.IsCompleted)
            throw new ArgumentException("Stale web verification.");
    }
    internal void WebReply(string id, string value)
    {
        lock(gate)
        {
            ValidateWebReply(id);
            if(value==null || value.Length is < 1 or > 16384 || value.Any(char.IsControl))throw new ArgumentException("Invalid web verification token.");
            Status("Web verification completed.");
            reply!.TrySetResult(value);
        }
    }
    internal void Reply(ChallengeReply response)
    {
        lock(gate)
        {
            if(state!="running" || challenge?.Id!=response.ChallengeId || reply==null || reply.Task.IsCompleted)throw new ArgumentException("Stale verification reply.");
            if(challenge.Kind=="web-captcha")throw new ArgumentException("Use the isolated verification view.");
            var value=response.Value;
            if(value==null || value.Length>32)throw new ArgumentException("Invalid verification reply.");
            if(challenge.Kind=="otp" && !(OneTimePassword.IsManualCode(value) || challenge.Optional && value.Length==0))throw new ArgumentException("Invalid OTP format.");
            if(challenge.Kind=="dalamud-timeout" && !(value=="retry" || value=="disable" || value=="local" && challenge.AllowLocal))throw new ArgumentException("Invalid update decision.");
            reply.TrySetResult(value);
        }
    }
    internal void Cancel() { lock(gate) { if(state!="running"||committed)return;state="cancelling";challenge=null;webVerificationId=null; } cancellation.Cancel(); }
    internal OperationView View() { lock(gate) return new(Id,region,state,challenge,log.Text,error,launched,state=="completed"?result:null,webVerificationId); }
    public void Dispose() { if(Completion.IsCompleted)cancellation.Dispose(); }
}

internal static class FrontendErrors
{
    internal static string Message(Exception ex) => ex switch
    {
        CredentialValidationException validation => Localization.Display(validation.UserMessage),
        Global.GlobalAuthenticationException authentication => authentication.UserMessage,
        Taiwan.TaiwanAuthenticationException authentication => authentication.Diagnostic,
        XIVLauncher.Login.Exceptions.LoginException authentication => UserLogMessage.Failure(authentication),
        IOException { Message: "ffxiv Steam initialization failed. Start Steam, sign in online, then retry." }
            or IOException { Message: "ffxiv Steam must be running and signed in online." }
            or IOException { Message: "ffxiv Steam did not return an authentication ticket." } => ex.Message,
        TimeoutException => "Operation timed out. Please retry.",
        OperationCanceledException => "Operation cancelled.",
        ArgumentException or System.Text.Json.JsonException => "Invalid request.",
        InvalidOperationException { Message: "operation-busy" } => "Another operation is running. Cancel it or wait for it to finish.",
        _ => "The operation failed. See the diagnostic log for details."
    };
    internal static string LogMessage(Exception ex) => ex is CredentialValidationException validation
        ? Localization.Catalog("en").GetValueOrDefault(UiVocabulary.Normalize(validation.UserMessage), "Validation failed.")
        : Message(ex);
    internal static string Diagnostic(Exception ex)
    {
        var text = new System.Text.StringBuilder();
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            text.AppendLine($"Exception: {current.GetType().FullName}; HRESULT: 0x{current.HResult:X8}");
            if (current is System.ComponentModel.Win32Exception native) text.AppendLine($"Windows error code: {native.NativeErrorCode}");
            text.AppendLine(LogMessage(current));
            text.AppendLine(current.StackTrace);
        }
        return text.ToString();
    }
}
