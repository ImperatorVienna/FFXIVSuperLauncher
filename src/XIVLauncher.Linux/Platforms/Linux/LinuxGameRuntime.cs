using System.Diagnostics;
using XIVLauncher.Common.Unix;
using XIVLauncher.Dalamud;
namespace XIVLauncher.Linux.Platforms.Linux;

internal sealed class LinuxGameRuntime : IGameRuntime
{
    private readonly CompatibilityRunner runner;
    private readonly UnixDalamudRunner injector;
    private readonly Processes.GameSessionTracker tracker = new();
    internal LinuxGameRuntime(LinuxSettings settings) : this(CreateRunner(settings)) { }
    internal LinuxGameRuntime(CompatibilityRunner runner) { this.runner=runner; injector=new(runner); }
    internal static void ValidateSetup(LinuxSettings settings) => CreateRunner(settings, false).Validate();
    private static CompatibilityRunner CreateRunner(LinuxSettings settings, bool requireIdle = true)
    {
        if (string.IsNullOrWhiteSpace(settings.ProtonScript))
            throw new CredentialValidationException("No Proton selected.","请先选择 Proton。");
        var proton=ProtonDiscovery.FromScript(settings.ProtonScript,settings.SteamRoot);
        if(requireIdle && settings.SelectedRegion=="ffxiv" && settings.Current.UseSteamPrefix)SteamPrefixDiscovery.EnsureIdle(settings.Current.SteamCompatibilityData);
        return new(settings.GetCompatibility(proton));
    }
    public void Validate() => runner.Validate();
    public async Task DiagnoseAsync(Action<string> status, CancellationToken token)
    {
        status(Localization.T("正在检查 Proton 环境…"));
        await runner.DiagnoseAsync(token);
    }
    public RegionDalamudFiles? WithoutDalamud(string region) => region=="ffxiv_cn"
        ? RegionLaunchPreparation.FixedChinaHelper(AppContext.BaseDirectory) : null;
    public async Task<ProcessStartInfo> PrepareAsync(LinuxSettings settings, RegionDalamudFiles? files, FileInfo game,
        DalamudStartInfo? info, bool noPlugins, CancellationToken token)
    {
        var prepared = files != null
            ? await RegionInjection.For(settings.SelectedRegion).PrepareAsync(injector,files,game,info!,settings.EnableDalamud,noPlugins,settings.RegionRoot,AppContext.BaseDirectory,token)
            : runner.BuildStartInfo([game.FullName],game.DirectoryName,mainSession:true,protonVerb:"runinprefix");
        if(settings.SelectedRegion=="ffxiv" && settings.Current.SteamAccount)
        {
            prepared.Environment["IS_FFXIV_LAUNCH_FROM_STEAM"]="1";
            prepared.Environment["SteamAppId"]=settings.Current.FreeTrial?"312060":"39210";
            prepared.Environment["SteamGameId"]=prepared.Environment["SteamAppId"];
            prepared.Environment["STEAM_COMPAT_APP_ID"]=prepared.Environment["SteamAppId"];
        }
        return prepared;
    }
    public async Task LaunchAsync(LinuxSettings settings, ProcessStartInfo prepared, string arguments, bool injected,
        Action<string> status, CancellationToken token)
    {
        if(injected)
        {
            var winePid=await injector.LaunchAsync(prepared,arguments,token,
                confirmationTimeout:settings.SelectedRegion=="ffxiv_tc"?TimeSpan.FromMinutes(4):null);
            status(settings.EnableDalamud ? Localization.F($"游戏已由注入器启动（Wine PID {winePid}）；Dalamud 加载状态请在游戏内确认。") : Localization.F($"中国区游戏已启动（Wine PID {winePid}）；未启用或更新 Dalamud。"));
        }
        else
        {
            await RegionLaunchPreparation.LaunchPlainAsync(prepared,arguments,token);
            status(Localization.T("已通过 Proton 提交游戏启动命令；本次未启用或更新 Dalamud。"));
        }
    }
    private string Prefix => Path.Combine(runner.Settings.DataDirectory,"pfx");
    public bool IsGameRunning => Processes.ProcessSessions.Find(Prefix,false)==Processes.ProcessPresence.Alive;
    public bool ObserveGame() => tracker.Observe(Prefix);
    public Task WaitForExitAsync() => runner.WaitForPrefixExitAsync();
}
