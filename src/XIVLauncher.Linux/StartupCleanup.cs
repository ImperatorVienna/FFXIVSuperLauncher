namespace XIVLauncher.Linux;

// Only abandoned atomic-write staging files in known launcher directories.
// Downloads, backups, lock files and Proton/game data have different lifetimes.
internal static class StartupCleanup
{
    internal static void Run(string root)
    {
        Clean(root, [Platforms.PlatformServices.Storage.SettingsFileName, "appimage-launcher"]);
        foreach (var region in new[] { "ffxiv_cn", "ffxiv", "ffxiv_tc" })
        {
            var directory = Path.Combine(root, region);
            if (new DirectoryInfo(directory).LinkTarget != null) continue;
            Clean(directory, ["settings.json", "credentials.json"]);
            Clean(Path.Combine(directory, "dalamud"), ["dalamudConfig.json"]);
        }
    }

    private static void Clean(string directory, string[] names)
    {
        try
        {
            if (!Directory.Exists(directory) || new DirectoryInfo(directory).LinkTarget != null) return;
            foreach (var path in Directory.EnumerateFiles(directory, "*.tmp"))
            {
                var name = Path.GetFileName(path);
                if (!names.Any(prefix => name.Length == prefix.Length + 37 && name.StartsWith(prefix + ".", StringComparison.Ordinal) &&
                    Guid.TryParseExact(name[(prefix.Length + 1)..^4], "N", out _))) continue;
                try
                {
                    var info = new FileInfo(path);
                    if (info.LinkTarget != null || info.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-1)) continue;
                    using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
