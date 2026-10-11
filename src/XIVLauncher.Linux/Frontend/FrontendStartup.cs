namespace XIVLauncher.Linux.Frontend;

internal sealed record FrontendStartup(string Root, string? LauncherEntry, bool SteamEntry)
{
    internal static bool TryParse(string[] args, out FrontendStartup? startup)
    {
        startup = null;
        if (args.Length < 3 || args[0] != "--frontend-host" || args[1] != "--data-root" || !Path.IsPathFullyQualified(args[2])) return false;
        if (args.Length == 3) { startup = new(args[2], null, false); return true; }
        if (args.Length is not (5 or 6) || args[3] != "--launcher-entry" || !Path.IsPathFullyQualified(args[4]) || args.Length == 6 && args[5] != "--steam-entry") return false;
        startup = new(args[2], args[4], args.Length == 6); return true;
    }
}
