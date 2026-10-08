using System.Security.Cryptography;
using System.Text.Json;
namespace XIVLauncher.Linux;

public static class LocalDalamud
{
    private sealed record Installation(string Version, int AssetVersion, Dictionary<string, string> AssetHashes, string? SupportedGameVersion = null);
    public static bool IsTimeout(Exception error, CancellationToken caller) => !caller.IsCancellationRequested &&
        (error is TimeoutException or OperationCanceledException ||
         error is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.TimedOut } ||
         error.InnerException != null && IsTimeout(error.InnerException, caller));

    internal static void Record(string root, DalamudDistribution distribution, string assetRoot)
    {
        var hashes = Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(assetRoot, path), path => Hash(path));
        var path = Path.Combine(root, "local-dalamud.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Installation(distribution.Version, distribution.AssetVersion, hashes, distribution.SupportedGameVersion)));
        File.Move(temp, path, true);
    }
    public static RegionDalamudFiles Find(LinuxSettings settings, CancellationToken token)
    {
        var root = settings.RegionRoot; var soil = settings.SelectedRegion == "ffxiv_cn";
        if (!soil && settings.SelectedRegion is not ("ffxiv_tc" or "ffxiv")) throw new IOException("此区服没有可用的本地 Dalamud。");
        var recordPath = Path.Combine(root, "local-dalamud.json");
        var record = File.Exists(recordPath) ? JsonSerializer.Deserialize<Installation>(File.ReadAllText(recordPath)) : null;
        var version = record?.Version ?? settings.Current.DalamudVersion;
        DalamudSources.ValidateVersion(version);
        var addon = Path.Combine(root, soil ? "addon/Hooks" : "addon", version);
        var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(addon, "hashes.json")));
        if (hashes == null || hashes.Count == 0) throw new IOException("本地 Dalamud 缺少完整性清单。");
        foreach (var entry in hashes)
        {
            token.ThrowIfCancellationRequested();
            var path = DalamudUpdateService.SafeChild(addon, entry.Key);
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(MD5.HashData(stream)).Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
                throw new IOException("本地 Dalamud 程序校验失败，不能跳过更新。");
        }
        foreach (var name in new[] { "Dalamud.Injector.exe", "Dalamud.dll", "Dalamud.Boot.dll" }) Require(addon, name);
        var runtime = Path.Combine(root, "runtime"); var runtimeVersion = File.ReadAllText(Path.Combine(runtime, "version")).Trim();
        DalamudSources.ValidateVersion(runtimeVersion);
        using (var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(addon, "Dalamud.runtimeconfig.json"))))
        {
            var localVersion = Version.Parse(runtimeVersion);
            foreach (var framework in config.RootElement.GetProperty("runtimeOptions").GetProperty("frameworks").EnumerateArray())
            {
                var required = Version.Parse(framework.GetProperty("version").GetString()!);
                if (localVersion.Major != required.Major || localVersion.Minor != required.Minor || localVersion < required)
                    throw new IOException("本地 Dalamud 与运行时版本不匹配，不能跳过更新。");
            }
        }
        Require(runtime, $"host/fxr/{runtimeVersion}/hostfxr.dll");
        Require(runtime, $"shared/Microsoft.NETCore.App/{runtimeVersion}/System.Private.CoreLib.dll");
        Require(runtime, $"shared/Microsoft.NETCore.App/{runtimeVersion}/coreclr.dll");
        Require(runtime, $"shared/Microsoft.WindowsDesktop.App/{runtimeVersion}/PresentationCore.dll");
        var assetVersion = record?.AssetVersion;
        if (assetVersion == null)
        {
            // 0.5.0 installations predate the receipt. Check their established asset version locally.
            var marker = Path.Combine(root, "assets/asset.ver");
            assetVersion = File.Exists(marker) && int.TryParse(File.ReadAllText(marker).Trim(), out var v) ? v :
                Directory.EnumerateDirectories(Path.Combine(root, "assets")).Select(p => int.TryParse(Path.GetFileName(p), out var n) ? n : -1).Max();
        }
        if (assetVersion < 0) throw new IOException("缺少本地 Dalamud 资源。");
        var assets = Path.Combine(root, "assets", assetVersion.ToString()!);
        foreach (var file in new[] { "UIRes/FontAwesomeFreeSolid.otf", "UIRes/Inconsolata-Regular.ttf", "UIRes/gamesym.ttf", "UIRes/NotoSansCJKjp-Medium.otf", "UIRes/defaultIcon.png", "UIRes/logo.png" }) Require(assets, file);
        if (record != null)
        {
            if (record.AssetHashes.Count == 0) throw new IOException("本地 Dalamud 资源清单为空。");
            foreach (var entry in record.AssetHashes)
            { token.ThrowIfCancellationRequested(); if (Hash(DalamudUpdateService.SafeChild(assets, entry.Key)) != entry.Value) throw new IOException("本地 Dalamud 资源校验失败。"); }
        }
        return new(new FileInfo(Path.Combine(addon, "Dalamud.Injector.exe")), new DirectoryInfo(runtime), new DirectoryInfo(assets), GameClientLanguage.For(settings), soil, record?.SupportedGameVersion);
    }
    private static void Require(string root, string file)
    { if (!File.Exists(Path.Combine(root, file)) || new FileInfo(Path.Combine(root, file)).Length == 0) throw new IOException("本地 Dalamud 组件不完整，不能跳过更新。"); }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public static void Disable(LinuxSettings settings) { settings.EnableDalamud = false; settings.Save(); }
}
