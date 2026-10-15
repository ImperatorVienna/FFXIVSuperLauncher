using XIVLauncher.Common.Util;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Common.Http;
using XIVLauncher.Linux.Platforms;
using XIVLauncher.Login.Client;
using XIVLauncher.Login.Models;

namespace XIVLauncher.Linux.Frontend;

internal static class FrontendGame
{
    internal static async Task RunAsync(string root, GameOperationRequest request, FrontendOperation operation, Action<IGameRuntime>? onStarting = null, Action<GameTravelSession>? registerTravelSession = null)
    {
        var token=operation.Token;token.ThrowIfCancellationRequested();
        var settings=LinuxSettings.Load(root);
        if(settings.SelectedRegion!=request.Region)throw new ArgumentException("The selected region has changed.");
        settings.EnsureRegionAvailable();
        var needsLogin=request.Kind=="launch" || request.Region=="ffxiv";
        if(needsLogin)
        {
            FrontendAccounts.Validate(request.Region,request.Account);
            if(request.Region!="ffxiv_cn" && !settings.Current.Accounts.ContainsKey(request.Account))
                throw new CredentialValidationException("Unknown account.","这是首次登录此账户，请填写并保存凭据，然后手动回到游戏页选择自己的登录名并登录。");
            AccountProfiles.Select(settings.Current,request.Account);
            settings.Current.RememberPassword=true;
            settings.Current.AutoOtp=request.AutoOtp;
            if(request.Region=="ffxiv") {settings.Current.SteamAccount=request.SteamAccount;settings.Current.FreeTrial=request.FreeTrial;}
            settings.AreaName=request.AreaName;
        }
        settings.EnableDalamud=request.EnableDalamud;
        if(!GameHelpers.IsValidGamePath(settings.GamePath))
            throw new CredentialValidationException("Invalid client path.","请选择当前游戏区服的完整客户端目录，需包含 game/ffxiv_dx11.exe 和有效版本文件。");
        settings.Save();
        GameTravelSession? travelSession = null;
        var provider=RegionLogin.For(request.Region);
        var areas=request.Region=="ffxiv_cn" && needsLogin
            ? await provider.AreasAsync(token).WaitAsync(TimeSpan.FromSeconds(15),token) : [];
        var area=areas.FirstOrDefault(x=>x.AreaName==request.AreaName);
        if(request.Region=="ffxiv_cn" && needsLogin && area==null)
            throw new CredentialValidationException("Select a data center.","请选择游戏大区，然后再次点击“登录并启动”。");
        using var downloads=new DownloadScope(operation.Download,token);
        async Task Update(CancellationToken ct)
        {
            GameProcessGuard.EnsureClientFilesIdle();
            await new RegionUpdateService().UpdateGameAsync(settings,operation.Status,ct);
        }
        async Task<string> Authenticate(CancellationToken ct)
        {
            var password=request.Region=="ffxiv_cn" ? "" : await RegionCredentials.PasswordForLoginAsync(settings,"",ct);
            if(request.Region!="ffxiv_cn" && password.Length==0)
                throw new CredentialValidationException("Missing password.","当前账号没有可用密码，请到“登录凭据管理”选项卡保存账号密码。");
            operation.Status("Signing in.");
            var loginRequest=FrontendLoginRequest.Create(settings, operation, password);
            var arguments=await provider.AuthenticateAsync(new(settings,area,areas,loginRequest,password,operation.Status,
                operation.AuthenticateWithWebAsync,
                ct=>operation.AskAsync("otp",settings.SelectedRegion=="ffxiv"?"2FA 验证码（可选）":"请输入当前六位 2FA 验证码",optional:true,token:ct),
                async ct=>{operation.ClearNotification();await Update(ct);},
                RegisterTravelSession: request.Region == "ffxiv_cn" && request.EnableDalamud && !request.NoPlugins && registerTravelSession != null
                    ? session => travelSession = session : null),ct);
            operation.ClearNotification();return arguments;
        }
        if(request.Kind=="game-update")
        {
            using var clientLock=GameLaunchCoordinator.AcquireClientLock(settings.GamePath);
            GameProcessGuard.EnsureClientFilesIdle();
            if(request.Region=="ffxiv") _=await Authenticate(token);else await Update(token);
            operation.Status("Game update completed.");return;
        }
        var runner=PlatformServices.CreateGameRuntime(settings);runner.Validate();
        try
        {
        await GameLaunchCoordinator.LaunchAsync(settings,runner,Authenticate,
            ct=>PrepareDalamudAsync(settings,operation,ct),request.NoPlugins,operation.Status,onStarting,token);
        if (travelSession != null) { registerTravelSession!(travelSession); travelSession = null; }
        operation.MarkLaunched();
        }
        finally { if (travelSession != null) await travelSession.DisposeAsync(); }
    }
    private static async Task<RegionDalamudFiles?> PrepareDalamudAsync(LinuxSettings settings, FrontendOperation operation, CancellationToken token)
    {
        try {return await new RegionDalamudUpdates().UpdateAsync(settings,operation.Status,token);}
        catch(Exception ex) when(LocalDalamud.IsTimeout(ex,token))
        {
            RegionDalamudFiles? local=null;
            try { local=await Task.Run(()=>LocalDalamud.Find(settings,token),token); }
            catch(Exception check) when(check is IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException) { }
            var choice=await operation.AskAsync("dalamud-timeout","Dalamud 更新连接超时。是否跳过本次更新？使用本地版本不能保证与当前游戏兼容。",allowLocal:local!=null,token:token);
            if(choice=="local")return local;
            LocalDalamud.Disable(settings);return null;
        }
    }
}
