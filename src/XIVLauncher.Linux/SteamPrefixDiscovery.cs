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
    private static string CanonicalDirectory(string path)
    {
        var full = Path.GetFullPath(path); var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            current = new DirectoryInfo(current).ResolveLinkTarget(true)?.FullName ?? current;
        }
        return current;
    }
    private static bool Matches(string entry, string variable, string expected)
    {
        if (!entry.StartsWith(variable, StringComparison.Ordinal)) return false;
        try { return CanonicalDirectory(entry[variable.Length..]) == CanonicalDirectory(expected); }
        catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; } catch (ArgumentException) { return false; }
    }
    public static bool IsPrefixOwner(string name) => GameProcessGuard.IsGameProcessName(name)
        || GameProcessGuard.IsOfficialLauncher(name) || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("wineserver", StringComparison.OrdinalIgnoreCase)
        || name is "wine" or "wine64" or "wine-preloader" or "wine64-preloader";
    public static void EnsureIdle(string path)
    {
        var data = Normalize(path); var prefix = Path.Combine(data, "pfx");
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                // Steam wrappers inherit the prefix variables without using Wine.
                // Only actual Wine processes can hold this environment busy.
                string name;
                try { name = process.ProcessName; } catch (InvalidOperationException) { continue; }
                if (!IsPrefixOwner(name)) continue;
                string[] environment;
                try { environment = File.ReadAllText($"/proc/{process.Id}/environ").Split('\0'); }
                catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                if (environment.Any(x => Matches(x, "WINEPREFIX=", prefix) || Matches(x, "STEAM_COMPAT_DATA_PATH=", data)))
                    throw new CredentialValidationException("The selected Steam prefix is in use.", "所选 Steam 兼容环境正在使用中，请先退出 Steam 启动的游戏或官方启动器后重试。");
            }
        }
    }
}
