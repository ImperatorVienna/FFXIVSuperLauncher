using System.Text.RegularExpressions;

namespace XIVLauncher.Common.Unix;

public sealed record ProtonInstallation(string Name, string Script, string SteamRoot, string? RuntimeEntryPoint, string? RequiredRuntimeAppId)
{
    public bool IsReady => RequiredRuntimeAppId == null || RuntimeEntryPoint != null;
    public override string ToString() => IsReady ? Name : $"{Name}（缺少 Steam Runtime {RequiredRuntimeAppId}）";
}

/// <summary>Reads Steam metadata only; discovery never starts a tool or changes a prefix.</summary>
public static partial class ProtonDiscovery
{
    public static IReadOnlyList<ProtonInstallation> Discover() => Discover(
        GetSteamRoots(),
        ["/usr/share/steam/compatibilitytools.d", "/usr/local/share/steam/compatibilitytools.d"]);

    public static IEnumerable<string> GetSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(data) || !Path.IsPathRooted(data)) data = Path.Combine(home, ".local/share");
        return new[]
        {
            Path.Combine(data, "Steam"), Path.Combine(home, ".steam/steam"), Path.Combine(home, ".steam/root"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam")
        }.Where(Directory.Exists).Select(Canonical).Distinct(StringComparer.Ordinal);
    }

    public static IReadOnlyList<ProtonInstallation> Discover(IEnumerable<string> steamRoots, IEnumerable<string> systemToolRoots)
    {
        var roots = steamRoots.Where(Directory.Exists).Select(Canonical).Distinct(StringComparer.Ordinal).ToArray();
        var libraries = roots.SelectMany(GetLibraries).Distinct(StringComparer.Ordinal).ToArray();
        var directories = systemToolRoots.Concat(roots.Select(x => Path.Combine(x, "compatibilitytools.d")))
            .Concat(libraries.Select(x => Path.Combine(x, "steamapps/common")))
            .Concat(libraries.Select(x => Path.Combine(x, "compatibilitytools.d")))
            .SelectMany(SafeDirectories).Distinct(StringComparer.Ordinal);
        var result = new Dictionary<string, ProtonInstallation>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            if (!File.Exists(Path.Combine(directory, "proton"))) continue;
            var script = Path.Combine(Canonical(directory), "proton");
            var name = ReadValue(Path.Combine(directory, "compatibilitytool.vdf"), "display_name") ?? Path.GetFileName(directory);
            var appId = ReadValue(Path.Combine(directory, "toolmanifest.vdf"), "require_tool_appid");
            var runtime = FindRuntime(libraries, appId);
            var owner = roots.FirstOrDefault(root => GetLibraries(root).Any(lib => directory.StartsWith(lib + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                        ?? roots.FirstOrDefault() ?? string.Empty;
            result[script] = new(name, script, owner, runtime, appId);
        }
        return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ProtonInstallation FromScript(string script, string steamRoot)
    {
        script = Path.GetFullPath(script);
        if (Directory.Exists(script)) script = Path.Combine(script, "proton");
        if (!File.Exists(script)) throw new FileNotFoundException("找不到 Proton 脚本", script);
        var directory = Path.GetDirectoryName(script)!;
        var appId = ReadValue(Path.Combine(directory, "toolmanifest.vdf"), "require_tool_appid");
        return new(ReadValue(Path.Combine(directory, "compatibilitytool.vdf"), "display_name") ?? Path.GetFileName(directory),
            script, steamRoot, FindRuntime(GetLibraries(steamRoot), appId), appId);
    }

    private static string? FindRuntime(IEnumerable<string> libraries, string? appId)
    {
        if (appId == null) return null;
        foreach (var lib in libraries)
        {
            var installDir = ReadValue(Path.Combine(lib, "steamapps", $"appmanifest_{appId}.acf"), "installdir");
            if (installDir == null) continue;
            var entry = Path.Combine(lib, "steamapps/common", installDir, "_v2-entry-point");
            if (File.Exists(entry)) return entry;
        }
        return null;
    }

    public static IEnumerable<string> GetLibraries(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) yield break;
        yield return root;
        var text = SafeRead(Path.Combine(root, "steamapps/libraryfolders.vdf"));
        if (text == null) yield break;
        foreach (Match match in KeyValueRegex().Matches(text))
            if (match.Groups[1].Value.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                var path = Unescape(match.Groups[2].Value);
                if (Path.IsPathRooted(path) && Directory.Exists(path)) yield return Canonical(path);
            }
    }

    private static IEnumerable<string> SafeDirectories(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static string Canonical(string path) => new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(path);
    private static string? SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string? ReadValue(string path, string key)
    {
        var text = SafeRead(path);
        return text == null ? null : KeyValueRegex().Matches(text).Cast<Match>()
            .Where(m => m.Groups[1].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(m => Unescape(m.Groups[2].Value)).FirstOrDefault();
    }

    private static string Unescape(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex("\"((?:[^\"\\\\]|\\\\.)*)\"\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex KeyValueRegex();
}
