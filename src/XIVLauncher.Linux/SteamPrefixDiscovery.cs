using XIVLauncher.Common.Unix;
namespace XIVLauncher.Linux;
public sealed record SteamGamePrefix(string AppId, string DataDirectory)
{
    public override string ToString() => $"{Localization.Display(AppId == "39210" ? "正式版" : "试玩版")} ({AppId}) — {DataDirectory}";
}
public static class SteamPrefixDiscovery
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new IOException("Select an existing Steam compatibility data directory.");
        path = Path.GetFullPath(path.Trim());
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) == "pfx") path = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path))!;
        path = CanonicalDirectory(path);
        var prefix = Path.Combine(path, "pfx");
        if (!Directory.Exists(Path.Combine(prefix, "drive_c")) || !File.Exists(Path.Combine(prefix, "system.reg")))
            throw new IOException("The selected directory does not contain an initialized Proton pfx (drive_c and system.reg).");
        return path;
    }
    public static IReadOnlyList<SteamGamePrefix> Discover(IEnumerable<string> roots)
    {
        var found = new Dictionary<string, SteamGamePrefix>(StringComparer.Ordinal);
        foreach (var library in roots.Where(Directory.Exists).SelectMany(ProtonDiscovery.GetLibraries).Distinct())
            foreach (var id in new[] { "39210", "312060" })
            {
                try { var path = Normalize(Path.Combine(library, "steamapps", "compatdata", id)); found[path] = new(id, path); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        return found.Values.OrderBy(x => x.AppId).ThenBy(x => x.DataDirectory).ToArray();
    }
    private static string CanonicalDirectory(string path) => Processes.ProcessSessions.CanonicalDirectory(path);
    public static bool IsPrefixOwner(string name) => Processes.ProcessIdentity.IsPrefixOwner(name);
    public static void EnsureIdle(string path)
    {
        var data = Normalize(path);
        if (Processes.ProcessSessions.Find(Path.Combine(data, "pfx"), true, data) != Processes.ProcessPresence.Gone)
            throw new CredentialValidationException("The selected Steam prefix is in use or cannot be inspected.", "所选 Steam 兼容环境正在使用中或无法确认空闲，请先退出相关游戏或官方启动器后重试。");
    }
}
