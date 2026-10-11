namespace XIVLauncher.Linux.Processes;

// Layer 2: role classification, independent of operation policy.
internal static class ProcessIdentity
{
    public static bool IsOfficialLauncher(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return new[] { "ffxivboot", "ffxivboot64", "ffxivlauncher", "ffxivlauncher64", "ffxivupdater", "ffxivupdater64", "FfxivLauncherTC", "FfxivUpdaterTC" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
    public static bool IsGameProcessName(string name) => name.Equals("ffxiv", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv.exe", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv_dx11", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase);
    internal static bool IsPrefixOwner(string name) => IsGameProcessName(name) || IsOfficialLauncher(name)
        || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("wineserver", StringComparison.OrdinalIgnoreCase)
        || name is "wine" or "wine64" or "wine-preloader" or "wine64-preloader";
}
