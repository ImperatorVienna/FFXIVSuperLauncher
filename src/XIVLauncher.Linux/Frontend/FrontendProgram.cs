namespace XIVLauncher.Linux.Frontend;

// The single .NET backend entry; all launcher windows are owned by Electron.
internal static class FrontendProgram
{
    public static int Main(string[] args)
    {
        if (args is ["--launcher-version"]) { Console.WriteLine(typeof(FrontendProgram).Assembly.GetName().Version?.ToString(3)); return 0; }
        if (!FrontendStartup.TryParse(args, out var startup)) return 2;
        XIVLauncher.Common.Constant.Paths.OverrideRoamingPath(startup!.Root);
        if (startup.SteamEntry)
            foreach (var key in new[] { "WINEPREFIX", "STEAM_COMPAT_DATA_PATH", "STEAM_COMPAT_TOOL_PATHS", "STEAM_COMPAT_MOUNTS", "STEAM_COMPAT_INSTALL_PATH" })
                Environment.SetEnvironmentVariable(key, null);
        // IPC is UTF-8 on every platform, independent of the Windows console code page.
        using var input = new StreamReader(Console.OpenStandardInput(), new System.Text.UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
        using var output = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        return FrontendHost.RunAsync(startup.Root, input, output,
            launcherEntry: startup.LauncherEntry, steamEntry: startup.SteamEntry).GetAwaiter().GetResult();
    }
}
