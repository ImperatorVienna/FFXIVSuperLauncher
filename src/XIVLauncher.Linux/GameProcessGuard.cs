namespace XIVLauncher.Linux;
public static class GameProcessGuard
{
    public static void EnsureClientFilesIdle() => EnsureIdle(name => IsGameProcessName(name) || IsOfficialLauncher(name), "Close games and official launchers before updating client files.");
    public static void EnsureGameSettingsIdle() => EnsureIdle(IsGameProcessName, "请退出游戏后再修改客户端语言、路径或兼容环境设置。");
    public static void EnsureGamesStopped() => EnsureIdle(IsGameProcessName, Localization.T("请退出各游戏区服游戏后再修改插件数据，避免运行中的插件覆盖设置。"));
    private static void EnsureIdle(Func<string, bool> blocked, string message)
    {
        foreach (var process in Processes.ProcessFacts.Snapshot())
            if (blocked(process.Name) && process.Presence != Processes.ProcessPresence.Gone)
                throw new CredentialValidationException("Game process is running or cannot be inspected.", message);
    }
    internal static bool IsLiveLinuxStat(string stat) => Processes.ProcessFacts.ParseStat(stat) != Processes.ProcessPresence.Gone;
    public static bool IsOfficialLauncher(string name) => Processes.ProcessIdentity.IsOfficialLauncher(name);
    public static bool IsGameProcessName(string name) => Processes.ProcessIdentity.IsGameProcessName(name);
}
