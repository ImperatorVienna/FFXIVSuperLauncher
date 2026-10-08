namespace XIVLauncher.Linux;
public static class GameProcessGuard
{
    public static void EnsureClientFilesIdle()
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        using (process)
        {
            try
            {
                var name = process.ProcessName;
                if (IsGameProcessName(name) || IsOfficialLauncher(name)) throw new IOException("Close games and official launchers before updating client files.");
            }
            catch (InvalidOperationException) { }
        }
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
