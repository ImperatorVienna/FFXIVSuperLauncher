namespace XIVLauncher.Linux;
public static class GameProcessGuard
{
    public static void EnsureClientFilesIdle() => EnsureIdle(name => IsGameProcessName(name) || IsOfficialLauncher(name), "Close games and official launchers before updating client files.");
    public static void EnsureGameSettingsIdle() => EnsureIdle(IsGameProcessName, "请退出游戏后再修改客户端语言、路径或兼容环境设置。");
    public static void EnsureGamesStopped() => EnsureIdle(IsGameProcessName, Localization.T("请退出各游戏区服游戏后再修改插件数据，避免运行中的插件覆盖设置。"));
    private static void EnsureIdle(Func<string, bool> blocked, string message)
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        using (process)
        {
            try
            {
                var name = process.ProcessName;
                if (blocked(name) && IsLive(process)) throw new CredentialValidationException("Game process is running.", message);
            }
            catch (InvalidOperationException) { }
        }
    }
    private static bool IsLive(System.Diagnostics.Process process)
    {
        if (process.HasExited) return false;
        if (!OperatingSystem.IsLinux()) return true;
        try { return IsLiveLinuxStat(File.ReadAllText($"/proc/{process.Id}/stat")); }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    internal static bool IsLiveLinuxStat(string stat)
    {
        // comm may contain spaces or parentheses; state follows its final closing parenthesis.
        var end = stat.LastIndexOf(')');
        if (end < 0 || end + 2 >= stat.Length) return true;
        return stat[end + 2] is not ('Z' or 'X' or 'x');
    }
    public static bool IsOfficialLauncher(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return new[] { "ffxivboot", "ffxivboot64", "ffxivlauncher", "ffxivlauncher64", "ffxivupdater", "ffxivupdater64", "FfxivLauncherTC", "FfxivUpdaterTC" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
    public static bool IsGameProcessName(string name) => name.Equals("ffxiv", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv.exe", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv_dx11", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase);
}
